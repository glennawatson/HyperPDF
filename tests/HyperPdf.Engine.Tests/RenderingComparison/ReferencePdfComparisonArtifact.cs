// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Retains unreviewed three-renderer reference PDF evidence without a conformance claim.</summary>
/// <param name="PdfFile">The source file name.</param>
/// <param name="SourceSha256">The SHA256 of the exact rendered PDF.</param>
/// <param name="SourceValidation">The independent source validation status.</param>
/// <param name="Framework">The actual executing runtime.</param>
/// <param name="PageIndex">The zero-based rendered page.</param>
/// <param name="Scale">The requested pixels per PDF point.</param>
/// <param name="HyperPdfEngine">The actual HyperPDF adapter assembly identity.</param>
/// <param name="PdfiumRenderer">The PDFium reference renderer.</param>
/// <param name="HyperPdfPageCount">The page count reported by HyperPDF.</param>
/// <param name="PdfiumPageCount">The page count reported by PDFium.</param>
/// <param name="HyperPdfGeometry">HyperPDF's independently derived pixel dimensions.</param>
/// <param name="PdfiumGeometry">PDFium's independently derived pixel dimensions.</param>
/// <param name="PdfJsGeometry">pdf.js's independently derived pixel dimensions.</param>
/// <param name="Classification">The unresolved standards classification.</param>
/// <param name="HyperPdfVsPdfium">The raw HyperPDF/PDFium differences.</param>
/// <param name="HyperPdfVsPdfJs">The raw HyperPDF/pdf.js differences.</param>
/// <param name="PdfiumVsPdfJs">The raw PDFium/pdf.js differences.</param>
/// <param name="PdfJsMetadata">The actual browser and viewport metadata.</param>
internal sealed record ReferencePdfComparisonArtifact(
    string PdfFile,
    string SourceSha256,
    string SourceValidation,
    string Framework,
    int PageIndex,
    double Scale,
    string HyperPdfEngine,
    string PdfiumRenderer,
    int HyperPdfPageCount,
    int PdfiumPageCount,
    RasterDimensions HyperPdfGeometry,
    RasterDimensions PdfiumGeometry,
    RasterDimensions PdfJsGeometry,
    ComparisonDecision Classification,
    RenderedPageDifference HyperPdfVsPdfium,
    RenderedPageDifference HyperPdfVsPdfJs,
    RenderedPageDifference PdfiumVsPdfJs,
    PdfJsRendererMetadata PdfJsMetadata);
