// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Orders reachable objects into first-page, remaining-page and shared sections. The plan holds pooled memory that
/// <see cref="Dispose"/> returns.
/// </summary>
internal ref struct PdfLinearizationPlan : IDisposable
{
    /// <summary>The number of object slots after the linearization dictionary.</summary>
    private const int HintObjectOffset = 2;

    /// <summary>Each page's objects in output order.</summary>
    private PooledRows _pageGroups;

    /// <summary>The objects shared by later pages but absent from the first page.</summary>
    private PooledInts _sharedOther;

    /// <summary>The objects outside the page groups and shared section.</summary>
    private PooledInts _trailing;

    /// <summary>The outline objects written consecutively before the main xref.</summary>
    private PooledInts _outline;

    /// <summary>The objects written with the catalog for opening the document.</summary>
    private PooledInts _openDocument;

    /// <summary>Each later page's indexes into the shared-object hint table.</summary>
    private PooledRows _sharedIdentifiers;

    /// <summary>The old-to-new object number map used by the value writer, rented from the shared pool.</summary>
    private int[]? _renumbering;

    /// <summary>Initializes a new instance of the <see cref="PdfLinearizationPlan"/> struct.</summary>
    /// <param name="graph">The repaired objects to write.</param>
    private PdfLinearizationPlan(PdfObjectGraph graph)
    {
        Graph = graph;
        _pageGroups = default;
        _sharedOther = default;
        _trailing = default;
        _outline = default;
        _openDocument = default;
        _sharedIdentifiers = default;
        _renumbering = null;
        OutlineWithFirstPage = false;
        CatalogGraphNumber = 0;
        CatalogNumber = 0;
        FirstPageNumber = 0;
    }

    /// <summary>Gets the repaired object graph.</summary>
    internal readonly PdfObjectGraph Graph { get; }

    /// <summary>Gets the number of page groups.</summary>
    internal readonly int PageCount => _pageGroups.RowCount;

    /// <summary>Gets the objects shared by later pages but absent from the first page.</summary>
    internal readonly ReadOnlySpan<int> SharedOther => _sharedOther.Span;

    /// <summary>Gets the objects outside the page groups and shared section.</summary>
    internal readonly ReadOnlySpan<int> Trailing => _trailing.Span;

    /// <summary>Gets the outline objects written consecutively before the main xref.</summary>
    internal readonly ReadOnlySpan<int> Outline => _outline.Span;

    /// <summary>Gets the objects written with the catalog for opening the document.</summary>
    internal readonly ReadOnlySpan<int> OpenDocument => _openDocument.Span;

    /// <summary>Gets a value indicating whether the requested outline pane places outlines with the first page.</summary>
    internal bool OutlineWithFirstPage { get; private set; }

    /// <summary>Gets the catalog's graph number.</summary>
    internal int CatalogGraphNumber { get; private set; }

    /// <summary>Gets the new catalog object number.</summary>
    internal int CatalogNumber { get; private set; }

    /// <summary>Gets the new first-page object number.</summary>
    internal int FirstPageNumber { get; private set; }

    /// <summary>Gets the old-to-new object number map used by the value writer.</summary>
    internal readonly int[] Renumbering => _renumbering!;

    /// <summary>Gets the linearization dictionary's object number.</summary>
    internal readonly int HeaderNumber => Graph.Count + 1;

    /// <summary>Gets the hint stream's object number.</summary>
    internal readonly int HintNumber => Graph.Count + HintObjectOffset;

    /// <summary>Gets the output number of one graph object.</summary>
    /// <param name="graphNumber">The graph object number.</param>
    internal readonly int this[int graphNumber]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _renumbering![Graph.GetOldNumber(graphNumber)];
    }

    /// <summary>Returns the pooled memory.</summary>
    public void Dispose()
    {
        _pageGroups.Dispose();
        _sharedOther.Dispose();
        _trailing.Dispose();
        _outline.Dispose();
        _openDocument.Dispose();
        _sharedIdentifiers.Dispose();
        if (_renumbering is { } renumbering)
        {
            ArrayPool<int>.Shared.Return(renumbering);
        }

        _renumbering = null;
    }

    /// <summary>Builds page groups from the document's reachable objects.</summary>
    /// <param name="document">The document to save.</param>
    /// <param name="cancellationToken">Cancels the graph walk.</param>
    /// <returns>The numbered layout, which the caller disposes.</returns>
    /// <exception cref="PdfException">The document has no indirect page or catalog, or page references exceed the supported depth.</exception>
    internal static PdfLinearizationPlan Create(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.PageCount == 0)
        {
            throw new PdfException(PdfError.Format, "A linearized PDF needs at least one page.");
        }

        var graph = PdfObjectGraph.CollectForSave(document.Objects, document.Objects.Security is not null, false, cancellationToken);
        var plan = new PdfLinearizationPlan(graph);
        var completed = false;
        try
        {
            plan.Fill(document, cancellationToken);
            completed = true;
            return plan;
        }
        finally
        {
            if (!completed)
            {
                plan.Dispose();
            }
        }
    }

    /// <summary>Gets a page's objects in output order.</summary>
    /// <param name="page">The zero based page index.</param>
    /// <returns>The graph numbers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<int> PageGroup(int page) => _pageGroups[page];

    /// <summary>Gets the indexes of the shared objects a later page uses.</summary>
    /// <param name="page">The zero based page index.</param>
    /// <returns>The indexes into the shared-object table; empty for the first page.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly ReadOnlySpan<int> SharedIdentifiers(int page) => _sharedIdentifiers[page];

    /// <summary>Maps an original object number to its graph number.</summary>
    /// <param name="graph">The repaired graph.</param>
    /// <param name="number">The original object number.</param>
    /// <returns>The graph number.</returns>
    /// <exception cref="PdfException">The object does not exist indirectly.</exception>
    private static int GraphNumber(PdfObjectGraph graph, int number)
    {
        if (number < 1 || number >= graph.Map.Length || graph.Map[number] <= 0)
        {
            throw new PdfException(PdfError.Format, "A page or catalog has no indirect object to linearize.");
        }

        return graph.Map[number];
    }

    /// <summary>Includes the resource dictionary inherited through a page's parent chain.</summary>
    /// <param name="page">The page.</param>
    /// <param name="walk">The page's graph walk.</param>
    private static void ScanInheritedResources(PdfPage page, ref PdfLinearizationWalk walk)
    {
        var node = page.Dictionary;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxNesting; depth++)
        {
            var resources = node.GetRaw(KnownName.Resources);
            if (!resources.IsNull)
            {
                walk.Scan(resources);
                return;
            }

            node = node.GetDictionary(KnownName.Parent);
        }
    }

    /// <summary>Counts the pages that use each graph object.</summary>
    /// <param name="count">The graph object count.</param>
    /// <param name="sets">Each page's dependencies.</param>
    /// <param name="uses">Receives the page usage counts by graph number.</param>
    private static void CountUses(int count, PooledRows sets, ref PooledInts uses)
    {
        var counts = uses.AddZeros(count + 1);
        for (var page = 0; page < sets.RowCount; page++)
        {
            foreach (var graphNumber in sets[page])
            {
                counts[graphNumber]++;
            }
        }
    }

    /// <summary>Adds numbers to a set.</summary>
    /// <param name="set">The set.</param>
    /// <param name="numbers">The numbers.</param>
    private static void AddAll(PooledBits set, ReadOnlySpan<int> numbers)
    {
        foreach (var number in numbers)
        {
            _ = set.Add(number);
        }
    }

    /// <summary>Walks the document and places every object in a section.</summary>
    /// <param name="document">The document to save.</param>
    /// <param name="cancellationToken">Cancels the graph walk.</param>
    private void Fill(PdfDocument document, CancellationToken cancellationToken)
    {
        var graph = Graph;
        CatalogGraphNumber = GraphNumber(graph, document.Objects.Trailer.GetRaw(KnownName.Root).AsReference().Number);
        var pages = default(PooledInts);
        var sets = default(PooledRows);
        var uses = default(PooledInts);
        var walk = new PdfLinearizationWalk(graph, cancellationToken);
        try
        {
            for (var index = 0; index < document.PageCount; index++)
            {
                pages.Add(GraphNumber(graph, PdfDocumentPages.GetPage(document, index).Id.Number));
            }

            ExcludeStructure(ref walk, pages.Span);
            CollectCatalogSections(ref walk);
            CollectPageSets(document, ref walk, pages.Span, ref sets);
            CountUses(graph.Count, sets, ref uses);
            CollectPageGroups(pages.Span, sets, uses.Span);
            CollectSharedOther(sets[0], uses.Span);
            CollectTrailing();
            CollectSharedIdentifiers(sets);
            Number();
        }
        finally
        {
            walk.Dispose();
            pages.Dispose();
            sets.Dispose();
            uses.Dispose();
        }
    }

    /// <summary>Leaves the catalog, page-tree nodes and pages out of the page dependency walks.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="pages">The page graph numbers.</param>
    private readonly void ExcludeStructure(ref PdfLinearizationWalk walk, ReadOnlySpan<int> pages)
    {
        walk.Exclude(CatalogGraphNumber);
        walk.Exclude(pages);
        for (var graphNumber = 1; graphNumber <= Graph.Count; graphNumber++)
        {
            if (Graph.GetValue(graphNumber).AsDictionary()?.IsName(KnownName.Type, KnownName.Pages) == true)
            {
                walk.Exclude(graphNumber);
            }
        }
    }

    /// <summary>Finds the objects needed to open the document, and the outlines, which get their own groups.</summary>
    /// <param name="walk">The walk.</param>
    private void CollectCatalogSections(ref PdfLinearizationWalk walk)
    {
        var catalog = Graph.GetValue(CatalogGraphNumber).AsDictionary()!;
        walk.Begin(true);
        walk.Scan(catalog.GetRaw(KnownName.AcroForm));
        walk.Scan(catalog.GetRaw(KnownName.ViewerPreferences));
        walk.Scan(catalog.GetRaw(KnownName.Threads));
        walk.Scan(catalog.GetRaw(KnownName.OpenAction));
        walk.Reached.CopyTo(ref _openDocument);
        walk.Exclude(_openDocument.Span);

        walk.Begin(true);
        walk.Scan(catalog.GetRaw(KnownName.Outlines));
        walk.Reached.CopyTo(ref _outline);
        OutlineWithFirstPage = catalog.GetName(KnownName.PageMode) == Graph.Names.Intern("UseOutlines"u8);
        walk.Exclude(_outline.Span);
    }

    /// <summary>Finds the indirect objects each page needs without following page-tree and back references.</summary>
    /// <param name="document">The document.</param>
    /// <param name="walk">The walk.</param>
    /// <param name="pages">The page graph numbers.</param>
    /// <param name="sets">Receives one reachable row per page, smallest first.</param>
    private readonly void CollectPageSets(PdfDocument document, ref PdfLinearizationWalk walk, ReadOnlySpan<int> pages, ref PooledRows sets)
    {
        var reached = default(PooledInts);
        try
        {
            for (var index = 0; index < pages.Length; index++)
            {
                walk.Begin(false, pages[index]);
                walk.Scan(Graph.GetValue(pages[index]));
                ScanInheritedResources(PdfDocumentPages.GetPage(document, index), ref walk);
                reached.Clear();
                walk.Reached.CopyTo(ref reached);
                foreach (var graphNumber in reached.Span)
                {
                    sets.Add(graphNumber);
                }

                sets.EndRow();
            }
        }
        finally
        {
            reached.Dispose();
        }
    }

    /// <summary>Places each page first in its group, followed by its private dependencies.</summary>
    /// <param name="pages">The page graph numbers.</param>
    /// <param name="sets">The page dependency sets.</param>
    /// <param name="uses">The page usage counts.</param>
    private void CollectPageGroups(ReadOnlySpan<int> pages, PooledRows sets, ReadOnlySpan<int> uses)
    {
        for (var index = 0; index < pages.Length; index++)
        {
            _pageGroups.Add(pages[index]);
            foreach (var number in sets[index])
            {
                if (number != pages[index] && (index == 0 || uses[number] == 1))
                {
                    _pageGroups.Add(number);
                }
            }

            if (index == 0 && OutlineWithFirstPage)
            {
                foreach (var number in _outline.Span)
                {
                    _pageGroups.Add(number);
                }
            }

            _pageGroups.EndRow();
        }
    }

    /// <summary>Gets dependencies used by multiple later pages and absent from page one.</summary>
    /// <param name="first">The first page's dependency row, smallest first.</param>
    /// <param name="uses">The page usage counts.</param>
    private void CollectSharedOther(ReadOnlySpan<int> first, ReadOnlySpan<int> uses)
    {
        for (var number = 1; number <= Graph.Count; number++)
        {
            if (uses[number] > 1 && first.BinarySearch(number) < 0)
            {
                _sharedOther.Add(number);
            }
        }
    }

    /// <summary>Gets objects not assigned to page groups, shared objects, the outline or the catalog.</summary>
    private void CollectTrailing()
    {
        var assigned = default(PooledBits);
        assigned.Rent(Graph.Count + 1);
        try
        {
            _ = assigned.Add(CatalogGraphNumber);
            for (var page = 0; page < _pageGroups.RowCount; page++)
            {
                AddAll(assigned, _pageGroups[page]);
            }

            AddAll(assigned, _sharedOther.Span);
            AddAll(assigned, _outline.Span);
            AddAll(assigned, _openDocument.Span);
            for (var number = 1; number <= Graph.Count; number++)
            {
                if (!assigned.Contains(number))
                {
                    _trailing.Add(number);
                }
            }
        }
        finally
        {
            assigned.Dispose();
        }
    }

    /// <summary>Indexes page dependencies into the first-page and later shared-object sections.</summary>
    /// <param name="sets">Each page's dependencies.</param>
    private void CollectSharedIdentifiers(PooledRows sets)
    {
        var shared = default(PooledInts);
        try
        {
            foreach (var number in _pageGroups[0])
            {
                shared.Add(number);
            }

            foreach (var number in _sharedOther.Span)
            {
                shared.Add(number);
            }

            _sharedIdentifiers.EndRow();
            for (var page = 1; page < sets.RowCount; page++)
            {
                var row = sets[page];
                for (var index = 0; index < shared.Count; index++)
                {
                    if (row.BinarySearch(shared[index]) >= 0)
                    {
                        _sharedIdentifiers.Add(index);
                    }
                }

                _sharedIdentifiers.EndRow();
            }
        }
        finally
        {
            shared.Dispose();
        }
    }

    /// <summary>Gives every object its output number: later pages, shared objects, the catalog section, then the first page.</summary>
    /// <exception cref="PdfException">The object groups do not cover the document.</exception>
    private void Number()
    {
        var map = Graph.Map;
        var renumbering = ArrayPool<int>.Shared.Rent(map.Length);
        map.CopyTo(renumbering, 0);

        // The map's own length bounds the old numbers the writer renumbers; a longer pooled array reads as unmapped beyond it.
        renumbering.AsSpan(map.Length).Clear();
        _renumbering = renumbering;
        var next = 1;
        for (var index = 1; index < _pageGroups.RowCount; index++)
        {
            Assign(_pageGroups[index], ref next);
        }

        Assign(_sharedOther.Span, ref next);
        Assign(_trailing.Span, ref next);
        if (!OutlineWithFirstPage)
        {
            Assign(_outline.Span, ref next);
        }

        CatalogNumber = next;
        renumbering[Graph.GetOldNumber(CatalogGraphNumber)] = next;
        next++;
        Assign(_openDocument.Span, ref next);
        FirstPageNumber = next;
        Assign(_pageGroups[0], ref next);
        if (next != Graph.Count + 1)
        {
            throw new PdfException(PdfError.Format, "The linearized object groups are incomplete.");
        }
    }

    /// <summary>Assigns output numbers to one ordered group.</summary>
    /// <param name="group">The graph numbers.</param>
    /// <param name="next">The next output number.</param>
    private readonly void Assign(ReadOnlySpan<int> group, ref int next)
    {
        foreach (var graphNumber in group)
        {
            _renumbering![Graph.GetOldNumber(graphNumber)] = next;
            next++;
        }
    }
}
