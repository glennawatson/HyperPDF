// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A document described by its content and styles: sections of paragraphs, lists, tables and more. <see cref="PdfComposer"/> lays it out on pages.</summary>
/// <param name="Styles">The named styles that paragraphs and runs use.</param>
/// <param name="Sections">The sections, in order.</param>
[DebuggerDisplay("PdfComposition: {Sections.Count} sections")]
public sealed record PdfComposition(PdfStyleSheet Styles, IReadOnlyList<PdfSection> Sections)
{
    /// <summary>Gets the document's title, which the <see cref="PdfFieldKind.Title"/> field shows.</summary>
    public string? Title { get; init; }

    /// <summary>Gets the document's author, which the <see cref="PdfFieldKind.Author"/> field shows.</summary>
    public string? Author { get; init; }
}
