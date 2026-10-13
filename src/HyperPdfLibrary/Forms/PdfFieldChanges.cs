// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>Changes to make to a field. A property left <see langword="null"/> keeps what the field has.</summary>
[DebuggerDisplay("PdfFieldChanges")]
public sealed record PdfFieldChanges
{
    /// <summary>Gets the new tooltip and alternate text (<c>/TU</c>). An empty text falls back to the field name.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Gets the flags to set. The flags that decide the field's kind cannot be changed.</summary>
    public PdfFieldFlags AddFlags { get; init; }

    /// <summary>Gets the flags to clear.</summary>
    public PdfFieldFlags RemoveFlags { get; init; }

    /// <summary>Gets the new value: text, or the export value to select in a radio group, combo box or list box.</summary>
    public string? Value { get; init; }

    /// <summary>Gets the new selection of a list box that allows several.</summary>
    public string[]? Values { get; init; }

    /// <summary>Gets whether a check box is on.</summary>
    public bool? IsChecked { get; init; }

    /// <summary>Gets the new default value. An empty text removes it.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the new character limit of a text field. 0 removes the limit.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Gets the new text alignment (<c>/Q</c>): 0 left, 1 centred, 2 right.</summary>
    public int? Quadding { get; init; }

    /// <summary>Gets the new default appearance string (<c>/DA</c>); it must name a font in the form's default resources.</summary>
    public string? DefaultAppearance { get; init; }

    /// <summary>Gets the new choices of a combo box or list box. A selection that is no longer an option is dropped.</summary>
    public PdfFieldOption[]? Options { get; init; }

    /// <summary>Gets the new export values: one per widget of a radio group, or one for a check box.</summary>
    public string[]? ExportValues { get; init; }

    /// <summary>Gets the new caption of a push button, or mark of a check box or radio button.</summary>
    public string? Caption { get; init; }

    /// <summary>Gets the new border colour.</summary>
    public PdfFieldColor? BorderColor { get; init; }

    /// <summary>Gets the new background colour.</summary>
    public PdfFieldColor? BackgroundColor { get; init; }

    /// <summary>Gets the new border width in points.</summary>
    public float? BorderWidth { get; init; }

    /// <summary>Gets the new border style.</summary>
    public PdfFieldBorderStyle? BorderStyle { get; init; }

    /// <summary>Gets the actions that replace the field's actions. An empty set removes them.</summary>
    public PdfFieldActions? Actions { get; init; }
}
