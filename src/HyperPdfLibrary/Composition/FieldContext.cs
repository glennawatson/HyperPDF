// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

namespace HyperPdfLibrary.Composition;

/// <summary>The values fields show on one page.</summary>
/// <param name="DisplayNumber">The number the page shows.</param>
/// <param name="NumberFormat">How the page number is written.</param>
/// <param name="PageCount">The number of pages in the document.</param>
/// <param name="SectionNumber">The number of the section, from 1.</param>
/// <param name="SectionPageCount">The number of pages in the section.</param>
/// <param name="Now">The date and time of the composition.</param>
/// <param name="Title">The document's title, or <see langword="null"/>.</param>
/// <param name="Author">The document's author, or <see langword="null"/>.</param>
[DebuggerDisplay("FieldContext: page {DisplayNumber} of {PageCount}")]
internal sealed record FieldContext(
    int DisplayNumber,
    PdfNumberFormat NumberFormat,
    int PageCount,
    int SectionNumber,
    int SectionPageCount,
    DateTimeOffset Now,
    string? Title,
    string? Author)
{
    /// <summary>The date format used when a date field names none.</summary>
    private const string DefaultDateFormat = "yyyy-MM-dd";

    /// <summary>Writes what a field shows.</summary>
    /// <param name="field">The field.</param>
    /// <returns>The text.</returns>
    internal string GetText(PdfField field) => field.Kind switch
    {
        PdfFieldKind.PageNumber => NumberText.Format(DisplayNumber, field.NumberFormat ?? NumberFormat),
        PdfFieldKind.PageCount => NumberText.Format(PageCount, field.NumberFormat ?? PdfNumberFormat.Decimal),
        PdfFieldKind.SectionNumber => NumberText.Format(SectionNumber, field.NumberFormat ?? PdfNumberFormat.Decimal),
        PdfFieldKind.SectionPageCount => NumberText.Format(SectionPageCount, field.NumberFormat ?? PdfNumberFormat.Decimal),
        PdfFieldKind.Date => Now.ToString(field.DateFormat ?? DefaultDateFormat, CultureInfo.InvariantCulture),
        PdfFieldKind.Title => Title ?? string.Empty,
        _ => Author ?? string.Empty,
    };
}
