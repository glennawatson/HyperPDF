// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>Turns the content features no fix can change into reasons the conversion stops.</summary>
internal static class PdfAConversionScanProblems
{
    /// <summary>The deepest <c>q</c> nesting PDF/A-1 allows (ISO 19005-1 §6.1.12, Table 1).</summary>
    private const int MaxSaveDepth = 28;

    /// <summary>Records the problems.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="facts">What the scan found.</param>
    internal static void Report(PdfAConversionSession session, PdfAConversionFacts facts)
    {
        if (facts.UnreadableStreams > 0)
        {
            session.Refuse("Some page content could not be read, so it cannot be checked for PDF/A.", PdfARule.UndefinedOperator, PdfARule.ContentUndefinedResources);
        }

        if (facts.UndefinedOperators > 0 && session.Applies(PdfARule.UndefinedOperator))
        {
            session.Refuse("Some page content uses commands the PDF standard does not define.", PdfARule.UndefinedOperator);
        }

        if (facts.MaxSaveDepth > MaxSaveDepth && session.Applies(PdfARule.GraphicsStateNesting))
        {
            session.Refuse("Some page content nests graphics states deeper than PDF/A-1 allows.", PdfARule.GraphicsStateNesting);
        }

        if (facts.UnknownIntents > 0)
        {
            session.Refuse("Some page content names a rendering intent the PDF standard does not define.", PdfARule.RenderingIntentName);
        }

        if (facts.InlineForbiddenFilters > 0)
        {
            session.Refuse("Some small images inside the page content use LZW compression, which PDF/A does not allow.", PdfARule.InlineFilterLzwForbidden, PdfARule.InlineFilterAllowed);
        }

        if (facts.InlineInterpolated > 0)
        {
            session.Refuse("Some small images inside the page content ask to be smoothed, which PDF/A does not allow.", PdfARule.ImageInterpolate);
        }
    }
}
