// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Gives the writers conforming copies of the catalog, page tree nodes and pages they are about to write: the required
/// /Type entries, a /Kids array, a /Count that matches the pages below, a /Parent that names the node that lists the
/// page, and page boxes with their corners the right way round. The document's own objects are never changed. Each fix
/// is reported as <see cref="PdfDiagnosticCode.FixedOnSave"/> so a viewer can tell the user.
/// </summary>
[DebuggerDisplay("PdfSaveRepairs: {_nodes.Count} page tree nodes")]
internal sealed class PdfSaveRepairs
{
    /// <summary>The page tree nodes and pages, by object number.</summary>
    private readonly Dictionary<int, TreeNode> _nodes = [];

    /// <summary>The pages found by scanning when the page tree led to none, in object order.</summary>
    private readonly SortedSet<int> _scannedPages = [];

    /// <summary>The objects the fixes read.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The object number of the catalog, or 0.</summary>
    private readonly int _catalogNumber;

    /// <summary>The object number the catalog's /Pages must name when it named nothing usable; otherwise 0.</summary>
    private readonly int _catalogPages;

    /// <summary>The object number of the page tree root when its kids were rebuilt from a scan; otherwise 0.</summary>
    private int _scannedRoot;

    /// <summary>The page tree root made as a new object, or <see langword="null"/>.</summary>
    private PdfDictionary? _madeRoot;

    /// <summary>Initializes a new instance of the <see cref="PdfSaveRepairs"/> class by walking the page tree once.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="allowNewObject">Whether a page tree root may be made as a new object, which only a writer that numbers the whole file can place.</param>
    internal PdfSaveRepairs(PdfObjectStore store, bool allowNewObject)
    {
        _store = store;
        var root = store.Trailer.GetRaw(KnownName.Root);
        _catalogNumber = root.IsReference ? root.AsReference().Number : 0;
        var pages = store.Catalog.GetRaw(KnownName.Pages);
        var pagesNumber = pages.IsReference && StoreReading.Resolve(store, pages).AsDictionary() is not null ? pages.AsReference().Number : FindPagesRoot();
        if (pagesNumber == 0)
        {
            _catalogPages = allowNewObject ? MakeRoot() : 0;
            return;
        }

        _catalogPages = pages.IsReference && pages.AsReference().Number == pagesNumber ? 0 : pagesNumber;
        if (Walk(pagesNumber, 0, 0) == 0)
        {
            RebuildFromScan(pagesNumber);
        }
    }

    /// <summary>Gets the object the repairs made, when its number is asked for; it takes the number one past the document's objects.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="value">The object.</param>
    /// <returns><see langword="true"/> when the repairs made that object.</returns>
    internal bool TryGetNewObject(int number, out PdfValue value)
    {
        value = _madeRoot is not null && number == _store.Size ? PdfValue.FromDictionary(_madeRoot) : default;
        return !value.IsNull;
    }

    /// <summary>Returns the conforming form of an object about to be written.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="value">The object's value.</param>
    /// <returns>The value itself when it needs no fix; otherwise a fixed copy.</returns>
    internal PdfValue Fix(int number, PdfValue value)
    {
        if (value.Kind == PdfKind.Stream && (number == _catalogNumber || _nodes.ContainsKey(number)))
        {
            value = WithoutData(value.AsStream()!, number);
        }

        if (value.Kind != PdfKind.Dictionary || value.AsDictionary() is not { } source)
        {
            return value;
        }

        PdfDictionary? copy = null;
        if (number == _catalogNumber)
        {
            SetName(source, ref copy, KnownName.Type, KnownName.Catalog, number);
            FixCatalogPages(source, ref copy, number);
            DeclareExtensions(source, ref copy, number);
        }

        if (_nodes.TryGetValue(number, out var node))
        {
            FixNode(source, ref copy, number, node);
            FixScanned(source, ref copy, number);
        }

        return copy is null ? value : PdfValue.FromDictionary(copy);
    }

    /// <summary>Finds an inheritable entry in a page's old ancestors.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The nearest ancestor's value, or null.</returns>
    private static PdfValue InheritedValue(PdfDictionary page, KnownName key)
    {
        var parent = page.GetDictionary(KnownName.Parent);
        for (var depth = 0; parent is not null && depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            if (!parent.GetRaw(key).IsNull)
            {
                return parent.GetRaw(key);
            }

            parent = parent.GetDictionary(KnownName.Parent);
        }

        return default;
    }

    /// <summary>Applies the fixes that belong to a page tree rebuilt from a scan: new kids for the root, inherited attributes copied into the pages.</summary>
    /// <param name="source">The object's dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The object number.</param>
    private void FixScanned(PdfDictionary source, ref PdfDictionary? copy, int number)
    {
        if (number == _scannedRoot)
        {
            Edit(source, ref copy, number, "The page tree listed no pages, so it was rebuilt from the pages found.").Set(KnownName.Kids, PdfValue.FromArray(ScannedKids()));
        }
        else if (_scannedPages.Contains(number))
        {
            FlattenInherited(source, ref copy, number);
        }
    }

    /// <summary>Makes a plain dictionary of a catalog, page tree node or page that was read as a stream, dropping the entries that only a stream has.</summary>
    /// <param name="stream">The object read as a stream.</param>
    /// <param name="number">The object number.</param>
    /// <returns>The object as a dictionary.</returns>
    private PdfValue WithoutData(PdfStream stream, int number)
    {
        var plain = stream.Dictionary.Clone();
        _ = plain.Remove(KnownName.Length);
        _ = plain.Remove(KnownName.Filter);
        _ = plain.Remove(KnownName.DecodeParms);
        PdfOpenContext.Report(_store.Context, PdfDiagnosticCode.FixedOnSave, "A catalog, page tree node or page had stream data, which it cannot have, so it was written without it.", number, -1);
        return PdfValue.FromDictionary(plain);
    }

    /// <summary>Finds the root of the page tree when the catalog names none: a <c>/Type /Pages</c> node with no /Parent.</summary>
    /// <returns>The node's object number, or 0.</returns>
    private int FindPagesRoot()
    {
        for (var number = 1; number < _store.Size; number++)
        {
            if (StoreReading.GetObject(_store, new(number, 0)).AsDictionary() is { } node && node.IsName(KnownName.Type, KnownName.Pages) && !node.ContainsKey(KnownName.Parent))
            {
                return number;
            }
        }

        // A node whose /Type was damaged is still the root when the pages lead up to it through their /Parent.
        for (var number = 1; number < _store.Size; number++)
        {
            if (StoreReading.GetObject(_store, new(number, 0)).AsDictionary() is { } page && page.IsName(KnownName.Type, KnownName.Page) && TopmostAncestor(page) is var top and > 0)
            {
                return top;
            }
        }

        return 0;
    }

    /// <summary>Follows a page's /Parent references to the top.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The object number of the topmost ancestor, or 0 when the page has no parent dictionary.</returns>
    private int TopmostAncestor(PdfDictionary page)
    {
        var top = 0;
        var current = page;
        for (var depth = 0; depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            var parent = current.GetRaw(KnownName.Parent);
            if (!parent.IsReference || StoreReading.Resolve(_store, parent).AsDictionary() is not { } next)
            {
                break;
            }

            top = parent.AsReference().Number;
            current = next;
        }

        return top;
    }

    /// <summary>Points the catalog's /Pages at the page tree root found by scanning, when it named none.</summary>
    /// <param name="source">The catalog dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The catalog's object number.</param>
    private void FixCatalogPages(PdfDictionary source, ref PdfDictionary? copy, int number)
    {
        if (_catalogPages != 0)
        {
            Edit(source, ref copy, number, "The catalog did not name the page tree, so the one found was named.").Set(KnownName.Pages, PdfValue.FromReference(new(_catalogPages, 0)));
        }
    }

    /// <summary>
    /// Makes a page tree of the pages found by scanning, for a file whose own page tree leads to none: they become the kids
    /// of the root node, and (in <see cref="Fix"/>) take on the attributes they inherited from their old parents.
    /// </summary>
    /// <param name="root">The object number of the page tree root.</param>
    private void RebuildFromScan(int root)
    {
        if (StoreReading.GetObject(_store, new(root, 0)).AsDictionary() is null)
        {
            return;
        }

        ScanPages(root);
        if (_scannedPages.Count == 0)
        {
            return;
        }

        _scannedRoot = root;
        _nodes[root] = new(false, 0, _scannedPages.Count);
    }

    /// <summary>Makes a page tree root as a new object over the pages found by scanning, for a file that has no page tree node at all.</summary>
    /// <returns>The new object's number, or 0 when the file has no pages.</returns>
    private int MakeRoot()
    {
        var root = _store.Size;
        ScanPages(root);
        if (_scannedPages.Count == 0)
        {
            return 0;
        }

        var node = new PdfDictionary(_store);
        node.Add(KnownName.Type, PdfValue.FromName(KnownName.Pages));
        node.Add(KnownName.Kids, PdfValue.FromArray(ScannedKids()));
        node.Add(KnownName.Count, PdfValue.FromInteger(_scannedPages.Count));
        _madeRoot = node;
        _nodes[root] = new(false, 0, _scannedPages.Count);
        PdfOpenContext.Report(_store.Context, PdfDiagnosticCode.FixedOnSave, "The file had no page tree, so one was made from the pages found.", 0, -1);
        return root;
    }

    /// <summary>Finds every page object in the file and gives it the root as its parent.</summary>
    /// <param name="root">The object number of the page tree root, which is not a page.</param>
    private void ScanPages(int root)
    {
        for (var number = 1; number < _store.Size; number++)
        {
            AddScannedPage(number, root);
        }
    }

    /// <summary>Adds an object to the scanned pages when it is a page.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="root">The object number of the page tree root.</param>
    private void AddScannedPage(int number, int root)
    {
        if (number == root || StoreReading.GetObject(_store, new(number, 0)).AsDictionary() is not { } page || !page.IsName(KnownName.Type, KnownName.Page))
        {
            return;
        }

        _ = _scannedPages.Add(number);
        _nodes[number] = new(true, root, 1);
    }

    /// <summary>Makes the /Kids of a rebuilt root: one reference for each page found.</summary>
    /// <returns>The array.</returns>
    private PdfArray ScannedKids()
    {
        var kids = new PdfArray(_store, _scannedPages.Count);
        foreach (var number in _scannedPages)
        {
            kids.Add(PdfValue.FromReference(new(number, 0)));
        }

        return kids;
    }

    /// <summary>Copies the resources, boxes and rotation a page inherited from its old parents into the page, now that its parent is the new root.</summary>
    /// <param name="source">The page dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The page's object number.</param>
    private void FlattenInherited(PdfDictionary source, ref PdfDictionary? copy, int number)
    {
        ReadOnlySpan<KnownName> keys = [KnownName.Resources, KnownName.MediaBox, KnownName.CropBox, KnownName.Rotate];
        foreach (var key in keys)
        {
            if (source.GetRaw(key).IsNull && InheritedValue(source, key) is { IsNull: false } value)
            {
                Edit(source, ref copy, number, "A page's inherited attributes were copied into it when the page tree was rebuilt.").Set(key, value);
            }
        }
    }

    /// <summary>Walks a node and the nodes below it.</summary>
    /// <param name="number">The node's object number.</param>
    /// <param name="parent">The number of the node that lists it, or 0.</param>
    /// <param name="depth">The depth.</param>
    /// <returns>The pages in and below the node, counting a page as one.</returns>
    private int Walk(int number, int parent, int depth)
    {
        if (depth > PdfLimits.MaxPageTreeDepth || _nodes.ContainsKey(number) || StoreReading.GetObject(_store, new(number, 0)).AsDictionary() is not { } node)
        {
            return 0;
        }

        if (node.GetArray(KnownName.Kids) is not { } kids || node.IsName(KnownName.Type, KnownName.Page))
        {
            _nodes[number] = new(true, parent, 1);
            return 1;
        }

        // Added first, so a loop back to this node stops here.
        _nodes[number] = new(false, parent, 0);
        var pages = 0;
        for (var i = 0; i < kids.Count; i++)
        {
            var kid = kids.GetRaw(i);
            pages += kid.IsReference ? Walk(kid.AsReference().Number, number, depth + 1) : 0;
        }

        _nodes[number] = new(false, parent, pages);
        return pages;
    }

    /// <summary>Fixes a page tree node or a page.</summary>
    /// <param name="source">The object's dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The object number.</param>
    /// <param name="node">What the walk found about the object.</param>
    private void FixNode(PdfDictionary source, ref PdfDictionary? copy, int number, in TreeNode node)
    {
        SetName(source, ref copy, KnownName.Type, node.IsPage ? KnownName.Page : KnownName.Pages, number);
        FixParent(source, ref copy, number, node.Parent);
        FixBox(source, ref copy, KnownName.MediaBox, number);
        FixBox(source, ref copy, KnownName.CropBox, number);
        if (node.IsPage)
        {
            FixResources(source, ref copy, number, node.Parent);
            return;
        }

        if (source.GetInt32(KnownName.Count, -1) != node.Pages)
        {
            Edit(source, ref copy, number, "A page tree node's /Count was wrong.").Set(KnownName.Count, PdfValue.FromInteger(node.Pages));
        }
    }

    /// <summary>Gives a page that has no /Resources, own or inherited, an empty resource dictionary, because ISO 32000 requires the entry.</summary>
    /// <param name="source">The page dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The page's object number.</param>
    /// <param name="parent">The number of the node that lists the page.</param>
    private void FixResources(PdfDictionary source, ref PdfDictionary? copy, int number, int parent)
    {
        if (!source.GetRaw(KnownName.Resources).IsNull || !InheritedValue(source, KnownName.Resources).IsNull || TreeHasResources(parent))
        {
            return;
        }

        Edit(source, ref copy, number, "A page had no /Resources, which a page needs, so an empty one was added.").Set(KnownName.Resources, PdfValue.FromDictionary(new(_store)));
    }

    /// <summary>Determines whether a page tree node, or one of its ancestors in the tree, holds /Resources for the pages below.</summary>
    /// <param name="node">The object number of the first node to look at.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool TreeHasResources(int node)
    {
        for (var depth = 0; node != 0 && depth < PdfLimits.MaxPageTreeDepth; depth++)
        {
            if (StoreReading.GetObject(_store, new(node, 0)).AsDictionary() is { } dictionary && !dictionary.GetRaw(KnownName.Resources).IsNull)
            {
                return true;
            }

            node = _nodes.TryGetValue(node, out var found) ? found.Parent : 0;
        }

        return false;
    }

    /// <summary>Sets /Parent to the node that lists the object, and removes it from the root.</summary>
    /// <param name="source">The object's dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The object number.</param>
    /// <param name="parent">The number of the node that lists the object, or 0 for the root.</param>
    private void FixParent(PdfDictionary source, ref PdfDictionary? copy, int number, int parent)
    {
        var current = source.GetRaw(KnownName.Parent);
        if (parent == 0)
        {
            if (!current.IsNull)
            {
                _ = Edit(source, ref copy, number, "The root of the page tree had a /Parent.").Remove(KnownName.Parent);
            }

            return;
        }

        if (!current.IsReference || current.AsReference().Number != parent)
        {
            Edit(source, ref copy, number, "A page tree entry's /Parent was missing or wrong.").Set(KnownName.Parent, PdfValue.FromReference(new(parent, 0)));
        }
    }

    /// <summary>Swaps the corners of a box that has them the wrong way round, and replaces or removes an unusable one.</summary>
    /// <param name="source">The page dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="key">The box key.</param>
    /// <param name="number">The page's object number.</param>
    private void FixBox(PdfDictionary source, ref PdfDictionary? copy, KnownName key, int number)
    {
        var state = PageBoxes.Inspect(source, key);
        if (state == PageBoxes.BoxState.Swapped && PageBoxes.Corrected(source, key) is { } corrected)
        {
            Edit(source, ref copy, number, "A page box had its corners the wrong way round.").Set(key, PdfValue.FromArray(corrected));
        }
        else if (state == PageBoxes.BoxState.Unusable)
        {
            ReplaceUnusable(source, ref copy, key, number);
        }
    }

    /// <summary>Replaces an unusable /MediaBox with US Letter, as readers do, and removes any other unusable box.</summary>
    /// <param name="source">The page dictionary.</param>
    /// <param name="copy">The copy.</param>
    /// <param name="key">The box key.</param>
    /// <param name="number">The page's object number.</param>
    private void ReplaceUnusable(PdfDictionary source, ref PdfDictionary? copy, KnownName key, int number)
    {
        var edited = Edit(source, ref copy, number, "A page box was empty or not four numbers.");
        if (key == KnownName.MediaBox)
        {
            edited.Set(key, PdfValue.FromArray(PdfPage.DefaultMediaBox.ToArray(_store)));
            return;
        }

        _ = edited.Remove(key);
    }

    /// <summary>Sets a name entry that is missing or different.</summary>
    /// <param name="source">The object's dictionary.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="key">The key.</param>
    /// <param name="name">The name it must have.</param>
    /// <param name="number">The object number.</param>
    private void SetName(PdfDictionary source, ref PdfDictionary? copy, KnownName key, KnownName name, int number)
    {
        if (!source.IsName(key, name))
        {
            Edit(source, ref copy, number, "A required /Type entry was missing or wrong.").Set(key, PdfValue.FromName(name));
        }
    }

    /// <summary>Gets the copy to change, making it and reporting the fix the first time.</summary>
    /// <param name="source">The object's dictionary.</param>
    /// <param name="copy">The copy, made here when null.</param>
    /// <param name="number">The object number.</param>
    /// <param name="message">What was fixed.</param>
    /// <returns>The copy.</returns>
    private PdfDictionary Edit(PdfDictionary source, ref PdfDictionary? copy, int number, string message)
    {
        PdfOpenContext.Report(_store.Context, PdfDiagnosticCode.FixedOnSave, message, number, -1);
        return copy ??= source.Clone();
    }

    /// <summary>
    /// Adds the ISO/TS 32003 extension to the catalog of an AES-GCM document that does not declare it, and the ISO/TS 32004
    /// extension to the catalog of a document that needs a PDF MAC and does not declare it.
    /// </summary>
    /// <param name="source">The catalog.</param>
    /// <param name="copy">The copy, made when the first fix is needed.</param>
    /// <param name="number">The object number.</param>
    private void DeclareExtensions(PdfDictionary source, ref PdfDictionary? copy, int number)
    {
        var current = copy ?? source;
        if (AesGcmExtension.IsRequired(_store) && !AesGcmExtension.IsDeclared(current, _store.Names))
        {
            PdfOpenContext.Report(_store.Context, PdfDiagnosticCode.FixedOnSave, "The catalog did not declare the AES-GCM extension (ISO/TS 32003), so it was added.", number, -1);
            copy = AesGcmExtension.Declare(current, _store);
            current = copy;
        }

        if (!PdfMacExtension.IsRequired(_store) || PdfMacExtension.IsDeclared(current, _store.Names))
        {
            return;
        }

        PdfOpenContext.Report(_store.Context, PdfDiagnosticCode.FixedOnSave, "The catalog did not declare the PDF MAC extension (ISO/TS 32004), so it was added.", number, -1);
        copy = PdfMacExtension.Declare(current, _store);
    }

    /// <summary>What the walk found about a page tree node or page.</summary>
    /// <param name="IsPage">Whether the object is a page.</param>
    /// <param name="Parent">The number of the node that lists it, or 0 for the root.</param>
    /// <param name="Pages">The pages in and below it.</param>
    private readonly record struct TreeNode(bool IsPage, int Parent, int Pages);
}
