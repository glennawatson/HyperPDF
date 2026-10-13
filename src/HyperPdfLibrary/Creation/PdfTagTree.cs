// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// The structure tree of a tagged document being created (ISO 32000-2 14.7). Elements are added under a root
/// <c>Document</c> element, pages mark their content with <see cref="PdfPageCanvas.BeginContent(PdfTagElement)"/>, and
/// <see cref="Finish"/> writes the structure elements, the structure tree root and the parent tree (14.7.4.4) that maps
/// each page's marked content back to its elements. Not thread-safe.
/// </summary>
[DebuggerDisplay("PdfTagTree: {_pages.Count} pages with content")]
public sealed class PdfTagTree
{
    /// <summary>The structure type of the root element.</summary>
    private const string DocumentType = "Document";

    /// <summary>The most key and value pairs in one leaf of the parent tree.</summary>
    private const int LeafPairs = 32;

    /// <summary>The items in a pair of a number tree: a key and a value.</summary>
    private const int PairLength = 2;

    /// <summary>The entries of a structure element dictionary.</summary>
    private const int ElementEntries = 10;

    /// <summary>The longest structure type spelling accepted.</summary>
    private const int MaxTypeLength = 32;

    /// <summary>The pages that have marked content, in the order they were first used.</summary>
    private readonly List<PageEntry> _pages = [];

    /// <summary>The same pages by object.</summary>
    private readonly Dictionary<PdfObjectId, PageEntry> _pageByObject = [];

    /// <summary>The document.</summary>
    private readonly IPdfObjectSink _sink;

    /// <summary>One once <see cref="Finish"/> has started, otherwise zero.</summary>
    private int _finished;

    /// <summary>Initializes a new instance of the <see cref="PdfTagTree"/> class.</summary>
    /// <param name="sink">The document the structure objects are added to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public PdfTagTree(IPdfObjectSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        RootId = sink.Reserve();
        Root = new(sink.Reserve(), DocumentType, null, PdfTagOptions.Default);
    }

    /// <summary>Gets the <c>Document</c> element, the parent of everything else.</summary>
    public PdfTagElement Root { get; }

    /// <summary>Gets the object id of the structure tree root, which the catalog's <c>/StructTreeRoot</c> refers to.</summary>
    public PdfObjectId RootId { get; }

    /// <summary>Adds an element at the end of a parent's children.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="type">A standard structure type such as <c>Sect</c>, <c>P</c>, <c>H1</c>, <c>Table</c>, <c>TR</c>, <c>TD</c>, <c>L</c>, <c>LI</c> or <c>Figure</c>.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parent"/> or <paramref name="type"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a standard structure type.</exception>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfTagElement AddElement(PdfTagElement parent, string type) => AddElement(parent, type, PdfTagOptions.Default);

    /// <summary>Adds an element at the end of a parent's children.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="type">A standard structure type such as <c>Sect</c>, <c>P</c>, <c>H1</c>, <c>Table</c>, <c>TR</c>, <c>TD</c>, <c>L</c>, <c>LI</c> or <c>Figure</c>.</param>
    /// <param name="options">What the element says besides its type.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="type"/> is not a standard structure type.</exception>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    public PdfTagElement AddElement(PdfTagElement parent, string type, PdfTagOptions options)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfFinished();
        var bytes = Encoding.ASCII.GetBytes(type);
        if (bytes.Length > MaxTypeLength || PdfStructureTypes.Find(bytes) == PdfStructureType.Unknown)
        {
            throw new ArgumentException($"'{type}' is not a standard structure type.", nameof(type));
        }

        var element = new PdfTagElement(_sink.Reserve(), type, parent, options);
        parent.AddChild(element);
        return element;
    }

    /// <summary>Writes the structure elements, the structure tree root and the parent tree. Calling it again does nothing.</summary>
    /// <returns>The structure tree root's object, for the catalog.</returns>
    public PdfObjectId Finish()
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return RootId;
        }

        var pending = new Stack<PdfTagElement>();
        pending.Push(Root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            _sink.Set(element.Id, PdfValue.FromDictionary(CreateElement(element)));
            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        _sink.Set(RootId, PdfValue.FromDictionary(CreateRoot()));
        return RootId;
    }

    /// <summary>Counts the figures that have no alternative text, which a reader of the document could not perceive.</summary>
    /// <returns>The number of <c>Figure</c> elements whose options give no alternative text or actual text.</returns>
    internal int CountFiguresWithoutDescription()
    {
        var count = 0;
        var pending = new Stack<PdfTagElement>();
        pending.Push(Root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            var undescribed = string.Equals(element.Type, "Figure", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(element.Options.AlternativeText)
                && string.IsNullOrWhiteSpace(element.Options.ActualText);
            count += undescribed ? 1 : 0;
            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        return count;
    }

    /// <summary>Records a piece of marked content on a page.</summary>
    /// <param name="element">The element the content belongs to.</param>
    /// <param name="page">The page's object.</param>
    /// <returns>The marked content id to write with the content.</returns>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    internal int AddContent(PdfTagElement element, PdfObjectId page)
    {
        ThrowIfFinished();
        if (!_pageByObject.TryGetValue(page, out var entry))
        {
            entry = new(_pages.Count);
            _pageByObject.Add(page, entry);
            _pages.Add(entry);
        }

        var mcid = entry.Elements.Count;
        entry.Elements.Add(element);
        element.AddContent(page, mcid);
        return mcid;
    }

    /// <summary>Gets the parent tree key of a page that has marked content.</summary>
    /// <param name="page">The page's object.</param>
    /// <param name="key">Receives the key for the page's <c>/StructParents</c>.</param>
    /// <returns><see langword="true"/> when the page has marked content.</returns>
    internal bool TryGetStructParents(PdfObjectId page, out int key)
    {
        if (_pageByObject.TryGetValue(page, out var entry))
        {
            key = entry.Key;
            return true;
        }

        key = 0;
        return false;
    }

    /// <summary>Sets a text string entry when there is text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="text">The text, or <see langword="null"/>.</param>
    private static void SetText(PdfDictionary dictionary, PdfName key, string? text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            dictionary.Set(key, PdfValue.FromString(PdfText.Encode(text)));
        }
    }

    /// <summary>Creates the dictionary of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The dictionary.</returns>
    private PdfDictionary CreateElement(PdfTagElement element)
    {
        var dictionary = new PdfDictionary(_sink.Owner, ElementEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.StructElem));
        dictionary.Set(KnownName.S, PdfValue.FromName(_sink.Names.Intern(element.Type)));
        dictionary.Set(KnownName.P, PdfValue.FromReference(element.Parent?.Id ?? RootId));
        if (element.Page.IsValid)
        {
            dictionary.Set(KnownName.Pg, PdfValue.FromReference(element.Page));
        }

        SetKids(dictionary, element);
        SetText(dictionary, KnownName.Alt, element.Options.AlternativeText);
        SetText(dictionary, KnownName.ActualText, element.Options.ActualText);
        SetText(dictionary, KnownName.Lang, element.Options.Language);
        SetText(dictionary, _sink.Names.Intern("T"), element.Options.Title);
        if (CreateAttributes(element.Options) is { } attributes)
        {
            dictionary.Set(KnownName.A, PdfValue.FromDictionary(attributes));
        }

        return dictionary;
    }

    /// <summary>Sets an element's <c>/K</c> entry.</summary>
    /// <param name="dictionary">The element dictionary.</param>
    /// <param name="element">The element.</param>
    private void SetKids(PdfDictionary dictionary, PdfTagElement element)
    {
        if (element.Kids.Count == 0)
        {
            return;
        }

        var kids = new PdfArray(_sink.Owner, element.Kids.Count);
        foreach (var kid in element.Kids)
        {
            kids.Add(CreateKid(element, kid));
        }

        dictionary.Set(KnownName.K, kids.Count == 1 ? kids.GetRaw(0) : PdfValue.FromArray(kids));
    }

    /// <summary>Creates one entry of an element's <c>/K</c>.</summary>
    /// <param name="element">The element.</param>
    /// <param name="kid">The child.</param>
    /// <returns>A reference to a child element, a marked content id, or a marked content reference when the content is on another page.</returns>
    private PdfValue CreateKid(PdfTagElement element, PdfTagElement.Kid kid)
    {
        if (kid.Element is { } child)
        {
            return PdfValue.FromReference(child.Id);
        }

        if (kid.Page == element.Page)
        {
            return PdfValue.FromInteger(kid.Mcid);
        }

        var reference = new PdfDictionary(_sink.Owner, ElementEntries);
        reference.Set(KnownName.Type, PdfValue.FromName(KnownName.MCR));
        reference.Set(KnownName.Pg, PdfValue.FromReference(kid.Page));
        reference.Set(KnownName.MCID, PdfValue.FromInteger(kid.Mcid));
        return PdfValue.FromDictionary(reference);
    }

    /// <summary>Throws when the tree is finished.</summary>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    private void ThrowIfFinished()
    {
        if (Volatile.Read(ref _finished) != 0)
        {
            throw new InvalidOperationException("The tree is finished and cannot change.");
        }
    }

    /// <summary>Creates the table attribute object of an element (ISO 32000-2 14.8.5.7).</summary>
    /// <param name="options">The element's options.</param>
    /// <returns>The attribute dictionary, or <see langword="null"/> when the element has no table attributes.</returns>
    private PdfDictionary? CreateAttributes(PdfTagOptions options)
    {
        if (options.TableScope == PdfTableScope.None && options.RowSpan <= 1 && options.ColumnSpan <= 1)
        {
            return null;
        }

        var names = _sink.Names;
        var attributes = new PdfDictionary(_sink.Owner, ElementEntries);
        attributes.Set(names.Intern("O"), PdfValue.FromName(names.Intern("Table")));
        if (options.TableScope != PdfTableScope.None)
        {
            attributes.Set(names.Intern("Scope"), PdfValue.FromName(names.Intern(options.TableScope.ToString())));
        }

        if (options.RowSpan > 1)
        {
            attributes.Set(names.Intern("RowSpan"), PdfValue.FromInteger(options.RowSpan));
        }

        if (options.ColumnSpan > 1)
        {
            attributes.Set(names.Intern("ColSpan"), PdfValue.FromInteger(options.ColumnSpan));
        }

        return attributes;
    }

    /// <summary>Creates the structure tree root.</summary>
    /// <returns>The dictionary.</returns>
    private PdfDictionary CreateRoot()
    {
        var root = new PdfDictionary(_sink.Owner, ElementEntries);
        root.Set(KnownName.Type, PdfValue.FromName(KnownName.StructTreeRoot));
        root.Set(KnownName.K, PdfValue.FromReference(Root.Id));
        root.Set(KnownName.ParentTree, PdfValue.FromReference(WriteParentTree()));
        root.Set(KnownName.ParentTreeNextKey, PdfValue.FromInteger(_pages.Count));
        return root;
    }

    /// <summary>Writes the parent tree as a number tree (ISO 32000-2 7.9.7), with leaves when it is large.</summary>
    /// <returns>The tree's root object.</returns>
    private PdfObjectId WriteParentTree()
    {
        var leaves = new List<PdfObjectId>();
        for (var start = 0; start < _pages.Count; start += LeafPairs)
        {
            var count = Math.Min(LeafPairs, _pages.Count - start);
            var nums = new PdfArray(_sink.Owner, count * PairLength);
            for (var i = start; i < start + count; i++)
            {
                nums.Add(PdfValue.FromInteger(_pages[i].Key));
                nums.Add(PdfValue.FromReference(WriteContentArray(_pages[i])));
            }

            var leaf = new PdfDictionary(_sink.Owner, ElementEntries);
            leaf.Set(KnownName.Nums, PdfValue.FromArray(nums));
            if (_pages.Count > LeafPairs)
            {
                leaf.Set(KnownName.Limits, PdfValue.FromArray(PdfArray.FromNumbers(_sink.Owner, [_pages[start].Key, _pages[start + count - 1].Key])));
            }

            leaves.Add(_sink.Add(PdfValue.FromDictionary(leaf)));
        }

        if (leaves.Count == 1)
        {
            return leaves[0];
        }

        var kids = new PdfArray(_sink.Owner, leaves.Count);
        foreach (var leaf in leaves)
        {
            kids.Add(PdfValue.FromReference(leaf));
        }

        var root = new PdfDictionary(_sink.Owner, ElementEntries);
        root.Set(KnownName.Kids, PdfValue.FromArray(kids));
        return _sink.Add(PdfValue.FromDictionary(root));
    }

    /// <summary>Writes the array that maps a page's marked content ids to their elements.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The array's object.</returns>
    private PdfObjectId WriteContentArray(PageEntry page)
    {
        var parents = new PdfArray(_sink.Owner, page.Elements.Count);
        foreach (var element in page.Elements)
        {
            parents.Add(PdfValue.FromReference(element.Id));
        }

        return _sink.Add(PdfValue.FromArray(parents));
    }

    /// <summary>A page that has marked content.</summary>
    private sealed class PageEntry
    {
        /// <summary>Initializes a new instance of the <see cref="PageEntry"/> class.</summary>
        /// <param name="key">The parent tree key.</param>
        internal PageEntry(int key) => Key = key;

        /// <summary>Gets the parent tree key, which is the page's <c>/StructParents</c>.</summary>
        internal int Key { get; }

        /// <summary>Gets the element of each marked content id.</summary>
        internal List<PdfTagElement> Elements { get; } = [];
    }
}
