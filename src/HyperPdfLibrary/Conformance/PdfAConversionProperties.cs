// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The descriptive metadata a PDF/A conversion writes to both the XMP packet and, for parts 1 to 3, /Info.</summary>
/// <param name="Title">The title, or <see langword="null"/>.</param>
/// <param name="Creators">The authors, in order.</param>
/// <param name="Description">The subject, or <see langword="null"/>.</param>
/// <param name="Subjects">The <c>dc:subject</c> terms.</param>
/// <param name="Keywords">The keywords, or <see langword="null"/>.</param>
/// <param name="CreatorTool">The application that made the original, or <see langword="null"/>.</param>
/// <param name="Producer">The application that wrote the PDF, or <see langword="null"/>.</param>
/// <param name="Created">The creation date, or <see langword="null"/>.</param>
/// <param name="Modified">The modification date, or <see langword="null"/>.</param>
/// <param name="Trapped">The trapping state: True, False or Unknown; <see langword="null"/> when not given.</param>
/// <param name="PdfUaPart">The PDF/UA part the document claims, or <see langword="null"/>.</param>
[DebuggerDisplay("PdfAConversionProperties: {Title}")]
internal sealed record PdfAConversionProperties(
    string? Title,
    string[] Creators,
    string? Description,
    string[] Subjects,
    string? Keywords,
    string? CreatorTool,
    string? Producer,
    DateTimeOffset? Created,
    DateTimeOffset? Modified,
    string? Trapped,
    int? PdfUaPart);
