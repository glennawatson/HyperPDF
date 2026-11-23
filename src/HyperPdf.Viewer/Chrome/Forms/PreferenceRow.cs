// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <summary>Gets a rule telling whether the row is shown right now, or <see langword="null"/> when it always is.</summary>
    internal Func<PreferencesViewModel, bool>? Visible { get; init; }

    /// <summary>Makes a section heading.</summary>
    /// <param name="label">The heading in English.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PreferenceRow Heading(string label) =>
        new(FormRowKind.Heading, label, [], static _ => 0, static _ => string.Empty, static (_, _) => { });

    /// <summary>Makes a setting chosen from a list.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="options">The choices.</param>
    /// <param name="get">Reads the chosen index.</param>
    /// <param name="set">Writes the chosen index.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PreferenceRow Choice(string label, IReadOnlyList<string> options, Func<PreferencesViewModel, int> get, Action<PreferencesViewModel, int> set) =>
        new(FormRowKind.Choice, label, options, get, static _ => string.Empty, (preferences, edit) => set(preferences, edit.Index));

    /// <summary>Makes a setting that is either on or off.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads whether it is on.</param>
    /// <param name="set">Writes whether it is on.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PreferenceRow Toggle(string label, Func<PreferencesViewModel, bool> get, Action<PreferencesViewModel, bool> set) =>
        new(FormRowKind.Toggle, label, [], preferences => get(preferences) ? 1 : 0, static _ => string.Empty, (preferences, edit) => set(preferences, edit.On));

    /// <summary>Makes a setting typed as text.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads the text.</param>
    /// <param name="set">Writes the text.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PreferenceRow Field(string label, Func<PreferencesViewModel, string> get, Action<PreferencesViewModel, string> set) =>
        new(FormRowKind.Field, label, [], static _ => 0, get, (preferences, edit) => set(preferences, edit.Text));

    /// <summary>Makes a secret setting typed as text and shown as dots.</summary>
    /// <param name="label">The name in English.</param>
    /// <param name="get">Reads the secret.</param>
    /// <param name="set">Writes the secret.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PreferenceRow Secret(string label, Func<PreferencesViewModel, string> get, Action<PreferencesViewModel, string> set) =>
        new(FormRowKind.Field, label, [], static _ => 0, get, (preferences, edit) => set(preferences, edit.Text)) { Masked = true };

    /// <summary>Makes a dynamic status note read from the preferences.</summary>
    /// <param name="text">Reads the text.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PreferenceRow Status(Func<PreferencesViewModel, string> text) =>
        new(FormRowKind.Text, string.Empty, [], static _ => 0, text, static (_, _) => { });

    /// <summary>Makes a copy of the row that is shown only when a condition holds.</summary>
    /// <param name="visible">Tells whether the row is shown.</param>
    /// <returns>The row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PreferenceRow When(Func<PreferencesViewModel, bool> visible) => this with { Visible = visible };
}
