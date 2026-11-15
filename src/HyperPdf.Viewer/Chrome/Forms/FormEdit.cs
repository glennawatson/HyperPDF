// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>One change the user made in a form dialog: a switch flipped, a value chosen, text typed or an action pressed.</summary>
/// <param name="Id">The number the form gave the row.</param>
/// <param name="Index">The chosen value of a list, or 1 for a switch turned on and 0 for one turned off.</param>
/// <param name="Text">The whole text of a text box after the change.</param>
/// <param name="Target">What the form attached to the row, or <see langword="null"/>.</param>
[DebuggerDisplay("FormEdit {Id}: {Index} '{Text}'")]
internal readonly record struct FormEdit(int Id, int Index, string Text, object? Target)
{
    /// <summary>Gets a value indicating whether a switch was turned on.</summary>
    internal bool On => Index != 0;
}
