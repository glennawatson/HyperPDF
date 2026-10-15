// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// A document's logical structure tree (tagged PDF): its elements in logical order, with role maps, attributes, the
/// ID tree and the parent tree that leads from marked content and annotations back to their elements. The tree is
/// built once and never changes, so it is safe to read from any thread. Use <see cref="PdfDocumentTagged.GetStructureTree"/>
/// for the document's shared copy.
/// </summary>
[DebuggerDisplay("PdfStructureTree: {ElementCount} elements, marked={IsMarked}")]
public sealed class PdfStructureTree
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The structure tree root.</summary>
    private readonly PdfDictionary _root;

    /// <summary>The top-level elements.</summary>
    private readonly List<PdfStructureElement> _roots = [];

    /// <summary>The elements by dictionary and by id.</summary>
    private readonly StructureIndex _index = new();

    /// <summary>The ID tree, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _idTree;

    /// <summary>The parent tree, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _parentTree;

    /// <summary>Initializes a new instance of the <see cref="PdfStructureTree"/> class and builds every element.</summary>
    /// <param name="document">The document.</param>
    /// <param name="root">The structure tree root.</param>
    private PdfStructureTree(PdfDocument document, PdfDictionary root)
    {
        _document = document;
        _root = root;
        var names = new TaggedNames(document.Objects.Names);
        var catalog = document.Catalog;
        IsMarked = IsTrue(catalog.GetDictionary(KnownName.MarkInfo)?.Get(names.Marked) ?? default);
        Language = catalog.GetText(KnownName.Lang) is { Length: > 0 } language ? language : null;
        _idTree = root.GetDictionary(names.IDTree);
        _parentTree = root.GetDictionary(KnownName.ParentTree);
        new StructureTreeBuilder(document, root, names, _index).Build(root, Language, _roots);
    }

    /// <summary>Gets a value indicating whether the catalog's <c>/MarkInfo</c> says the document is tagged (ISO 32000-2 14.7.1).</summary>
    public bool IsMarked { get; }

    /// <summary>Gets the document's language, the catalog's <c>/Lang</c>, or <see langword="null"/>.</summary>
    public string? Language { get; }

    /// <summary>Gets the top-level elements, in logical order.</summary>
    public IReadOnlyList<PdfStructureElement> Roots => _roots;

    /// <summary>Gets the number of elements.</summary>
    public int ElementCount => _index.Count;

    /// <summary>Gets the structure tree root dictionary.</summary>
    public PdfDictionary Root => _root;

    /// <summary>Reads a document's structure tree. Prefer <see cref="PdfDocumentTagged.GetStructureTree"/>, which reads it once.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The tree, or <see langword="null"/> when the document has no structure tree root.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfStructureTree? Load(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Catalog.GetDictionary(KnownName.StructTreeRoot) is { } root ? new PdfStructureTree(document, root) : null;
    }

    /// <summary>Finds the element built from a dictionary.</summary>
    /// <param name="dictionary">The element's dictionary.</param>
    /// <returns>The element, or <see langword="null"/> when the tree does not reach it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public PdfStructureElement? Find(PdfDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        return _index.Find(dictionary);
    }

    /// <summary>Finds an element by its element id, through the ID tree or the ids the elements carry.</summary>
    /// <param name="id">The id, as in a table cell's <c>/Headers</c>.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <see langword="null"/>.</exception>
    public PdfStructureElement? FindById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (_index.FindById(id) is { } element)
        {
            return element;
        }

        // A PDF 2.0 writer may key the tree with UTF-8, an older one with PDFDocEncoding or UTF-16.
        var found = NameTree.Find(_idTree, PdfText.Encode(id)).AsDictionary() ?? NameTree.Find(_idTree, PdfText.EncodeUtf8(id)).AsDictionary();
        return found is { } dictionary ? _index.Find(dictionary) : null;
    }

    /// <summary>Gets a page's <c>/StructParents</c> key into the parent tree.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The key, or -1 when the page has none or does not exist.</returns>
    public int GetStructParents(int pageIndex) =>
        (uint)pageIndex < (uint)_document.PageCount ? PdfDocumentPages.GetPage(_document, pageIndex).Dictionary.GetInt32(KnownName.StructParents, -1) : -1;

    /// <summary>Gets the element that owns a marked content id on a page, through the page's parent tree entry.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The element, or <see langword="null"/> when the parent tree does not say.</returns>
    public PdfStructureElement? GetMarkedContentParent(int pageIndex, int mcid)
    {
        var key = GetStructParents(pageIndex);
        return key < 0 || mcid < 0 || NameTree.FindNumber(_parentTree, key).AsArray() is not { } parents ? null : FindElement(parents.GetRaw(mcid));
    }

    /// <summary>Gets the elements a page's parent tree entry lists, indexed by marked content id.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">Receives one entry per marked content id; <see langword="null"/> where the entry is missing.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void GetPageParents(int pageIndex, List<PdfStructureElement?> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var key = GetStructParents(pageIndex);
        if (key < 0 || NameTree.FindNumber(_parentTree, key).AsArray() is not { } parents)
        {
            return;
        }

        for (var i = 0; i < parents.Count; i++)
        {
            output.Add(FindElement(parents.GetRaw(i)));
        }
    }

    /// <summary>Gets the element that refers to an annotation or XObject, through its <c>/StructParent</c>.</summary>
    /// <param name="dictionary">The annotation or XObject dictionary.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public PdfStructureElement? GetObjectParent(PdfDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var key = dictionary.GetInt32(KnownName.StructParent, -1);
        return key >= 0 ? FindElement(NameTree.FindNumberRaw(_parentTree, key)) : null;
    }

    /// <summary>Reads a boolean that some producers write as a number.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for true or a non-zero number.</returns>
    private static bool IsTrue(PdfValue value) => value.Kind == PdfKind.Boolean ? value.AsBoolean() : value.AsInteger() != 0;

    /// <summary>Finds the element a stored value names: by object number for a reference, else by dictionary.</summary>
    /// <param name="raw">The value as stored, a reference or a direct dictionary.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    private PdfStructureElement? FindElement(PdfValue raw)
    {
        if (raw.IsReference)
        {
            return _index.Find(raw.AsReference());
        }

        var dictionary = raw.AsDictionary();
        return dictionary is null ? null : _index.Find(dictionary);
    }
}
