// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>One line of a dialog made of rows: a control, with the name of its setting beside it when it has one.</summary>
/// <param name="Kind">What the control is.</param>
/// <param name="Action">What a press on the control does.</param>
/// <param name="Index">The row the control belongs to, or the row and one of its values as one number.</param>
/// <param name="Text">The text of the control.</param>
/// <param name="Enabled">Whether the control can be pressed.</param>
/// <param name="Selected">Whether the control is the one chosen or typed into.</param>
[DebuggerDisplay("FormLine: {Kind} {Label} {Text}")]
internal sealed record FormLine(ChromeItemKind Kind, OverlayAction Action, int Index, string Text, bool Enabled, bool Selected)
{
    /// <summary>Gets what the control tells a screen reader it is.</summary>
    internal ChromeItemTrait Trait { get; init; }

    /// <summary>Gets the name of the setting, drawn in the column of names to the left of the control. Empty for a line that has no name.</summary>
    internal string Label { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the control sits in the column of controls although it has no name of its own, as the values of an open list do.</summary>
    internal bool InColumn { get; init; }

    /// <summary>Gets the icon drawn before the text: the box of a switch, the ring of a value or the chevron of a list.</summary>
    internal ChromeIcon Icon { get; init; }

    /// <summary>Gets a value indicating whether the control is drawn in a box, so it is seen to be something to press.</summary>
    internal bool Framed { get; init; }
}
