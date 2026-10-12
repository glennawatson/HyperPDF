// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Retains a fractional DeviceRGB comparison without claiming a precise raster oracle.</summary>
/// <param name="PdfSha256">The generated PDF hash.</param>
/// <param name="Runtime">The actual managed runtime.</param>
/// <param name="Clause">The source of the compositing and precision distinction.</param>
/// <param name="IdealBlendingColor">The ideal component-space result, before device quantization.</param>
/// <param name="HyperPdfGeometry">The actual HyperPDF raster size.</param>
/// <param name="PdfiumGeometry">The actual PDFium raster size.</param>
/// <param name="PdfJsGeometry">The actual browser raster size.</param>
/// <param name="HyperPdfInterior">The observed HyperPDF center pixel.</param>
/// <param name="PdfiumInterior">The observed PDFium center pixel.</param>
/// <param name="PdfJsInterior">The observed browser center pixel.</param>
/// <param name="Decision">The unresolved classification, with no conformance score.</param>
/// <param name="PdfJs">The browser and renderer identity.</param>
internal sealed record FractionalGroupArtifact(
    string PdfSha256,
    string Runtime,
    string Clause,
    BlendingColor IdealBlendingColor,
    RasterDimensions HyperPdfGeometry,
    RasterDimensions PdfiumGeometry,
    RasterDimensions PdfJsGeometry,
    Bgra32Color HyperPdfInterior,
    Bgra32Color PdfiumInterior,
    Bgra32Color PdfJsInterior,
    ComparisonDecision Decision,
    PdfJsRendererMetadata PdfJs);
