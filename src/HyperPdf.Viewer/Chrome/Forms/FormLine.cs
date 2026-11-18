// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>One line of a form dialog as it is shown: a row, one wrapped line of its text, or one value of its open list.</summary>
/// <param name="Kind">How the line is drawn.</param>
/// <param name="Action">What a press on it means to the dialog.</param>
/// <param name="Index">The row it belongs to, and for a value of a list the value too.</param>
/// <param name="Text">The line's text.</param>
/// <param name="Enabled">Whether it can be pressed.</param>
/// <param name="Selected">Whether it is the chosen value, or the text box being typed into.</param>
[DebuggerDisplay("FormLine: {Kind} {Text}")]
internal sealed record FormLine(ChromeItemKind Kind, OverlayAction Action, int Index, string Text, bool Enabled, bool Selected)
{
    /// <summary>Gets what the line is beyond its kind: a switch or a list that opens.</summary>
    internal ChromeItemTrait Trait { get; init; }
}
