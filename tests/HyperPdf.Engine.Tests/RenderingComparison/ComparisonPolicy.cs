// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Selects a reference only after independently checking well-formed PDF expectations.</summary>
internal static class ComparisonPolicy
{
    /// <summary>Scores each available renderer against the oracle; similarity cannot substitute for it.</summary>
    /// <param name="hyperPdf">The HyperPDF raster.</param>
    /// <param name="pdfium">The PDFium raster, when available.</param>
    /// <param name="pdfJs">The pdf.js raster, when available.</param>
    /// <param name="oracle">The independent oracle, or null when review is still required.</param>
    /// <returns>The selected reference and every independent score.</returns>
    internal static ComparisonDecision Evaluate(ComparisonRaster hyperPdf, ComparisonRaster? pdfium, ComparisonRaster? pdfJs, StandardsOracle? oracle)
    {
        if (oracle is null)
        {
            return new(ReferenceSelection.ReviewRequired, OracleAssessment.Unresolved, null, null, null, ComparisonCategory.Unreviewed, ReferenceReason.NoIndependentOracle);
        }

        var hyperScore = RasterScoring.AgainstOracle(hyperPdf, oracle);
        var pdfiumScore = Score(pdfium, oracle);
        var pdfJsScore = Score(pdfJs, oracle);
        var assessment = hyperScore.IsAcceptable ? OracleAssessment.Satisfied : OracleAssessment.Violated;
        var selection = Select(pdfiumScore, pdfJsScore);
        return new(selection, assessment, hyperScore, pdfiumScore, pdfJsScore, ComparisonCategory.WellFormedStandards, ReasonFor(selection));
    }

    /// <summary>Records malformed-file recovery as unresolved; raster agreement has no standards authority.</summary>
    /// <returns>The separate recovery category with no conformance scores.</returns>
    internal static ComparisonDecision Recovery() =>
        new(ReferenceSelection.ReviewRequired, OracleAssessment.Unresolved, null, null, null, ComparisonCategory.MalformedRecovery, ReferenceReason.MalformedRecovery);

    /// <summary>Names the independently established selection criterion.</summary>
    /// <param name="selection">The scored reference selection.</param>
    /// <returns>The selection reason.</returns>
    private static ReferenceReason ReasonFor(ReferenceSelection selection) => selection switch
    {
        ReferenceSelection.Pdfium => ReferenceReason.PreferredStandardsCorrectPdfium,
        ReferenceSelection.PdfJs => ReferenceReason.OnlyPdfJsSatisfiesOracle,
        _ => ReferenceReason.NeitherReferenceSatisfiesOracle,
    };

    /// <summary>Scores an available raster.</summary>
    /// <param name="raster">The raster, if available.</param>
    /// <param name="oracle">The independent expectation.</param>
    /// <returns>The score, or null for an unavailable renderer.</returns>
    private static RasterScore? Score(ComparisonRaster? raster, StandardsOracle oracle) =>
        raster is null ? null : RasterScoring.AgainstOracle(raster, oracle);

    /// <summary>Prefers PDFium when it satisfies the oracle, including allowed rounding differences.</summary>
    /// <param name="pdfium">PDFium's score.</param>
    /// <param name="pdfJs">pdf.js's score.</param>
    /// <returns>The standards-supported reference.</returns>
    private static ReferenceSelection Select(RasterScore? pdfium, RasterScore? pdfJs)
    {
        var pdfiumAcceptable = pdfium is { IsAcceptable: true };
        var pdfJsAcceptable = pdfJs is { IsAcceptable: true };
        if (!pdfiumAcceptable)
        {
            return pdfJsAcceptable ? ReferenceSelection.PdfJs : ReferenceSelection.OwnStandardsExpectation;
        }

        return ReferenceSelection.Pdfium;
    }
}
