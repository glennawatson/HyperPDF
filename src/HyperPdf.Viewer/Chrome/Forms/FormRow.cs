// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>One row of a form dialog, read from a view model when the dialog is laid out.</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Label">The row's text, or the name of its setting.</param>
/// <param name="Value">The text of a typed setting.</param>
/// <param name="Options">The values of a setting with a list.</param>
/// <param name="Selected">The chosen value of a setting with a list, or 1 for a setting that is on.</param>
/// <param name="Enabled">Whether the row can be pressed.</param>
/// <param name="Id">The number the form gave the row, which comes back with each change the user makes to it, or -1 for a row that cannot change.</param>
/// <param name="Target">What the form attached to the row, which comes back with each change, or <see langword="null"/>.</param>
[DebuggerDisplay("FormRow: {Kind} {Label}")]
internal sealed record FormRow(FormRowKind Kind, string Label, string Value, IReadOnlyList<string> Options, int Selected, bool Enabled, int Id, object? Target)
{
    /// <summary>Gets the icon drawn before the words of something to do, which says by its shape and tint what kind of thing it does.</summary>
    internal ChromeIcon Icon { get; init; }

    /// <summary>Gets what the setting does, shown when the pointer rests on it, or <see langword="null"/> for a row that says nothing more.</summary>
    internal string? Help { get; init; }

    /// <summary>Gets a value indicating whether the setting has a value it starts with, which it can be put back to.</summary>
    internal bool CanReset { get; init; }

    /// <summary>Gets what putting the setting back does, or <see langword="null"/> while it already has the value it starts with.</summary>
    internal string? ResetHelp { get; init; }
}
