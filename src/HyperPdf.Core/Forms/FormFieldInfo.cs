// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>A field with the attributes the form design edits.</summary>
/// <param name="Name">The fully qualified name.</param>
/// <param name="Kind">The kind of field.</param>
/// <param name="Flags">The flags the form design sets.</param>
/// <param name="Tooltip">The tooltip and alternate text; empty when none.</param>
/// <param name="Value">The text, selected export value or, for a radio group, the button that is on; <see langword="null"/> for none.</param>
/// <param name="Values">The selected export values of a list box that allows several.</param>
/// <param name="DefaultValue">The default value, or <see langword="null"/>.</param>
/// <param name="MaxLength">The most characters a text field takes; 0 for no limit.</param>
/// <param name="Quadding">The text alignment: 0 left, 1 centred, 2 right.</param>
/// <param name="DefaultAppearance">The default appearance string.</param>
/// <param name="Options">The choices of a combo box or list box.</param>
/// <param name="Widgets">The field's widgets.</param>
[DebuggerDisplay("FormFieldInfo: {Kind} {Name}")]
public sealed record FormFieldInfo(
    string Name,
    FormFieldKind Kind,
    FormDesignFlags Flags,
    string Tooltip,
    string? Value,
    IReadOnlyList<string> Values,
    string? DefaultValue,
    int MaxLength,
    int Quadding,
    string DefaultAppearance,
    IReadOnlyList<FormChoiceOption> Options,
    IReadOnlyList<FormWidgetInfo> Widgets);
