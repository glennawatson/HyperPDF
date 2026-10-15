// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Finds and removes structure kids that point at nothing: a marked content reference whose id no content stream of its
/// page draws any more, and an object reference to an object that is gone (ISO 32000-1:2008, 14.7.4 and 14.7.4.4). An edit
/// such as a redaction or a page change can leave them, and the tags then describe content the page no longer has, which
/// breaks the logical reading order PDF/UA-1 requires (ISO 14289-1:2014, 7.1). An element left with no kid is deleted,
/// and the parent tree forgets what was removed, so nothing in the tree points at a deleted element.
/// </summary>
[DebuggerDisplay("PdfUaStructureReferences: {_affected.Count} elements affected")]
internal sealed class PdfUaStructureReferences
{
    /// <summary>The most elements visited.</summary>
    private const int MaxElements = StructureTreeBuilder.MaxElements;

    /// <summary>The most elements, marked content ids or pages a kept set may hold before it is dropped instead of kept for the next scan.</summary>
    private const int MaxKeptSet = 1 << 12;

    /// <summary>The scan left behind by the last check, so the next one reuses its collections.</summary>
    private static PdfUaStructureReferences? _spare;

    /// <summary>The index of each page by the number of its object.</summary>
    private readonly Dictionary<int, int> _pagesByObject = [];

    /// <summary>The marked content ids each page draws, filled for the pages the tree points at.</summary>
    private readonly Dictionary<int, HashSet<int>> _drawn = [];

    /// <summary>The elements that have a kid pointing at nothing.</summary>
    private readonly List<PdfStructureElement> _affected = [];

    /// <summary>The pages that have a reference to marked content that is gone, with the ids.</summary>
    private readonly Dictionary<int, HashSet<int>> _missing = [];

    /// <summary>The elements that are, or hold, an element with a kid pointing at nothing.</summary>
    private readonly HashSet<PdfStructureElement> _chain = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The new kids of each changed element, by object number.</summary>
    private readonly Dictionary<int, PdfArray> _newKids = [];

    /// <summary>The numbers of the elements that were left with no kid and are deleted.</summary>
    private readonly HashSet<int> _deleted = [];

    /// <summary>The marked content a page's content stream names, refilled for each page.</summary>
    private readonly List<ScannedMark> _marks = [];

    /// <summary>The empty sets kept for the next page or scan.</summary>
    private readonly List<HashSet<int>> _freeSets = [];

    /// <summary>The page indexes of the pages with missing content, in order.</summary>
    private readonly List<int> _order = [];

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The structure tree.</summary>
    private PdfStructureTree _tree = null!;

    /// <summary>The <c>/Stm</c> name of the document.</summary>
    private PdfName _stm;

    /// <summary>The cancellation token.</summary>
    private CancellationToken _cancellationToken;

    /// <summary>Adds a finding for each page whose tags point at marked content the page no longer draws, and one for the whole document for a missing object.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfUaFinding> findings, CancellationToken cancellationToken)
    {
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return;
        }

        var scan = Start(document, tree, cancellationToken);
        try
        {
            if (!scan.Find())
            {
                return;
            }

            scan._order.AddRange(scan._missing.Keys);
            scan._order.Sort();
            foreach (var page in scan._order)
            {
                findings.Add(new(PdfUaRequirement.StructureReferences, page));
            }

            // Only a reference to a deleted object leaves no page to name.
            if (scan._missing.Count == 0)
            {
                findings.Add(new(PdfUaRequirement.StructureReferences, PdfUaFinding.DocumentLevel));
            }
        }
        finally
        {
            scan.Finish();
        }
    }

    /// <summary>Removes the dangling kids and deletes the elements they leave empty.</summary>
    /// <param name="document">The document, read after any earlier repair.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return;
        }

        var scan = Start(document, tree, cancellationToken);
        try
        {
            if (!scan.Find())
            {
                return;
            }

            var roots = tree.Roots;
            for (var i = 0; i < roots.Count; i++)
            {
                if (scan._chain.Contains(roots[i]))
                {
                    scan.Prune(roots[i]);
                }
            }

            scan.Write(transaction);
        }
        finally
        {
            scan.Finish();
        }
    }

    /// <summary>Takes the scan the last check left, or makes one, and points it at a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="tree">The structure tree.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <returns>The scan, which the caller gives back with <see cref="Finish"/>.</returns>
    private static PdfUaStructureReferences Start(PdfDocument document, PdfStructureTree tree, CancellationToken cancellationToken)
    {
        var scan = Interlocked.Exchange(ref _spare, null) ?? new();
        scan._document = document;
        scan._tree = tree;
        scan._stm = document.Objects.Names.Intern("Stm"u8);
        scan._cancellationToken = cancellationToken;
        for (var i = 0; i < document.PageCount; i++)
        {
            var id = PdfDocumentPages.GetPage(document, i).Id;
            if (id.IsValid)
            {
                scan._pagesByObject[id.Number] = i;
            }
        }

        return scan;
    }

    /// <summary>Stores an element's new kids, where the element keeps them.</summary>
    /// <param name="transaction">The edit.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="element">The element.</param>
    /// <param name="kids">The kids that stay.</param>
    private static void SetKids(PdfEditTransaction transaction, PdfObjectStore store, PdfStructureElement element, PdfArray kids)
    {
        var raw = element.Dictionary.GetRaw(KnownName.K);
        if (raw.IsReference && StoreReading.Resolve(store, raw).AsArray() is not null)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromArray(kids));
            return;
        }

        var copy = element.Dictionary.Clone();
        if (kids.Count == 0)
        {
            _ = copy.Remove(KnownName.K);
        }
        else
        {
            copy.Set(KnownName.K, kids.Count == 1 ? kids.GetRaw(0) : PdfValue.FromArray(kids));
        }

        transaction.Replace(element.Id, PdfValue.FromDictionary(copy));
    }

    /// <summary>Scans the tree for kids that point at nothing.</summary>
    /// <returns><see langword="true"/> when some kid points at nothing.</returns>
    private bool Find()
    {
        var budget = MaxElements;
        var roots = _tree.Roots;
        for (var i = 0; i < roots.Count; i++)
        {
            Visit(roots[i], ref budget);
        }

        return _affected.Count > 0;
    }

    /// <summary>Forgets the document and keeps the emptied collections for the next scan.</summary>
    private void Finish()
    {
        _document = null!;
        _tree = null!;
        _cancellationToken = default;
        _pagesByObject.Clear();
        _affected.Clear();
        _chain.Clear();
        _newKids.Clear();
        _deleted.Clear();
        _marks.Clear();
        _order.Clear();
        KeepSets(_drawn);
        KeepSets(_missing);
        Volatile.Write(ref _spare, this);
    }

    /// <summary>Empties a map of sets, keeping the small sets to be reused.</summary>
    /// <param name="sets">The map.</param>
    private void KeepSets(Dictionary<int, HashSet<int>> sets)
    {
        foreach (var set in sets.Values)
        {
            if (set.Count > MaxKeptSet || _freeSets.Count > MaxKeptSet)
            {
                continue;
            }

            set.Clear();
            _freeSets.Add(set);
        }

        sets.Clear();
    }

    /// <summary>Takes an empty set from those kept, or makes one.</summary>
    /// <returns>The empty set.</returns>
    private HashSet<int> TakeSet()
    {
        if (_freeSets.Count == 0)
        {
            return [];
        }

        var set = _freeSets[^1];
        _freeSets.RemoveAt(_freeSets.Count - 1);
        return set;
    }

    /// <summary>Visits an element and its descendants, noting the ones with a kid that points at nothing.</summary>
    /// <param name="element">The element.</param>
    /// <param name="budget">How many more elements may be visited.</param>
    private void Visit(PdfStructureElement element, ref int budget)
    {
        if (budget <= 0)
        {
            return;
        }

        budget--;
        _cancellationToken.ThrowIfCancellationRequested();
        if (HasDanglingKid(element))
        {
            _affected.Add(element);
            for (var up = element; up is not null; up = up.Parent)
            {
                _ = _chain.Add(up);
            }
        }

        // An indexed loop, as enumerating the interface list would box its enumerator.
        var kids = element.Kids;
        for (var i = 0; i < kids.Count; i++)
        {
            if (kids[i] is { Kind: PdfStructureKidKind.Element, Element: { } child })
            {
                Visit(child, ref budget);
            }
        }
    }

    /// <summary>Determines whether any kid of an element points at nothing, noting the missing marked content ids.</summary>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private bool HasDanglingKid(PdfStructureElement element)
    {
        var raw = element.Dictionary.GetRaw(KnownName.K);
        if (StoreReading.Resolve(_document.Objects, raw).AsArray() is not { } array)
        {
            return Judge(raw, element.PageIndex, true);
        }

        var found = false;
        for (var i = 0; i < array.Count; i++)
        {
            found |= Judge(array.GetRaw(i), element.PageIndex, true);
        }

        return found;
    }

    /// <summary>Decides whether one kid of an element points at nothing.</summary>
    /// <param name="raw">The kid as the element stores it.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <param name="note">Whether to note the marked content ids that are gone.</param>
    /// <returns><see langword="true"/> when the kid points at nothing.</returns>
    private bool Judge(PdfValue raw, int elementPage, bool note)
    {
        var kid = StoreReading.Resolve(_document.Objects, raw);
        return kid.IsNumber ? IsMissing(elementPage, kid.AsInt32(), note) : kid.AsDictionary() is { } dictionary && JudgeDictionary(dictionary, elementPage, note);
    }

    /// <summary>Decides whether a kid written as a dictionary points at nothing: a marked content reference or an object reference. A child element never does.</summary>
    /// <param name="dictionary">The kid.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <param name="note">Whether to note the marked content ids that are gone.</param>
    /// <returns><see langword="true"/> when the kid points at nothing.</returns>
    private bool JudgeDictionary(PdfDictionary dictionary, int elementPage, bool note) =>
        dictionary.IsName(KnownName.Type, KnownName.MCR)
            ? IsContentMissing(dictionary, elementPage, note)
            : dictionary.IsName(KnownName.Type, KnownName.OBJR) && StoreReading.Resolve(_document.Objects, dictionary.GetRaw(KnownName.Obj)).IsNull;

    /// <summary>Decides whether a marked content reference names an id its page no longer draws. A reference into a form or pattern stream is kept, as such content is not scanned here.</summary>
    /// <param name="reference">The <c>/MCR</c> dictionary.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <param name="note">Whether to note the marked content id when it is gone.</param>
    /// <returns><see langword="true"/> when the page does not draw the id.</returns>
    private bool IsContentMissing(PdfDictionary reference, int elementPage, bool note) =>
        !reference.ContainsKey(_stm) && IsMissing(PageOf(reference.GetRaw(KnownName.Pg), elementPage), reference.GetInt32(KnownName.MCID, -1), note);

    /// <summary>Determines whether a page no longer draws a marked content id.</summary>
    /// <param name="pageIndex">The page, or -1 when it is unknown.</param>
    /// <param name="mcid">The marked content id, or a negative number when there is none.</param>
    /// <param name="note">Whether to note the id when it is gone.</param>
    /// <returns><see langword="true"/> when the page does not draw it.</returns>
    private bool IsMissing(int pageIndex, int mcid, bool note)
    {
        var missing = pageIndex >= 0 && mcid >= 0 && !DrawnOn(pageIndex).Contains(mcid);
        if (missing && note)
        {
            ref var gone = ref CollectionsMarshal.GetValueRefOrAddDefault(_missing, pageIndex, out _);
            gone ??= TakeSet();
            _ = gone.Add(mcid);
        }

        return missing;
    }

    /// <summary>Finds the page a reference names.</summary>
    /// <param name="raw">The <c>/Pg</c> value as stored.</param>
    /// <param name="fallback">The page to use when the value names none.</param>
    /// <returns>The page index, or -1 when the value names a page that is not in the document.</returns>
    private int PageOf(PdfValue raw, int fallback) => raw.IsReference ? _pagesByObject.GetValueOrDefault(raw.AsReference().Number, -1) : fallback;

    /// <summary>Gets the marked content ids a page's content draws.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The ids.</returns>
    private HashSet<int> DrawnOn(int pageIndex)
    {
        if (_drawn.TryGetValue(pageIndex, out var known))
        {
            return known;
        }

        _marks.Clear();
        _ = MarkedContentScanner.Scan(_document, PdfDocumentPages.GetPage(_document, pageIndex), _marks);
        var ids = TakeSet();
        foreach (var mark in _marks)
        {
            if (mark.Mcid >= 0)
            {
                _ = ids.Add(mark.Mcid);
            }
        }

        _drawn[pageIndex] = ids;
        return ids;
    }

    /// <summary>Works out the new kids of an element and its descendants, children first, as an element is deleted when it keeps no kid.</summary>
    /// <param name="element">The element, which is in the chain.</param>
    private void Prune(PdfStructureElement element)
    {
        var kids = element.Kids;
        for (var i = 0; i < kids.Count; i++)
        {
            if (kids[i] is { Kind: PdfStructureKidKind.Element, Element: { } child } && _chain.Contains(child))
            {
                Prune(child);
            }
        }

        if (!element.Id.IsValid)
        {
            return;
        }

        var kept = KeepKids(element, out var removed);
        if (!removed)
        {
            return;
        }

        if (kept.Count == 0 && element.Parent is not null)
        {
            _ = _deleted.Add(element.Id.Number);
            return;
        }

        _newKids[element.Id.Number] = kept;
    }

    /// <summary>Lists the kids of an element that stay.</summary>
    /// <param name="element">The element.</param>
    /// <param name="removed">Whether any kid was left out.</param>
    /// <returns>The kids that stay, as the element stores them.</returns>
    private PdfArray KeepKids(PdfStructureElement element, out bool removed)
    {
        var raw = element.Dictionary.GetRaw(KnownName.K);
        var items = StoreReading.Resolve(_document.Objects, raw).AsArray();
        var count = items?.Count ?? 1;
        var kept = new PdfArray(_document.Objects, count);
        removed = false;
        for (var i = 0; i < count; i++)
        {
            var item = items is null ? raw : items.GetRaw(i);
            if (IsGone(item, element.PageIndex))
            {
                removed = true;
            }
            else if (!item.IsNull)
            {
                kept.Add(item);
            }
        }

        return kept;
    }

    /// <summary>Determines whether a kid goes: it points at nothing, or it is a child element that was deleted.</summary>
    /// <param name="item">The kid as the element stores it.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <returns><see langword="true"/> when the kid is left out.</returns>
    private bool IsGone(PdfValue item, int elementPage) =>
        Judge(item, elementPage, false) || (item.IsReference && _deleted.Contains(item.AsReference().Number));

    /// <summary>Stores the new kids, deletes the empty elements and updates the parent tree.</summary>
    /// <param name="transaction">The edit.</param>
    private void Write(PdfEditTransaction transaction)
    {
        var store = _document.Objects;
        foreach (var element in _chain)
        {
            if (element.Id.IsValid && _newKids.TryGetValue(element.Id.Number, out var kids))
            {
                SetKids(transaction, store, element, kids);
            }
        }

        foreach (var number in _deleted)
        {
            transaction.Delete(new(number, 0));
        }

        if (PdfParentTree.Load(transaction, store) is not { } parents)
        {
            return;
        }

        parents.RemoveElements(_deleted);
        foreach (var (pageIndex, mcids) in _missing)
        {
            var key = PdfDocumentPages.GetPage(_document, pageIndex).Dictionary.GetInt32(KnownName.StructParents, -1);
            if (key >= 0)
            {
                parents.ClearMarkedContent(key, mcids);
            }
        }

        parents.Write();
    }
}
