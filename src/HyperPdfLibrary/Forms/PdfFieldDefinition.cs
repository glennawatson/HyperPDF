// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>Describes a form field to create: its type, name, widgets and the attributes ISO 32000-2 12.7 gives each type.</summary>
/// <param name="Type">The kind of field. <see cref="PdfFieldType.Unknown"/> is not allowed.</param>
/// <param name="Name">The field's fully qualified name. A period nests the field under fields of the shorter names, for example <c>Address.Street</c>.</param>
/// <param name="Widgets">Where the field is shown. A field with one widget is written as one merged dictionary; several widgets give the field a dictionary of its own with one kid per widget.</param>
[DebuggerDisplay("PdfFieldDefinition: {Type} {Name}")]
public sealed record PdfFieldDefinition(PdfFieldType Type, string Name, PdfWidgetPlacement[] Widgets)
{
    /// <summary>Gets the tooltip and alternate text (<c>/TU</c>). Null uses the field name, so assistive software always has a label.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Gets the flags the caller sets (<c>/Ff</c>). The type's own flags (radio, push button, combo box) are added.</summary>
    public PdfFieldFlags Flags { get; init; }

    /// <summary>Gets the initial value: the text of a text field, the selected export value of a radio group, combo box or list box.</summary>
    public string? Value { get; init; }

    /// <summary>Gets the values selected in a list box that allows several selections.</summary>
    public string[] Values { get; init; } = [];

    /// <summary>Gets the default value (<c>/DV</c>) used when the form is reset.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets a value indicating whether a check box starts checked.</summary>
    public bool IsChecked { get; init; }

    /// <summary>Gets the export value of a check box when it is checked; null uses <c>Yes</c>.</summary>
    public string? ExportValue { get; init; }

    /// <summary>Gets the most characters a text field takes; 0 for no limit. A comb field needs it.</summary>
    public int MaxLength { get; init; }

    /// <summary>Gets the text alignment (<c>/Q</c>): 0 left, 1 centred, 2 right.</summary>
    public int Quadding { get; init; }

    /// <summary>Gets the default appearance string (<c>/DA</c>), such as <c>/Helv 12 Tf 0 g</c>; null builds one from the font size and the form's font.</summary>
    public string? DefaultAppearance { get; init; }

    /// <summary>Gets the size the text is drawn at; 0 fits it to the field. Null picks 0 for single line text and 12 otherwise.</summary>
    public float? FontSize { get; init; }

    /// <summary>Gets the choices of a combo box or list box.</summary>
    public PdfFieldOption[] Options { get; init; } = [];

    /// <summary>
    /// Gets the caption of a push button (<c>/MK /CA</c>), or the mark of a check box or radio button:
    /// <c>4</c> check, <c>8</c> cross, <c>l</c> circle, <c>u</c> diamond, <c>n</c> square, <c>H</c> star.
    /// </summary>
    public string? Caption { get; init; }

    /// <summary>Gets the border colour (<c>/MK /BC</c>), or null for the reader's default.</summary>
    public PdfFieldColor? BorderColor { get; init; }

    /// <summary>Gets the background colour (<c>/MK /BG</c>), or null for none.</summary>
    public PdfFieldColor? BackgroundColor { get; init; }

    /// <summary>Gets the border width in points (<c>/BS /W</c>), or null for the default of 1.</summary>
    public float? BorderWidth { get; init; }

    /// <summary>Gets the border style (<c>/BS /S</c>), or null for solid.</summary>
    public PdfFieldBorderStyle? BorderStyle { get; init; }

    /// <summary>Gets the actions the field carries.</summary>
    public PdfFieldActions? Actions { get; init; }

    /// <summary>Gets the names of the fields a signature field locks once it is signed (<c>/Lock</c>); empty for none.</summary>
    public string[] LockedFields { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the library draws the widget appearances. When false the form asks readers to draw
    /// them (<c>/NeedAppearances</c>), which a document that claims PDF/A cannot do.
    /// </summary>
    public bool GenerateAppearances { get; init; } = true;
}
