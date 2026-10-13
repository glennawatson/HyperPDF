// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>Changes to make to a field. A property left <see langword="null"/> keeps what the field has.</summary>
[DebuggerDisplay("FormFieldChanges")]
public sealed record FormFieldChanges
{
    /// <summary>Gets the new tooltip and alternate text.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Gets the flags to set.</summary>
    public FormDesignFlags AddFlags { get; init; }

    /// <summary>Gets the flags to clear.</summary>
    public FormDesignFlags RemoveFlags { get; init; }

    /// <summary>Gets the new value: text, or the export value to select.</summary>
    public string? Value { get; init; }

    /// <summary>Gets the new selection of a list box that allows several.</summary>
    public IReadOnlyList<string>? Values { get; init; }

    /// <summary>Gets whether a check box is on.</summary>
    public bool? IsChecked { get; init; }

    /// <summary>Gets the new default value; empty removes it.</summary>
    public string? DefaultValue { get; init; }

    /// <summary>Gets the new character limit; 0 removes it.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Gets the new text alignment: 0 left, 1 centred, 2 right.</summary>
    public int? Quadding { get; init; }

    /// <summary>Gets the new default appearance string; it must name a font in the form's default resources.</summary>
    public string? DefaultAppearance { get; init; }

    /// <summary>Gets the new choices of a combo box or list box.</summary>
    public IReadOnlyList<FormChoiceOption>? Options { get; init; }

    /// <summary>Gets the new export values: one per widget of a radio group, or one for a check box.</summary>
    public IReadOnlyList<string>? ExportValues { get; init; }

    /// <summary>Gets the new caption of a push button, or mark of a check box or radio button.</summary>
    public string? Caption { get; init; }
}
