// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A field with the attributes the form design edits, read from the document.</summary>
/// <param name="Name">The fully qualified name.</param>
/// <param name="Type">The kind of field.</param>
/// <param name="Flags">The field flags.</param>
/// <param name="Tooltip">The tooltip and alternate text (<c>/TU</c>); empty when the field has none.</param>
/// <param name="Value">The text, selected export value or, for a radio group, the export value of the button that is on; <see langword="null"/> for none.</param>
/// <param name="Values">The selected export values of a list box that allows several selections.</param>
/// <param name="DefaultValue">The default value (<c>/DV</c>), or <see langword="null"/>.</param>
/// <param name="MaxLength">The most characters a text field takes; 0 for no limit.</param>
/// <param name="Quadding">The text alignment (<c>/Q</c>): 0 left, 1 centred, 2 right.</param>
/// <param name="DefaultAppearance">The default appearance string (<c>/DA</c>), as the field or the form holds it.</param>
/// <param name="Options">The choices of a combo box or list box.</param>
/// <param name="Widgets">The field's widgets.</param>
[DebuggerDisplay("PdfFieldDetails: {Type} {Name}")]
public sealed record PdfFieldDetails(
    string Name,
    PdfFieldType Type,
    PdfFieldFlags Flags,
    string Tooltip,
    string? Value,
    string[] Values,
    string? DefaultValue,
    int MaxLength,
    int Quadding,
    string DefaultAppearance,
    PdfFieldOption[] Options,
    PdfWidgetDetails[] Widgets)
{
    /// <summary>Gets the border colour of the field's first widget, or <see langword="null"/> for none set.</summary>
    public PdfFieldColor? BorderColor { get; init; }

    /// <summary>Gets the background colour of the field's first widget, or <see langword="null"/> for none set.</summary>
    public PdfFieldColor? BackgroundColor { get; init; }

    /// <summary>Gets the border width of the field's first widget.</summary>
    public float BorderWidth { get; init; } = 1;

    /// <summary>Gets the border style of the field's first widget.</summary>
    public PdfFieldBorderStyle BorderStyle { get; init; }

    /// <summary>Gets the caption of a push button or the mark of a check box or radio button.</summary>
    public string? Caption { get; init; }

    /// <summary>Gets the actions of the field, or <see langword="null"/> when it has none.</summary>
    public PdfFieldActions? Actions { get; init; }

    /// <summary>Gets a value indicating whether a check box is on, or any button of a radio group is.</summary>
    public bool IsChecked
    {
        get
        {
            foreach (var widget in Widgets)
            {
                if (widget.IsChecked)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
