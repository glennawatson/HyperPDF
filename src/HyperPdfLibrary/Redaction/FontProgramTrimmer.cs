// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Redaction;

/// <summary>
/// Cuts the embedded font programs of the fonts that lost text down to the glyphs the document still shows, so the outlines
/// of redacted characters do not stay in the file. It uses the optimiser's subset planning: every page is read to find the
/// glyphs still shown, a program shared with an annotation, form field or pattern keeps all its glyphs, and glyph numbers are
/// kept so widths and the remaining text stay valid. A font that lost nothing is left as it was.
/// </summary>
internal static class FontProgramTrimmer
{
    /// <summary>Cuts down the programs of the fonts that lost text.</summary>
    /// <param name="document">The document, edited in place inside the redaction transaction.</param>
    /// <param name="tally">What the redaction removed, including the fonts that lost codes.</param>
    /// <param name="cancellationToken">Stops page interpretation.</param>
    internal static void Trim(PdfDocument document, RedactionTally tally, CancellationToken cancellationToken)
    {
        if (tally.RemovedCodes.Count == 0)
        {
            return;
        }

        var store = document.Objects;
        var resources = document.DrawingServices.Optimization;
        var scanner = ContentUsageScanner.Rent(document, cancellationToken);
        OptimizerGraph? graph = null;
        try
        {
            scanner.ScanDocument(null, cancellationToken);
            var only = FontNumbers(scanner, tally);
            if (only.Count == 0)
            {
                return;
            }

            graph = OptimizerGraph.Collect(store, store.Security is not null, null, resources);
            using var state = FontPlanState.Rent(resources);
            state.OnlyFonts.UnionWith(only);
            state.LimitToOnlyFonts = true;
            FontSubsetPlanner.Plan(document, scanner, graph, state, new(), cancellationToken);
            Apply(document, state);
        }
        finally
        {
            graph?.Return();
            scanner.Return();
        }
    }

    /// <summary>Finds the object numbers of the fonts that lost codes.</summary>
    /// <param name="scanner">The scan of the pages, which numbers the fonts it saw.</param>
    /// <param name="tally">What the redaction removed.</param>
    /// <returns>The object numbers.</returns>
    private static HashSet<int> FontNumbers(ContentUsageScanner scanner, RedactionTally tally)
    {
        var numbers = new HashSet<int>();
        foreach (var font in tally.RemovedCodes.Keys)
        {
            if (scanner.FontNumbers.TryGetValue(font, out var number))
            {
                _ = numbers.Add(number);
            }
        }

        return numbers;
    }

    /// <summary>Replaces the programs that were cut down, and tags the fonts that name them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="state">The plan.</param>
    private static void Apply(PdfDocument document, FontPlanState state)
    {
        var store = document.Objects;
        foreach (var (program, subset) in state.Subsets)
        {
            StoreEditing.Replace(store, new(program, 0), PdfValue.FromStream(subset));
        }

        foreach (var (number, rename) in state.Renames)
        {
            if (StoreReading.GetObject(store, new(number, 0)).AsDictionary() is { } dictionary)
            {
                StoreEditing.Replace(store, new(number, 0), PdfValue.FromDictionary(FontNaming.Apply(dictionary, rename, store.Names)));
            }
        }
    }
}
