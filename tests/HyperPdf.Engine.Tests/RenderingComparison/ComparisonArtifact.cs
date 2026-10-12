// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>The exact fixture, expectations, engine scores and browser identity for a completed comparison.</summary>
/// <param name="Fixture">The graphics case.</param>
/// <param name="PdfSha256">The generated source hash.</param>
/// <param name="Runtime">The actual managed runtime.</param>
/// <param name="Width">The expected raster width.</param>
/// <param name="Height">The expected raster height.</param>
/// <param name="HyperPdfGeometry">The actual HyperPDF raster geometry.</param>
/// <param name="PdfiumGeometry">The actual PDFium raster geometry.</param>
/// <param name="PdfJsGeometry">The actual browser raster geometry.</param>
/// <param name="Regions">The independently derived expected regions and source clauses.</param>
/// <param name="Decision">The oracle scores and selected reference.</param>
/// <param name="HyperPdfToPdfium">The whole-image differences between HyperPDF and PDFium, without conformance authority.</param>
/// <param name="HyperPdfToPdfJs">The whole-image differences between HyperPDF and pdf.js, without conformance authority.</param>
/// <param name="PdfiumToPdfJs">The measured reference-engine similarity.</param>
/// <param name="PdfJs">The actual browser and pdf.js metadata.</param>
internal sealed record ComparisonArtifact(
    StandardsRenderCase Fixture,
    string PdfSha256,
    string Runtime,
    int Width,
    int Height,
    RasterDimensions HyperPdfGeometry,
    RasterDimensions PdfiumGeometry,
    RasterDimensions PdfJsGeometry,
    ExpectedPixelRegion[] Regions,
    ComparisonDecision Decision,
    RasterScore HyperPdfToPdfium,
    RasterScore HyperPdfToPdfJs,
    RasterScore PdfiumToPdfJs,
    PdfJsRendererMetadata PdfJs);
