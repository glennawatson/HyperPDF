// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// Collects the rows of a form dialog. A window describes itself by adding rows that read its view model, and is asked again whenever
/// something changes. A row that can change carries a number, and the dialog sends each change the user makes as a
/// <see cref="FormEdit"/> with that number.
/// </summary>
[DebuggerDisplay("FormBuilder: {Rows.Count} rows")]
internal sealed class FormBuilder
{
    /// <summary>Marks the target of a field row whose text is secret, so the dialog shows it as dots.</summary>
    internal static readonly object MaskedField = new();

    /// <summary>The number of a row that cannot change.</summary>
    private const int NoId = -1;

    /// <summary>Gets the rows added so far.</summary>
    internal List<FormRow> Rows { get; } = [];

    /// <summary>Adds bold text that names a group of rows.</summary>
    /// <param name="text">The text.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Heading(string text) => Rows.Add(new(FormRowKind.Heading, text, string.Empty, [], 0, true, NoId, null));

    /// <summary>Adds plain text, which wraps.</summary>
    /// <param name="text">The text.</param>
    internal void Text(string text)
    {
        if (text.Length > 0)
        {
            Rows.Add(new(FormRowKind.Text, text, string.Empty, [], 0, true, NoId, null));
        }
    }

    /// <summary>Adds a name and its value as one line of text.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Pair(string name, string value) => Text(value.Length > 0 ? $"{name}: {value}" : name);

    /// <summary>Adds a setting that is on or off.</summary>
    /// <param name="label">The setting's name.</param>
    /// <param name="on">Whether it is on.</param>
    /// <param name="id">The row's number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Toggle(string label, bool on, int id) => Rows.Add(new(FormRowKind.Toggle, label, string.Empty, [], on ? 1 : 0, true, id, null));

    /// <summary>Adds a setting with a list of values.</summary>
    /// <param name="label">The setting's name.</param>
    /// <param name="options">The values.</param>
    /// <param name="selected">The chosen value.</param>
    /// <param name="id">The row's number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Choice(string label, IReadOnlyList<string> options, int selected, int id) =>
        Rows.Add(new(FormRowKind.Choice, label, string.Empty, options, selected, options.Count > 0, id, null));

    /// <summary>Adds a setting whose few values are all shown at once.</summary>
    /// <param name="label">The setting's name.</param>
    /// <param name="options">The values.</param>
    /// <param name="selected">The chosen value.</param>
    /// <param name="id">The row's number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Options(string label, IReadOnlyList<string> options, int selected, int id) =>
        Rows.Add(new(FormRowKind.Options, label, string.Empty, options, selected, options.Count > 0, id, null));

    /// <summary>Adds a setting typed as text.</summary>
    /// <param name="label">The setting's name.</param>
    /// <param name="value">The text now.</param>
    /// <param name="id">The row's number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Field(string label, string value, int id) => Rows.Add(new(FormRowKind.Field, label, value, [], 0, true, id, null));

    /// <summary>Adds a setting typed as text that is secret, such as a key. The dialog shows dots in place of the letters.</summary>
    /// <param name="label">The setting's name.</param>
    /// <param name="value">The text now.</param>
    /// <param name="id">The row's number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Secret(string label, string value, int id) => Rows.Add(new(FormRowKind.Field, label, value, [], 0, true, id, MaskedField));

    /// <summary>Adds something to do.</summary>
    /// <param name="label">What it does.</param>
    /// <param name="enabled">Whether it can be done now.</param>
    /// <param name="id">The row's number.</param>
    /// <param name="target">What the action is done to, which comes back with the press, or <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Action(string label, bool enabled, int id, object? target) =>
        Rows.Add(new(FormRowKind.Action, label, string.Empty, [], 0, enabled, id, target));
}
