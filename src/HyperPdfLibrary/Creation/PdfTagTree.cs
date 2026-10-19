// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <summary>The entries of a namespace dictionary.</summary>
    private const int NamespaceEntries = 2;

    /// <summary>The most standard namespaces in a mixed tree.</summary>
    private const int StandardNamespaceCount = 2;

    /// <summary>The pages that have marked content, in the order they were first used.</summary>
    private readonly List<PageEntry> _pages = [];

    /// <summary>The document.</summary>
    private readonly IPdfObjectSink _sink;

    /// <summary>Whether a PDF 2.0 creator may promote a standalone PDF 1.7 tree when a new type needs it.</summary>
    private readonly bool _allowsPdf20Namespace;

    /// <summary>The marked-content pages by object, allocated when content is first added.</summary>
    private Dictionary<PdfObjectId, PageEntry>? _pageByObject;

    /// <summary>The PDF 2.0 namespace object, reserved on promotion when the creator allows both namespaces.</summary>
    private PdfObjectId _namespaceId;

    /// <summary>One once <see cref="Finish"/> has started, otherwise zero.</summary>
    private int _finished;

    /// <summary>Initializes a new instance of the <see cref="PdfTagTree"/> class.</summary>
    /// <param name="sink">The document the structure objects are added to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public PdfTagTree(IPdfObjectSink sink)
        : this(sink, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfTagTree"/> class.</summary>
    /// <param name="sink">The document the structure objects are added to.</param>
    /// <param name="usesPdf20Namespace">Whether common and PDF 2.0 types use an explicit PDF 2.0 namespace, with legacy-only types in the PDF 1.7 namespace.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public PdfTagTree(IPdfObjectSink sink, bool usesPdf20Namespace)
        : this(sink, usesPdf20Namespace, usesPdf20Namespace)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfTagTree"/> class with deferred namespace support.</summary>
    /// <param name="sink">The document receiving structure objects.</param>
    /// <param name="usesPdf20Namespace">Whether to identify the PDF 2.0 namespace immediately.</param>
    /// <param name="allowsPdf20Namespace">Whether the document version permits promotion to mixed namespaces.</param>
    internal PdfTagTree(IPdfObjectSink sink, bool usesPdf20Namespace, bool allowsPdf20Namespace)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _allowsPdf20Namespace = usesPdf20Namespace || allowsPdf20Namespace;
        RootId = sink.Reserve();
        Root = new(sink.Reserve(), DocumentType, null, PdfTagOptions.Default);
        _namespaceId = usesPdf20Namespace ? sink.Reserve() : default;
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
    /// <exception cref="ArgumentException">The type is unknown or requires an unavailable structure namespace.</exception>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfTagElement AddElement(PdfTagElement parent, string type) => AddElement(parent, type, PdfTagOptions.Default);

    /// <summary>Adds an element at the end of a parent's children.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="type">A standard structure type such as <c>Sect</c>, <c>P</c>, <c>H1</c>, <c>Table</c>, <c>TR</c>, <c>TD</c>, <c>L</c>, <c>LI</c> or <c>Figure</c>.</param>
    /// <param name="options">What the element says besides its type.</param>
    /// <returns>The element.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The type or an attribute is undefined, misplaced or requires an unavailable namespace or output version.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A table scope, note subtype or numbering scheme is undefined.</exception>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    public PdfTagElement AddElement(PdfTagElement parent, string type, PdfTagOptions options)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfFinished();
        var resolved = PdfTagTypeNames.Validate(type, _allowsPdf20Namespace);
        PdfTagAttributes.Validate(options);
        if (options.ListNumbering is not null)
        {
            PdfTagListNumbering.Validate(_sink, resolved, options);
        }

        if (options.PrintField is { } field)
        {
            _ = PdfTagPrintField.Validate(_sink, resolved, field);
        }

        if (_allowsPdf20Namespace && !_namespaceId.IsValid && !PdfStructureTypes.IsStandardIn(resolved, PdfStructureTypes.Pdf17Flag))
        {
            _namespaceId = _sink.Reserve();
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
        PdfDictionary? roleMap = null;
        PdfObjectId legacyNamespaceId = default;
        pending.Push(Root);
        while (pending.Count > 0)
        {
            var element = pending.Pop();
            _sink.Set(element.Id, PdfValue.FromDictionary(CreateElement(element, ref legacyNamespaceId)));
            if (!_namespaceId.IsValid && NumberedHeadings.IsExtendedName(element.Type.AsSpan()))
            {
                roleMap ??= new(_sink.Owner, 1);
                roleMap.Set(_sink.Names.Intern(element.Type), PdfValue.FromName(_sink.Names.Intern("H6")));
            }

            foreach (var kid in element.Kids)
            {
                if (kid.Element is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        _sink.Set(RootId, PdfValue.FromDictionary(CreateRoot(roleMap, legacyNamespaceId)));
        return RootId;
    }

    /// <summary>Records a piece of marked content on a page.</summary>
    /// <param name="element">The element the content belongs to.</param>
    /// <param name="page">The page's object.</param>
    /// <returns>The marked content id to write with the content.</returns>
    /// <exception cref="InvalidOperationException">The tree is finished.</exception>
    internal int AddContent(PdfTagElement element, PdfObjectId page)
    {
        ThrowIfFinished();
        var pages = _pageByObject ??= [];
        if (!pages.TryGetValue(page, out var entry))
        {
            entry = new(_pages.Count);
            pages.Add(page, entry);
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
        if (_pageByObject is { } pages && pages.TryGetValue(page, out var entry))
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
        // Empty Alt and ActualText are explicit descriptions, not absent entries.
        if (text is not null && (text.Length != 0 || key == KnownName.Alt || key == KnownName.ActualText))
        {
            dictionary.Set(key, PdfValue.FromString(PdfText.Encode(text)));
        }
    }

    /// <summary>Creates the dictionary of an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="legacyNamespaceId">The shared legacy namespace reserved by this save.</param>
    /// <returns>The dictionary.</returns>
    private PdfDictionary CreateElement(PdfTagElement element, ref PdfObjectId legacyNamespaceId)
    {
        var dictionary = new PdfDictionary(_sink.Owner, ElementEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.StructElem));
        dictionary.Set(KnownName.S, PdfValue.FromName(_sink.Names.Intern(element.Type)));
        dictionary.Set(KnownName.P, PdfValue.FromReference(element.Parent?.Id ?? RootId));
        if (_namespaceId.IsValid)
        {
            dictionary.Set(_sink.Names.Intern("NS"), PdfValue.FromReference(ElementNamespace(element, ref legacyNamespaceId)));
        }

        if (element.Page.IsValid)
        {
            dictionary.Set(KnownName.Pg, PdfValue.FromReference(element.Page));
        }

        SetKids(dictionary, element);
        SetText(dictionary, KnownName.Alt, element.Options.AlternativeText);
        SetText(dictionary, KnownName.ActualText, element.Options.ActualText);
        SetText(dictionary, KnownName.Lang, element.Options.Language);
        SetText(dictionary, _sink.Names.Intern("T"), element.Options.Title);
        if (element.Options.ListNumbering is not null)
        {
            PdfTagListNumbering.Validate(_sink, PdfStructureTypes.Find(element.TypeUtf8), element.Options);
        }

        PdfTagAttributes.Write(_sink, dictionary, element.Options);

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

    /// <summary>Creates the structure tree root.</summary>
    /// <param name="roleMap">PDF 1.7 extension-heading mappings, when needed.</param>
    /// <param name="legacyNamespaceId">The legacy namespace used by this save, or an invalid id when unused.</param>
    /// <returns>The dictionary.</returns>
    private PdfDictionary CreateRoot(PdfDictionary? roleMap, PdfObjectId legacyNamespaceId)
    {
        var root = new PdfDictionary(_sink.Owner, ElementEntries);
        root.Set(KnownName.Type, PdfValue.FromName(KnownName.StructTreeRoot));
        root.Set(KnownName.K, PdfValue.FromReference(Root.Id));
        root.Set(KnownName.ParentTree, PdfValue.FromReference(WriteParentTree()));
        root.Set(KnownName.ParentTreeNextKey, PdfValue.FromInteger(_pages.Count));
        if (roleMap is not null)
        {
            root.Set(KnownName.RoleMap, PdfValue.FromDictionary(roleMap));
        }

        if (_namespaceId.IsValid)
        {
            var namespaces = new PdfArray(_sink.Owner, legacyNamespaceId.IsValid ? StandardNamespaceCount : 1);
            namespaces.Add(PdfValue.FromReference(_namespaceId));
            if (legacyNamespaceId.IsValid)
            {
                namespaces.Add(PdfValue.FromReference(legacyNamespaceId));
                _sink.Set(legacyNamespaceId, PdfValue.FromDictionary(CreateNamespace(PdfStructureTypes.Pdf17Namespace)));
            }

            root.Set(_sink.Names.Intern("Namespaces"), PdfValue.FromArray(namespaces));
            _sink.Set(_namespaceId, PdfValue.FromDictionary(CreateNamespace(PdfStructureTypes.Pdf20Namespace)));
        }

        return root;
    }

    /// <summary>Gets the explicit namespace for a type in a mixed tree.</summary>
    /// <param name="element">The element being serialized.</param>
    /// <param name="legacyNamespaceId">The shared legacy namespace reserved by this save.</param>
    /// <returns>The standard namespace object.</returns>
    private PdfObjectId ElementNamespace(PdfTagElement element, ref PdfObjectId legacyNamespaceId)
    {
        if (PdfStructureTypes.IsStandardIn(PdfStructureTypes.Find(element.TypeUtf8), PdfStructureTypes.Pdf20Flag))
        {
            return _namespaceId;
        }

        if (!legacyNamespaceId.IsValid)
        {
            legacyNamespaceId = _sink.Reserve();
        }

        return legacyNamespaceId;
    }

    /// <summary>Creates a standard structure namespace dictionary (ISO 32000-2 14.8.6).</summary>
    /// <param name="uri">The namespace identifier.</param>
    /// <returns>The dictionary.</returns>
    private PdfDictionary CreateNamespace(string uri)
    {
        var space = new PdfDictionary(_sink.Owner, NamespaceEntries);
        space.Set(KnownName.Type, PdfValue.FromName(_sink.Names.Intern("Namespace")));
        space.Set(_sink.Names.Intern("NS"), PdfValue.FromString(PdfText.Encode(uri)));
        return space;
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
