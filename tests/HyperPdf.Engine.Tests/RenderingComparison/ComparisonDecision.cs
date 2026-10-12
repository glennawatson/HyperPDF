// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>A reference decision based on standards scores, independent of renderer agreement.</summary>
/// <param name="Reference">The selected reference or required review.</param>
/// <param name="HyperPdfAssessment">HyperPDF's result against the supplied oracle.</param>
/// <param name="HyperPdfScore">The HyperPDF oracle score, when an oracle exists.</param>
/// <param name="PdfiumScore">The PDFium oracle score, when both are available.</param>
/// <param name="PdfJsScore">The pdf.js oracle score, when both are available.</param>
/// <param name="Category">Whether this is a standards, recovery or unreviewed comparison.</param>
/// <param name="Reason">The independent reason for this reference decision.</param>
internal sealed record ComparisonDecision(
    ReferenceSelection Reference,
    OracleAssessment HyperPdfAssessment,
    RasterScore? HyperPdfScore,
    RasterScore? PdfiumScore,
    RasterScore? PdfJsScore,
    ComparisonCategory Category,
    ReferenceReason Reason);
