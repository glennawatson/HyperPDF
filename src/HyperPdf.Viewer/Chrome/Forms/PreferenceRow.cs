// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Preferences;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>One row of the preferences window: how to read a setting from the preferences and how to write a change back.</summary>
/// <param name="Kind">What the row is.</param>
/// <param name="Label">The setting's name in English, which is looked up in the language shown.</param>
/// <param name="Options">The values of a setting with a list.</param>
/// <param name="Selected">Reads the chosen value of a list, or 1 for a switch that is on.</param>
/// <param name="Value">Reads the text of a typed setting.</param>
/// <param name="Apply">Writes a change the user made.</param>
[DebuggerDisplay("PreferenceRow: {Kind} {Label}")]
internal sealed record PreferenceRow(
    FormRowKind Kind,
    string Label,
    IReadOnlyList<string> Options,
    Func<PreferencesViewModel, int> Selected,
    Func<PreferencesViewModel, string> Value,
    Action<PreferencesViewModel, FormEdit> Apply)
{
    /// <summary>Gets a value indicating whether a field row's text is secret and shown as dots.</summary>
    internal bool Masked { get; init; }

    /// <summary>Makes the name of a group of settings.</summary>
    /// <param name="label">The name in English.</param>
    /// <returns>The row.</returns>
    internal static PreferenceRow Heading(string label) =>
        new(FormRowKind.Heading, label, [], static _ => 0, static _ => string.Empty, static (_, _) => { });

    /// <summary>Makes a setting with a list of values.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="options">The values.</param>
    /// <param name="get">Reads the chosen value.</param>
    /// <param name="set">Writes the chosen value.</param>
    /// <returns>The row.</returns>
    internal static PreferenceRow Choice(string label, IReadOnlyList<string> options, Func<PreferencesViewModel, int> get, Action<PreferencesViewModel, int> set) =>
        new(FormRowKind.Choice, label, options, get, static _ => string.Empty, (preferences, edit) => set(preferences, edit.Index));

    /// <summary>Makes a setting that is on or off.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads whether it is on.</param>
    /// <param name="set">Turns it on or off.</param>
    /// <returns>The row.</returns>
    internal static PreferenceRow Toggle(string label, Func<PreferencesViewModel, bool> get, Action<PreferencesViewModel, bool> set) =>
        new(FormRowKind.Toggle, label, [], preferences => get(preferences) ? 1 : 0, static _ => string.Empty, (preferences, edit) => set(preferences, edit.On));

    /// <summary>Makes a setting typed as text that is secret, which the dialog shows as dots.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads the text.</param>
    /// <param name="set">Writes the text.</param>
    /// <returns>The row.</returns>
    internal static PreferenceRow Secret(string label, Func<PreferencesViewModel, string> get, Action<PreferencesViewModel, string> set) =>
        Field(label, get, set) with { Masked = true };

    /// <summary>Makes a setting typed as text.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads the text.</param>
    /// <param name="set">Writes the text.</param>
    /// <returns>The row.</returns>
    internal static PreferenceRow Field(string label, Func<PreferencesViewModel, string> get, Action<PreferencesViewModel, string> set) =>
        new(FormRowKind.Field, label, [], static _ => 0, get, (preferences, edit) => set(preferences, edit.Text));
}
