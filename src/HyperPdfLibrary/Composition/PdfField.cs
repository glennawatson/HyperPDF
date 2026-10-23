// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A value filled in when the page is drawn, such as the page number in a footer.</summary>
/// <param name="Kind">What the field shows.</param>
[DebuggerDisplay("PdfField: {Kind}")]
public sealed record PdfField(PdfFieldKind Kind) : PdfInline
{
    /// <summary>Gets a page number field.</summary>
    public static PdfField PageNumber { get; } = new(PdfFieldKind.PageNumber);

    /// <summary>Gets a field that shows the number of pages in the document.</summary>
    public static PdfField PageCount { get; } = new(PdfFieldKind.PageCount);

    /// <summary>Gets the name of a character style to lay over the paragraph's formatting, or <see langword="null"/> for none.</summary>
    public string? Style { get; init; }

    /// <summary>Gets formatting laid over the style, or <see langword="null"/> for none.</summary>
    public PdfCharacterFormat? Format { get; init; }

    /// <summary>Gets the .NET date and time format for a <see cref="PdfFieldKind.Date"/> field; the default is <c>yyyy-MM-dd</c>.</summary>
    public string? DateFormat { get; init; }

    /// <summary>Gets the number format for a page number or a count, or <see langword="null"/> to use the section's page number format for page numbers and decimal digits for counts.</summary>
    public PdfNumberFormat? NumberFormat { get; init; }
}
