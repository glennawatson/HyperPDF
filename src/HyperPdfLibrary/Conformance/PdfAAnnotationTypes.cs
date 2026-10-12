// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The annotation types each PDF/A part allows, interned for one document.</summary>
/// <param name="PdfA1">The types PDF/A-1 allows.</param>
/// <param name="PdfA2">The types PDF/A-2 and PDF/A-3 allow.</param>
/// <param name="PdfA4">The types every PDF/A-4 level allows.</param>
/// <param name="PdfA4E">The types PDF/A-4e adds.</param>
/// <param name="FileAttachment">The FileAttachment type, which PDF/A-4f adds.</param>
internal sealed record PdfAAnnotationTypes(
    HashSet<PdfName> PdfA1,
    HashSet<PdfName> PdfA2,
    HashSet<PdfName> PdfA4,
    HashSet<PdfName> PdfA4E,
    PdfName FileAttachment);
