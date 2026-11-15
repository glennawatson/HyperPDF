// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// One row of a window described as a table: how to read the row from a view model, and how a change the user makes is written
/// back or which command it runs. A window is a list of these, so it is laid out and bound the same way as every other window.
/// </summary>
/// <typeparam name="T">The view model the window shows.</typeparam>
/// <param name="Kind">What the row is.</param>
/// <param name="Label">Reads the row's text, or the name of its setting.</param>
/// <param name="Options">Reads the values of a setting with a list.</param>
/// <param name="Selected">Reads the chosen value of a list, or 1 for a switch that is on.</param>
/// <param name="Value">Reads the text of a typed setting.</param>
/// <param name="Apply">Writes a change the user made.</param>
/// <param name="Command">Finds the command an action runs, or <see langword="null"/> for a row that is not an action.</param>
/// <param name="Visible">Tells whether the row is shown now.</param>
/// <param name="Enabled">Tells whether the row can be changed or pressed now.</param>
[DebuggerDisplay("FormSetting: {Kind}")]
internal sealed record FormSetting<T>(
    FormRowKind Kind,
    Func<T, string> Label,
    Func<T, IReadOnlyList<string>> Options,
    Func<T, int> Selected,
    Func<T, string> Value,
    Action<T, FormEdit> Apply,
    Func<T, ICommand>? Command,
    Func<T, bool> Visible,
    Func<T, bool> Enabled)
{
    /// <summary>Gets what reads the items of a row that stands for a list, one line for each item, or <see langword="null"/> for a single row.</summary>
    internal Func<T, IReadOnlyList<object>>? Items { get; init; }

    /// <summary>Gets what reads the text of one item of a list.</summary>
    internal Func<object, string> ItemText { get; init; } = static item => item.ToString() ?? string.Empty;

    /// <summary>Makes a row with another rule for when it is shown.</summary>
    /// <param name="visible">Tells whether the row is shown now.</param>
    /// <returns>The row.</returns>
    internal FormSetting<T> When(Func<T, bool> visible) => this with { Visible = visible };

    /// <summary>Makes a row with another rule for when it can be changed or pressed.</summary>
    /// <param name="enabled">Tells whether the row can be changed or pressed now.</param>
    /// <returns>The row.</returns>
    internal FormSetting<T> EnabledWhen(Func<T, bool> enabled) => this with { Enabled = enabled };
}
