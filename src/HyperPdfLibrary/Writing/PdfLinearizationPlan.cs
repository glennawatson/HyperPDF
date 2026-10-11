// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>Orders reachable objects into first-page, remaining-page and shared sections.</summary>
internal sealed class PdfLinearizationPlan
{
    /// <summary>The number of object slots after the linearization dictionary.</summary>
    private const int HintObjectOffset = 2;

    /// <summary>Initializes a new instance of the <see cref="PdfLinearizationPlan"/> class.</summary>
    /// <param name="graph">The repaired objects to write.</param>
    /// <param name="sections">The page, shared, outline and catalog groups.</param>
    /// <param name="outlineWithFirstPage">Whether the outline pane is requested on open.</param>
    /// <exception cref="PdfException">The object groups do not cover the document.</exception>
    private PdfLinearizationPlan(PdfObjectGraph graph, PlanSections sections, bool outlineWithFirstPage)
    {
        var (pageGroups, sharedOther, trailing, outline, catalogGraphNumber, openDocument, sharedIdentifiers) = sections;
        Graph = graph;
        PageGroups = pageGroups;
        SharedOther = sharedOther;
        Trailing = trailing;
        Outline = outline;
        CatalogGraphNumber = catalogGraphNumber;
        OpenDocument = openDocument;
        OutlineWithFirstPage = outlineWithFirstPage;
        SharedIdentifiers = sharedIdentifiers;
        Renumbering = (int[])graph.Map.Clone();
        var next = 1;
        for (var index = 1; index < pageGroups.Length; index++)
        {
            Assign(pageGroups[index], ref next);
        }

        Assign(sharedOther, ref next);
        Assign(trailing, ref next);
        if (!outlineWithFirstPage)
        {
            Assign(outline, ref next);
        }

        CatalogNumber = next;
        Assign([catalogGraphNumber], ref next);
        Assign(openDocument, ref next);
        FirstPageNumber = next;
        Assign(pageGroups[0], ref next);
        if (next != graph.Count + 1)
        {
            throw new PdfException(PdfError.Format, "The linearized object groups are incomplete.");
        }
    }

    /// <summary>Gets the repaired object graph.</summary>
    internal PdfObjectGraph Graph { get; }

    /// <summary>Gets the old-to-new object number map used by the value writer.</summary>
    internal int[] Renumbering { get; }

    /// <summary>Gets each page's objects in output order.</summary>
    internal int[][] PageGroups { get; }

    /// <summary>Gets the objects shared by later pages but absent from the first page.</summary>
    internal int[] SharedOther { get; }

    /// <summary>Gets the objects outside the page groups and shared section.</summary>
    internal int[] Trailing { get; }

    /// <summary>Gets the outline objects written consecutively before the main xref.</summary>
    internal int[] Outline { get; }

    /// <summary>Gets the catalog's graph number.</summary>
    internal int CatalogGraphNumber { get; }

    /// <summary>Gets the objects written with the catalog for opening the document.</summary>
    internal int[] OpenDocument { get; }

    /// <summary>Gets whether the requested outline pane places outlines with the first page.</summary>
    internal bool OutlineWithFirstPage { get; }

    /// <summary>Gets each later page's indexes into the shared-object hint table.</summary>
    internal int[][] SharedIdentifiers { get; }

    /// <summary>Gets the new catalog object number.</summary>
    internal int CatalogNumber { get; }

    /// <summary>Gets the new first-page object number.</summary>
    internal int FirstPageNumber { get; }

    /// <summary>Gets the linearization dictionary's object number.</summary>
    internal int HeaderNumber => Graph.Count + 1;

    /// <summary>Gets the hint stream's object number.</summary>
    internal int HintNumber => Graph.Count + HintObjectOffset;

    /// <summary>Gets the output number of one graph object.</summary>
    /// <param name="graphNumber">The graph object number.</param>
    internal int this[int graphNumber]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Renumbering[Graph.GetOldNumber(graphNumber)];
    }

    /// <summary>Builds page groups from the document's reachable objects.</summary>
    /// <param name="document">The document to save.</param>
    /// <param name="cancellationToken">Cancels the graph walk.</param>
    /// <returns>The numbered layout.</returns>
    /// <exception cref="PdfException">The document has no indirect page or catalog, or page references exceed the supported depth.</exception>
    internal static PdfLinearizationPlan Create(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.PageCount == 0)
        {
            throw new PdfException(PdfError.Format, "A linearized PDF needs at least one page.");
        }

        var store = document.Objects;
        var graph = PdfObjectGraph.Collect(store, store.Security is not null, cancellationToken);
        var catalog = store.Trailer.GetRaw(KnownName.Root).AsReference();
        var catalogGraphNumber = GraphNumber(graph, catalog.Number);
        var pageNumbers = GetPageNumbers(document, graph);
        var excluded = ExcludedObjects(graph, pageNumbers, catalogGraphNumber);
        var openDocument = GetOpenDocumentObjects(graph, catalogGraphNumber, excluded, cancellationToken);
        Exclude(excluded, openDocument);
        var outline = GetOutlineObjects(graph, catalogGraphNumber, excluded, cancellationToken);
        var catalogDictionary = graph.GetValue(catalogGraphNumber).AsDictionary()!;
        var outlineWithFirstPage = catalogDictionary.GetName(KnownName.PageMode) == graph.Names.Intern("UseOutlines"u8);
        Exclude(excluded, outline);
        var pageSets = CollectPageSets(document, graph, pageNumbers, excluded, cancellationToken);
        var uses = CountUses(graph.Count, pageSets);
        var pageGroups = BuildPageGroups(graph.Count, pageNumbers, pageSets, uses);
        if (outlineWithFirstPage)
        {
            pageGroups[0] = [.. pageGroups[0], .. outline];
        }

        var sharedOther = GetSharedOther(graph.Count, pageSets[0], uses);
        var trailing = GetTrailing(graph.Count, pageGroups, sharedOther, outline, catalogGraphNumber, openDocument);
        var identifiers = GetSharedIdentifiers(pageSets, pageGroups[0], sharedOther);
        return new(graph, new(pageGroups, sharedOther, trailing, outline, catalogGraphNumber, openDocument, identifiers), outlineWithFirstPage);
    }

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

    /// <summary>Gets the graph number of each page dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="graph">The repaired graph.</param>
    /// <returns>Page object numbers in reading order.</returns>
    private static int[] GetPageNumbers(PdfDocument document, PdfObjectGraph graph)
    {
        var numbers = new int[document.PageCount];
        for (var index = 0; index < numbers.Length; index++)
        {
            numbers[index] = GraphNumber(graph, PdfDocumentPages.GetPage(document, index).Id.Number);
        }

        return numbers;
    }

    /// <summary>Marks catalog, page-tree nodes and page dictionaries outside each page's private walk.</summary>
    /// <param name="graph">The repaired graph.</param>
    /// <param name="pages">The page graph numbers.</param>
    /// <param name="catalog">The catalog graph number.</param>
    /// <returns>Objects excluded from page dependency walks.</returns>
    private static bool[] ExcludedObjects(PdfObjectGraph graph, int[] pages, int catalog)
    {
        var excluded = new bool[graph.Count + 1];
        excluded[catalog] = true;
        foreach (var page in pages)
        {
            excluded[page] = true;
        }

        for (var graphNumber = 1; graphNumber <= graph.Count; graphNumber++)
        {
            if (graph.GetValue(graphNumber).AsDictionary()?.IsName(KnownName.Type, KnownName.Pages) == true)
            {
                excluded[graphNumber] = true;
            }
        }

        return excluded;
    }

    /// <summary>Finds form fields and other objects needed to open the document before page data.</summary>
    /// <param name="graph">The repaired graph.</param>
    /// <param name="catalogNumber">The catalog's graph number.</param>
    /// <param name="excluded">Catalog and page-tree objects.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The opening objects in graph order.</returns>
    private static int[] GetOpenDocumentObjects(PdfObjectGraph graph, int catalogNumber, bool[] excluded, CancellationToken cancellationToken)
    {
        var catalog = graph.GetValue(catalogNumber).AsDictionary()!;
        var set = new HashSet<int>();
        var walk = new WalkState(graph, excluded, set, true, cancellationToken);
        Scan(catalog.GetRaw(KnownName.AcroForm), walk, 0);
        Scan(catalog.GetRaw(KnownName.ViewerPreferences), walk, 0);
        Scan(catalog.GetRaw(KnownName.Threads), walk, 0);
        Scan(catalog.GetRaw(KnownName.OpenAction), walk, 0);
        return Ordered(graph.Count, set);
    }

    /// <summary>Finds outlines for their own contiguous hint group.</summary>
    /// <param name="graph">The repaired graph.</param>
    /// <param name="catalogNumber">The catalog's graph number.</param>
    /// <param name="excluded">Objects already assigned to open or page-tree sections.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The outline objects in graph order.</returns>
    private static int[] GetOutlineObjects(PdfObjectGraph graph, int catalogNumber, bool[] excluded, CancellationToken cancellationToken)
    {
        var catalog = graph.GetValue(catalogNumber).AsDictionary()!;
        var set = new HashSet<int>();
        var walk = new WalkState(graph, excluded, set, true, cancellationToken);
        Scan(catalog.GetRaw(KnownName.Outlines), walk, 0);
        return Ordered(graph.Count, set);
    }

    /// <summary>Excludes assigned objects from later page dependency walks.</summary>
    /// <param name="excluded">The exclusion map.</param>
    /// <param name="numbers">The assigned graph numbers.</param>
    private static void Exclude(bool[] excluded, int[] numbers)
    {
        foreach (var number in numbers)
        {
            excluded[number] = true;
        }
    }

    /// <summary>Sorts a reachability set into graph number order.</summary>
    /// <param name="count">The graph's object count.</param>
    /// <param name="set">The reachable objects.</param>
    /// <returns>The ordered graph numbers.</returns>
    private static int[] Ordered(int count, HashSet<int> set)
    {
        var ordered = new List<int>(set.Count);
        for (var number = 1; number <= count; number++)
        {
            if (set.Contains(number))
            {
                ordered.Add(number);
            }
        }

        return [.. ordered];
    }

    /// <summary>Finds the indirect objects each page needs without following page-tree and back references.</summary>
    /// <param name="document">The document.</param>
    /// <param name="graph">The repaired graph.</param>
    /// <param name="pages">The page graph numbers.</param>
    /// <param name="excluded">Catalog, page-tree and page objects.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>One reachable set per page.</returns>
    private static HashSet<int>[] CollectPageSets(PdfDocument document, PdfObjectGraph graph, int[] pages, bool[] excluded, CancellationToken cancellationToken)
    {
        var sets = new HashSet<int>[pages.Length];
        for (var index = 0; index < pages.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index);
            var set = new HashSet<int> { pages[index] };
            var walk = new WalkState(graph, excluded, set, false, cancellationToken);
            Scan(graph.GetValue(pages[index]), walk, 0);
            ScanInheritedResources(page, walk);
            sets[index] = set;
        }

        return sets;
    }

    /// <summary>Includes the resource dictionary inherited through a page's parent chain.</summary>
    /// <param name="page">The page.</param>
    /// <param name="walk">The page's graph walk.</param>
    private static void ScanInheritedResources(PdfPage page, WalkState walk)
    {
        var node = page.Dictionary;
        for (var depth = 0; node is not null && depth < PdfLimits.MaxNesting; depth++)
        {
            var resources = node.GetRaw(KnownName.Resources);
            if (!resources.IsNull)
            {
                Scan(resources, walk, depth);
                return;
            }

            node = node.GetDictionary(KnownName.Parent);
        }
    }

    /// <summary>Collects indirect references in one page value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="walk">The graph walk.</param>
    /// <param name="depth">The reference nesting depth.</param>
    /// <exception cref="PdfException">References exceed the permitted nesting depth.</exception>
    private static void Scan(PdfValue value, WalkState walk, int depth)
    {
        if (depth >= PdfLimits.MaxNesting)
        {
            throw new PdfException(PdfError.Format, "The page references are too deep to linearize.");
        }

        switch (value.Kind)
        {
            case PdfKind.Reference:
                {
                    ScanReference(value.AsReference().Number, walk, depth);
                    break;
                }

            case PdfKind.Array:
                {
                    ScanArray(value.AsArray()!, walk, depth);
                    break;
                }

            case PdfKind.Dictionary or PdfKind.Stream:
                {
                    ScanDictionary(value.AsDictionary()!, value.Kind == PdfKind.Stream, walk, depth);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Follows one indirect page dependency when it is not a page-tree or back reference.</summary>
    /// <param name="number">The old object number.</param>
    /// <param name="walk">The graph walk.</param>
    /// <param name="depth">The reference depth.</param>
    private static void ScanReference(int number, WalkState walk, int depth)
    {
        if (number < 1 || number >= walk.Graph.Map.Length)
        {
            return;
        }

        var graphNumber = walk.Graph.Map[number];
        if (graphNumber < 1 || walk.Excluded[graphNumber] || !walk.Set.Add(graphNumber))
        {
            return;
        }

        walk.CancellationToken.ThrowIfCancellationRequested();
        Scan(walk.Graph.GetValue(graphNumber), walk, depth + 1);
    }

    /// <summary>Visits the values of a direct PDF array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="walk">The graph walk.</param>
    /// <param name="depth">The reference depth.</param>
    private static void ScanArray(PdfArray array, WalkState walk, int depth)
    {
        foreach (var item in array.Items)
        {
            Scan(item, walk, depth + 1);
        }
    }

    /// <summary>Visits dictionary entries other than page parents, form children and stream lengths.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="isStream">Whether the writer replaces its length.</param>
    /// <param name="walk">The graph walk.</param>
    /// <param name="depth">The reference depth.</param>
    private static void ScanDictionary(PdfDictionary dictionary, bool isStream, WalkState walk, int depth)
    {
        for (var index = 0; index < dictionary.Count; index++)
        {
            var key = dictionary.GetKeyAt(index);
            if ((!walk.IncludeFormTree && (key.Is(KnownName.Parent) || key.Is(KnownName.Kids))) || (isStream && key.Is(KnownName.Length)))
            {
                continue;
            }

            Scan(dictionary.GetValueAt(index), walk, depth + 1);
        }
    }

    /// <summary>Counts the pages that use each graph object.</summary>
    /// <param name="count">The graph object count.</param>
    /// <param name="sets">Each page's dependencies.</param>
    /// <returns>The page usage counts by graph number.</returns>
    private static int[] CountUses(int count, HashSet<int>[] sets)
    {
        var uses = new int[count + 1];
        foreach (var set in sets)
        {
            foreach (var graphNumber in set)
            {
                uses[graphNumber]++;
            }
        }

        return uses;
    }

    /// <summary>Places each page first in its group, followed by its private dependencies.</summary>
    /// <param name="count">The graph object count.</param>
    /// <param name="pages">The page graph numbers.</param>
    /// <param name="sets">The page dependency sets.</param>
    /// <param name="uses">The page usage counts.</param>
    /// <returns>The ordered page groups.</returns>
    private static int[][] BuildPageGroups(int count, int[] pages, HashSet<int>[] sets, int[] uses)
    {
        var groups = new int[pages.Length][];
        for (var index = 0; index < pages.Length; index++)
        {
            var group = new List<int> { pages[index] };
            for (var number = 1; number <= count; number++)
            {
                if (number != pages[index] && sets[index].Contains(number) && (index == 0 || uses[number] == 1))
                {
                    group.Add(number);
                }
            }

            groups[index] = [.. group];
        }

        return groups;
    }

    /// <summary>Gets dependencies used by multiple later pages and absent from page one.</summary>
    /// <param name="count">The graph object count.</param>
    /// <param name="first">The first page's dependency set.</param>
    /// <param name="uses">The page usage counts.</param>
    /// <returns>The shared graph objects.</returns>
    private static int[] GetSharedOther(int count, HashSet<int> first, int[] uses)
    {
        var shared = new List<int>();
        for (var number = 1; number <= count; number++)
        {
            if (uses[number] > 1 && !first.Contains(number))
            {
                shared.Add(number);
            }
        }

        return [.. shared];
    }

    /// <summary>Gets objects not assigned to page groups, shared objects or the catalog.</summary>
    /// <param name="count">The graph object count.</param>
    /// <param name="groups">The page groups.</param>
    /// <param name="shared">The later-page shared objects.</param>
    /// <param name="outline">The outline objects.</param>
    /// <param name="catalog">The catalog graph number.</param>
    /// <param name="openDocument">The document-opening objects.</param>
    /// <returns>The trailing graph objects.</returns>
    private static int[] GetTrailing(int count, int[][] groups, int[] shared, int[] outline, int catalog, int[] openDocument)
    {
        var assigned = new bool[count + 1];
        assigned[catalog] = true;
        foreach (var group in groups)
        {
            foreach (var number in group)
            {
                assigned[number] = true;
            }
        }

        foreach (var number in shared)
        {
            assigned[number] = true;
        }

        foreach (var number in outline)
        {
            assigned[number] = true;
        }

        foreach (var number in openDocument)
        {
            assigned[number] = true;
        }

        var trailing = new List<int>();
        for (var number = 1; number <= count; number++)
        {
            if (!assigned[number])
            {
                trailing.Add(number);
            }
        }

        return [.. trailing];
    }

    /// <summary>Indexes page dependencies into the first-page and later shared-object sections.</summary>
    /// <param name="sets">Each page's dependencies.</param>
    /// <param name="firstGroup">The first page's objects.</param>
    /// <param name="sharedOther">The later-page shared objects.</param>
    /// <returns>Shared identifiers per page, empty for the first page.</returns>
    private static int[][] GetSharedIdentifiers(HashSet<int>[] sets, int[] firstGroup, int[] sharedOther)
    {
        var shared = new int[firstGroup.Length + sharedOther.Length];
        firstGroup.CopyTo(shared, 0);
        sharedOther.CopyTo(shared, firstGroup.Length);
        var identifiers = new int[sets.Length][];
        identifiers[0] = [];
        for (var page = 1; page < sets.Length; page++)
        {
            var needed = new List<int>();
            for (var index = 0; index < shared.Length; index++)
            {
                if (sets[page].Contains(shared[index]))
                {
                    needed.Add(index);
                }
            }

            identifiers[page] = [.. needed];
        }

        return identifiers;
    }

    /// <summary>Assigns output numbers to one ordered group.</summary>
    /// <param name="group">The graph numbers.</param>
    /// <param name="next">The next output number.</param>
    private void Assign(int[] group, ref int next)
    {
        foreach (var graphNumber in group)
        {
            var old = Graph.GetOldNumber(graphNumber);
            Renumbering[old] = next;
            next++;
        }
    }

    /// <summary>The state of one bounded reference walk.</summary>
    /// <param name="Graph">The repaired objects.</param>
    /// <param name="Excluded">Objects outside this section.</param>
    /// <param name="Set">The visited objects.</param>
    /// <param name="IncludeFormTree">Whether to follow form parent and child links.</param>
    /// <param name="CancellationToken">Cancels the walk.</param>
    private sealed record WalkState(PdfObjectGraph Graph, bool[] Excluded, HashSet<int> Set, bool IncludeFormTree, CancellationToken CancellationToken);

    /// <summary>The ordered sections placed around the two cross-reference tables.</summary>
    /// <param name="PageGroups">The page groups.</param>
    /// <param name="SharedOther">Objects shared by later pages.</param>
    /// <param name="Trailing">Objects outside page and outline groups.</param>
    /// <param name="Outline">The outline objects.</param>
    /// <param name="CatalogGraphNumber">The catalog graph number.</param>
    /// <param name="OpenDocument">Objects needed at document open.</param>
    /// <param name="SharedIdentifiers">Each page's shared-object identifiers.</param>
    private sealed record PlanSections(int[][] PageGroups, int[] SharedOther, int[] Trailing, int[] Outline, int CatalogGraphNumber, int[] OpenDocument, int[][] SharedIdentifiers);
}
