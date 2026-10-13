// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>A field to add to the form (ISO 32000-2 12.7).</summary>
/// <param name="Kind">The kind of field.</param>
/// <param name="Name">The fully qualified name; a period nests the field under shorter names.</param>
/// <param name="Widgets">Where the field is shown.</param>
[DebuggerDisplay("FormFieldDesign: {Kind} {Name}")]
public sealed record FormFieldDesign(FormFieldKind Kind, string Name, IReadOnlyList<FormWidgetDesign> Widgets)
{
    /// <summary>Gets the tooltip and alternate text; null uses the field name.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Gets the flags to set.</summary>
    public FormDesignFlags Flags { get; init; }

    /// <summary>Gets the starting value: text, or the export value to select.</summary>
    public string? Value { get; init; }

    /// <summary>Gets the values selected in a list box that allows several.</summary>
    public IReadOnlyList<string> Values { get; init; } = [];

    /// <summary>Gets the value the form returns to when it is reset.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets a value indicating whether a check box starts on.</summary>
    public bool IsChecked { get; init; }

    /// <summary>Gets the export value of a check box when it is on; null uses <c>Yes</c>.</summary>
    public string? ExportValue { get; init; }

    /// <summary>Gets the most characters a text field takes; 0 for no limit.</summary>
    public int MaxLength { get; init; }

    /// <summary>Gets the text alignment: 0 left, 1 centred, 2 right.</summary>
    public int Quadding { get; init; }

    /// <summary>Gets the default appearance string, such as <c>/Helv 12 Tf 0 g</c>; null builds one.</summary>
    public string? DefaultAppearance { get; init; }

    /// <summary>Gets the text size in points; 0 fits the text to the field; null picks a size.</summary>
    public float? FontSize { get; init; }

    /// <summary>Gets the choices of a combo box or list box.</summary>
    public IReadOnlyList<FormChoiceOption> Options { get; init; } = [];

    /// <summary>Gets the caption of a push button, or the mark of a check box or radio button.</summary>
    public string? Caption { get; init; }
}
