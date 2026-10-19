// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Creation;

/// <summary>What a structure element says besides its type (ISO 32000-2 14.7.2 and 14.8.4).</summary>
/// <param name="AlternativeText">The description written as Alt, or null to omit it.</param>
/// <param name="ActualText">The replacement text, or null to omit it.</param>
/// <param name="Language">The element's BCP 47 language, or null to inherit it.</param>
/// <param name="Title">The element's title, or null to omit it.</param>
/// <param name="TableScope">The cells labelled by a table header.</param>
/// <param name="RowSpan">The number of spanned rows; values above one are written.</param>
/// <param name="ColumnSpan">The number of spanned columns; values above one are written.</param>
/// <param name="NoteType">The FENote subtype; null omits the optional attribute.</param>
[DebuggerDisplay("PdfTagOptions: alt {AlternativeText}, scope {TableScope}")]
public sealed record PdfTagOptions(
    string? AlternativeText,
    string? ActualText,
    string? Language,
    string? Title,
    PdfTableScope TableScope,
    int RowSpan,
    int ColumnSpan,
    PdfNoteType? NoteType)
{
    /// <summary>Initializes a new instance of the <see cref="PdfTagOptions"/> class with no extra entries.</summary>
    public PdfTagOptions()
        : this(null, null, null, null, PdfTableScope.None, 0, 0, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfTagOptions"/> class.</summary>
    /// <param name="options">The element, table and note options to retain.</param>
    /// <param name="listNumbering">The optional list scheme.</param>
    public PdfTagOptions(PdfTagOptions options, PdfListNumbering? listNumbering)
        : this(options)
    {
        ListNumbering = listNumbering;
    }

    /// <summary>Gets the options that add nothing.</summary>
    public static PdfTagOptions Default { get; } = new();

    /// <summary>Gets the list's label scheme; null omits the optional attribute.</summary>
    public PdfListNumbering? ListNumbering { get; init; }

    /// <summary>Gets the non-interactive field attributes; null omits the PrintField owner.</summary>
    public PdfPrintFieldAttributes? PrintField { get; init; }

    /// <summary>Returns the element, table and optional note and list options.</summary>
    /// <param name="alternativeText">Receives the description.</param>
    /// <param name="actualText">Receives the replacement text.</param>
    /// <param name="language">Receives the language.</param>
    /// <param name="title">Receives the title.</param>
    /// <param name="tableScope">Receives the table header scope.</param>
    /// <param name="rowSpan">Receives the row span.</param>
    /// <param name="columnSpan">Receives the column span.</param>
    /// <param name="noteType">Receives the optional FENote subtype.</param>
    /// <param name="listNumbering">Receives the optional list scheme.</param>
    public void Deconstruct(
        out string? alternativeText,
        out string? actualText,
        out string? language,
        out string? title,
        out PdfTableScope tableScope,
        out int rowSpan,
        out int columnSpan,
        out PdfNoteType? noteType,
        out PdfListNumbering? listNumbering)
    {
        Deconstruct(out alternativeText, out actualText, out language, out title, out tableScope, out rowSpan, out columnSpan, out noteType);
        listNumbering = ListNumbering;
    }
}
