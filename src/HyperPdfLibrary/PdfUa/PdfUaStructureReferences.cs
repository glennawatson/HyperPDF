// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.PdfUa;

/// <summary>Checks and repairs references to content missing from a document's structure tree.</summary>
internal static class PdfUaStructureReferences
{
    /// <summary>The most elements visited.</summary>
    private const int MaxElements = StructureTreeBuilder.MaxElements;

    /// <summary>The most elements, marked content ids or pages a kept set may hold before it is dropped instead of kept for the next scan.</summary>
    private const int MaxKeptSet = 1 << 12;

    /// <summary>Adds a finding for each page whose tags point at marked content the page no longer draws, and one for the whole document for a missing object.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfUaFinding> findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return;
        }

        var scan = Start(document, tree, cancellationToken);
        try
        {
            if (!Find(scan))
            {
                return;
            }

            scan.Buffers.Order.AddRange(scan.Buffers.Missing.Keys);
            scan.Buffers.Order.Sort();
            foreach (var page in scan.Buffers.Order)
            {
                findings.Add(new(PdfUaRequirement.StructureReferences, page));
            }

            // Only a reference to a deleted object leaves no page to name.
            if (scan.Buffers.Missing.Count == 0)
            {
                findings.Add(new(PdfUaRequirement.StructureReferences, PdfUaFinding.DocumentLevel));
            }
        }
        finally
        {
            Finish(scan);
        }
    }

    /// <summary>Removes the dangling kids and deletes the elements they leave empty.</summary>
    /// <param name="document">The document, read after any earlier repair.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="cancellationToken">Stops the repair.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (PdfDocumentTagged.GetStructureTree(document) is not { } tree)
        {
            return;
        }

        var scan = Start(document, tree, cancellationToken);
        try
        {
            if (!Find(scan))
            {
                return;
            }

            var roots = tree.Roots;
            for (var i = 0; i < roots.Count; i++)
            {
                if (scan.Buffers.Chain.Contains(roots[i]))
                {
                    Prune(scan, roots[i]);
                }
            }

            Write(scan, transaction);
        }
        finally
        {
            Finish(scan);
        }
    }

    /// <summary>Takes the scan the last check left, or makes one, and points it at a document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="tree">The structure tree.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <returns>The scan, which the caller gives back with <see cref="Finish"/>.</returns>
    private static PdfUaStructureReferenceScan Start(PdfDocument document, PdfStructureTree tree, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var buffers = Interlocked.Exchange(ref document.State.StructureReferences, null) ?? CreateBuffers();
        var scan = new PdfUaStructureReferenceScan(document, tree, document.Objects.Names.Intern("Stm"u8), buffers, cancellationToken);
        try
        {
            for (var i = 0; i < document.PageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = PdfDocumentPages.GetPage(document, i).Id;
                if (id.IsValid)
                {
                    buffers.PagesByObject[id.Number] = i;
                }
            }

            return scan;
        }
        catch
        {
            Finish(scan);
            throw;
        }
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
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <returns><see langword="true"/> when some kid points at nothing.</returns>
    private static bool Find(in PdfUaStructureReferenceScan scan)
    {
        var budget = MaxElements;
        var roots = scan.Tree.Roots;
        for (var i = 0; i < roots.Count; i++)
        {
            Visit(scan, roots[i], ref budget);
        }

        return scan.Buffers.Chain.Count > 0;
    }

    /// <summary>Forgets the document and keeps the emptied collections for the next scan.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    private static void Finish(in PdfUaStructureReferenceScan scan)
    {
        var buffers = scan.Buffers;
        buffers.PagesByObject.Clear();
        buffers.Chain.Clear();
        buffers.NewKids.Clear();
        buffers.Deleted.Clear();
        buffers.Marks.Clear();
        buffers.Order.Clear();
        KeepSets(buffers.FreeSets, buffers.Drawn);
        KeepSets(buffers.FreeSets, buffers.Missing);
        var document = scan.Document;
        if (document.IsDisposed)
        {
            return;
        }

        _ = Interlocked.CompareExchange(ref document.State.StructureReferences, buffers, null);
        if (document.IsDisposed)
        {
            _ = Interlocked.CompareExchange(ref document.State.StructureReferences, null, buffers);
        }
    }

    /// <summary>Empties a map of sets, keeping the small sets to be reused.</summary>
    /// <param name="freeSets">The emptied sets available for reuse.</param>
    /// <param name="sets">The map.</param>
    private static void KeepSets(List<HashSet<int>> freeSets, Dictionary<int, HashSet<int>> sets)
    {
        foreach (var set in sets.Values)
        {
            if (set.Count > MaxKeptSet || freeSets.Count >= MaxKeptSet)
            {
                continue;
            }

            set.Clear();
            freeSets.Add(set);
        }

        sets.Clear();
    }

    /// <summary>Takes an empty set from those kept, or makes one.</summary>
    /// <param name="freeSets">The emptied sets available for reuse.</param>
    /// <returns>The empty set.</returns>
    private static HashSet<int> TakeSet(List<HashSet<int>> freeSets)
    {
        if (freeSets.Count == 0)
        {
            return [];
        }

        var set = freeSets[^1];
        freeSets.RemoveAt(freeSets.Count - 1);
        return set;
    }

    /// <summary>Visits an element and its descendants, noting the ones with a kid that points at nothing.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="element">The element.</param>
    /// <param name="budget">How many more elements may be visited.</param>
    private static void Visit(in PdfUaStructureReferenceScan scan, PdfStructureElement element, ref int budget)
    {
        if (budget <= 0)
        {
            return;
        }

        budget--;
        scan.CancellationToken.ThrowIfCancellationRequested();
        if (HasDanglingKid(scan, element))
        {
            for (var up = element; up is not null; up = up.Parent)
            {
                _ = scan.Buffers.Chain.Add(up);
            }
        }

        // An indexed loop, as enumerating the interface list would box its enumerator.
        var kids = element.Kids;
        for (var i = 0; i < kids.Count; i++)
        {
            if (kids[i] is { Kind: PdfStructureKidKind.Element, Element: { } child })
            {
                Visit(scan, child, ref budget);
            }
        }
    }

    /// <summary>Determines whether any kid of an element points at nothing, noting the missing marked content ids.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="element">The element.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool HasDanglingKid(in PdfUaStructureReferenceScan scan, PdfStructureElement element)
    {
        var raw = element.Dictionary.GetRaw(KnownName.K);
        if (StoreReading.Resolve(scan.Document.Objects, raw).AsArray() is not { } array)
        {
            return Judge(scan, raw, element.PageIndex, true);
        }

        var found = false;
        for (var i = 0; i < array.Count; i++)
        {
            found |= Judge(scan, array.GetRaw(i), element.PageIndex, true);
        }

        return found;
    }

    /// <summary>Decides whether one kid of an element points at nothing.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="raw">The kid as the element stores it.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <param name="note">Whether to note the marked content ids that are gone.</param>
    /// <returns><see langword="true"/> when the kid points at nothing.</returns>
    private static bool Judge(in PdfUaStructureReferenceScan scan, PdfValue raw, int elementPage, bool note)
    {
        var kid = StoreReading.Resolve(scan.Document.Objects, raw);
        return kid.IsNumber ? IsMissing(scan, elementPage, kid.AsInt32(), note) : kid.AsDictionary() is { } dictionary && JudgeDictionary(scan, dictionary, elementPage, note);
    }

    /// <summary>Decides whether a kid written as a dictionary points at nothing: a marked content reference or an object reference. A child element never does.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="dictionary">The kid.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <param name="note">Whether to note the marked content ids that are gone.</param>
    /// <returns><see langword="true"/> when the kid points at nothing.</returns>
    private static bool JudgeDictionary(in PdfUaStructureReferenceScan scan, PdfDictionary dictionary, int elementPage, bool note) =>
        dictionary.IsName(KnownName.Type, KnownName.MCR)
            ? IsContentMissing(scan, dictionary, elementPage, note)
            : dictionary.IsName(KnownName.Type, KnownName.OBJR) && StoreReading.Resolve(scan.Document.Objects, dictionary.GetRaw(KnownName.Obj)).IsNull;

    /// <summary>Decides whether a marked content reference names an id its page no longer draws. A reference into a form or pattern stream is kept, as such content is not scanned here.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="reference">The <c>/MCR</c> dictionary.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <param name="note">Whether to note the marked content id when it is gone.</param>
    /// <returns><see langword="true"/> when the page does not draw the id.</returns>
    private static bool IsContentMissing(in PdfUaStructureReferenceScan scan, PdfDictionary reference, int elementPage, bool note) =>
        !reference.ContainsKey(scan.Stm) && IsMissing(scan, PageOf(scan, reference.GetRaw(KnownName.Pg), elementPage), reference.GetInt32(KnownName.MCID, -1), note);

    /// <summary>Determines whether a page no longer draws a marked content id.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="pageIndex">The page, or -1 when it is unknown.</param>
    /// <param name="mcid">The marked content id, or a negative number when there is none.</param>
    /// <param name="note">Whether to note the id when it is gone.</param>
    /// <returns><see langword="true"/> when the page does not draw it.</returns>
    private static bool IsMissing(in PdfUaStructureReferenceScan scan, int pageIndex, int mcid, bool note)
    {
        var missing = pageIndex >= 0 && mcid >= 0 && !DrawnOn(scan, pageIndex).Contains(mcid);
        if (missing && note)
        {
            ref var gone = ref CollectionsMarshal.GetValueRefOrAddDefault(scan.Buffers.Missing, pageIndex, out _);
            gone ??= TakeSet(scan.Buffers.FreeSets);
            _ = gone.Add(mcid);
        }

        return missing;
    }

    /// <summary>Finds the page a reference names.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="raw">The <c>/Pg</c> value as stored.</param>
    /// <param name="fallback">The page to use when the value names none.</param>
    /// <returns>The page index, or -1 when the value names a page that is not in the document.</returns>
    private static int PageOf(in PdfUaStructureReferenceScan scan, PdfValue raw, int fallback) =>
        raw.IsReference ? scan.Buffers.PagesByObject.GetValueOrDefault(raw.AsReference().Number, -1) : fallback;

    /// <summary>Gets the marked content ids a page's content draws.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The ids.</returns>
    private static HashSet<int> DrawnOn(in PdfUaStructureReferenceScan scan, int pageIndex)
    {
        if (scan.Buffers.Drawn.TryGetValue(pageIndex, out var known))
        {
            return known;
        }

        scan.Buffers.Marks.Clear();
        _ = MarkedContentScanner.Scan(scan.Document, PdfDocumentPages.GetPage(scan.Document, pageIndex), scan.Buffers.Marks);
        var ids = TakeSet(scan.Buffers.FreeSets);
        foreach (var mark in scan.Buffers.Marks)
        {
            if (mark.Mcid >= 0)
            {
                _ = ids.Add(mark.Mcid);
            }
        }

        scan.Buffers.Drawn[pageIndex] = ids;
        return ids;
    }

    /// <summary>Works out the new kids of an element and its descendants, children first, as an element is deleted when it keeps no kid.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="element">The element, which is in the chain.</param>
    private static void Prune(in PdfUaStructureReferenceScan scan, PdfStructureElement element)
    {
        var kids = element.Kids;
        for (var i = 0; i < kids.Count; i++)
        {
            if (kids[i] is { Kind: PdfStructureKidKind.Element, Element: { } child } && scan.Buffers.Chain.Contains(child))
            {
                Prune(scan, child);
            }
        }

        if (!element.Id.IsValid)
        {
            return;
        }

        var kept = KeepKids(scan, element, out var removed);
        if (!removed)
        {
            return;
        }

        if (kept.Count == 0 && element.Parent is not null)
        {
            _ = scan.Buffers.Deleted.Add(element.Id.Number);
            return;
        }

        scan.Buffers.NewKids[element.Id.Number] = kept;
    }

    /// <summary>Lists the kids of an element that stay.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="element">The element.</param>
    /// <param name="removed">Whether any kid was left out.</param>
    /// <returns>The kids that stay, as the element stores them.</returns>
    private static PdfArray KeepKids(in PdfUaStructureReferenceScan scan, PdfStructureElement element, out bool removed)
    {
        var raw = element.Dictionary.GetRaw(KnownName.K);
        var items = StoreReading.Resolve(scan.Document.Objects, raw).AsArray();
        var count = items?.Count ?? 1;
        var kept = new PdfArray(scan.Document.Objects, count);
        removed = false;
        for (var i = 0; i < count; i++)
        {
            var item = items is null ? raw : items.GetRaw(i);
            if (IsGone(scan, item, element.PageIndex))
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
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="item">The kid as the element stores it.</param>
    /// <param name="elementPage">The page the element's content is on, or -1.</param>
    /// <returns><see langword="true"/> when the kid is left out.</returns>
    private static bool IsGone(in PdfUaStructureReferenceScan scan, PdfValue item, int elementPage) =>
        Judge(scan, item, elementPage, false) || (item.IsReference && scan.Buffers.Deleted.Contains(item.AsReference().Number));

    /// <summary>Stores the new kids, deletes the empty elements and updates the parent tree.</summary>
    /// <param name="scan">The operation context and leased working collections.</param>
    /// <param name="transaction">The edit.</param>
    private static void Write(in PdfUaStructureReferenceScan scan, PdfEditTransaction transaction)
    {
        var store = scan.Document.Objects;
        foreach (var element in scan.Buffers.Chain)
        {
            if (element.Id.IsValid && scan.Buffers.NewKids.TryGetValue(element.Id.Number, out var kids))
            {
                SetKids(transaction, store, element, kids);
            }
        }

        foreach (var number in scan.Buffers.Deleted)
        {
            transaction.Delete(new(number, 0));
        }

        if (PdfParentTree.Load(transaction, store) is not { } parents)
        {
            return;
        }

        parents.RemoveElements(scan.Buffers.Deleted);
        foreach (var (pageIndex, mcids) in scan.Buffers.Missing)
        {
            var key = PdfDocumentPages.GetPage(scan.Document, pageIndex).Dictionary.GetInt32(KnownName.StructParents, -1);
            if (key >= 0)
            {
                parents.ClearMarkedContent(key, mcids);
            }
        }

        parents.Write();
    }

    /// <summary>Creates collections that one document reuses between reference scans.</summary>
    /// <returns>The empty working storage.</returns>
    private static PdfUaStructureReferenceBuffers CreateBuffers() => new([], [], [], [with(ReferenceEqualityComparer.Instance)], [], [], [], [], []);
}
