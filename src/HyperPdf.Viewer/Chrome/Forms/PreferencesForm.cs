// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer.Icons;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// Describes the preferences window. Every change applies straight away and is kept for next time, as the view model does
/// it. Each setting says what it does and what a new install starts it as, a setting that has been changed has a button
/// that puts it back, and one button puts them all back.
/// </summary>
internal static class PreferencesForm
{
    /// <summary>The rows of the window, top to bottom.</summary>
    private static readonly PreferenceRow[] Rows = PreferenceRows.All;

    /// <summary>The number that comes back from the button that puts every setting back.</summary>
    private static readonly int ResetAllId = Rows.Length;

    /// <summary>The number that comes back from the button that brings the settings back from before that.</summary>
    private static readonly int RestoreId = Rows.Length + 1;

    /// <summary>Makes the window for a preferences view model.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>What the window shows.</returns>
    internal static FormRequest For(PreferencesViewModel preferences)
    {
        var resets = new PreferenceResets();
        return new(
            UiText.Get("Preferences"),
            form => Describe(form, preferences, resets),
            null,
            null,
            Signal.Merge(preferences.GetChangedObservable().AsSignal(), preferences.Sharpness.GetChangedObservable().AsSignal()),
            null,
            edits => edits.SubscribeSafe(edit => Apply(preferences, resets, edit), ErrorTrace.Write));
    }

    /// <summary>Adds the rows, reading each setting as it is now.</summary>
    /// <param name="form">Receives the rows.</param>
    /// <param name="preferences">The preferences.</param>
    /// <param name="resets">What the window remembers from before the settings were put back.</param>
    private static void Describe(FormBuilder form, PreferencesViewModel preferences, PreferenceResets resets)
    {
        form.Text(UiText.Get("Changes apply straight away and are kept for next time. Rest the pointer on a setting to read what it does."));

        // Both buttons are always there, so nothing below them moves when one of them becomes usable.
        form.Action(ChromeIcon.Undo, UiText.Get("Put every setting back to its default"), AnyChanged(preferences), ResetAllId);
        form.Action(ChromeIcon.Redo, UiText.Get("Bring my settings back from before that"), resets.Before.Count > 0, RestoreId);
        for (var i = 0; i < Rows.Length; i++)
        {
            var row = Rows[i];
            if (row.Visible is { } visible && !visible(preferences))
            {
                continue;
            }

            var label = row.Label.Length > 0 ? UiText.Get(row.Label) : string.Empty;
            switch (row.Kind)
            {
                case FormRowKind.Text:
                    {
                        form.Text(row.Value(preferences));
                        break;
                    }

                case FormRowKind.Choice:
                    {
                        AddChoice(form, row, label, preferences, i);
                        break;
                    }

                case FormRowKind.Toggle:
                    {
                        AddToggle(form, row, label, preferences, i);
                        break;
                    }

                case FormRowKind.Field:
                    {
                        AddField(form, row, label, preferences, i);
                        break;
                    }

                default:
                    {
                        form.Heading(label);
                        break;
                    }
            }
        }
    }

    /// <summary>Adds a setting chosen from a list. The list holds only what this computer can use, and marks the value a new install starts with.</summary>
    /// <param name="form">Receives the row.</param>
    /// <param name="row">The setting.</param>
    /// <param name="label">The setting's name in the language shown.</param>
    /// <param name="preferences">The preferences.</param>
    /// <param name="id">The row's place in the list of rows.</param>
    private static void AddChoice(FormBuilder form, PreferenceRow row, string label, PreferencesViewModel preferences, int id)
    {
        var selected = row.Selected(preferences);
        var normal = row.Selected(preferences.Defaults);
        var options = new List<string>(row.Options.Count);
        var shown = -1;
        for (var value = 0; value < row.Options.Count; value++)
        {
            if (!IsOffered(row, value, selected))
            {
                continue;
            }

            if (value == selected)
            {
                shown = options.Count;
            }

            options.Add(value == normal ? UiText.Format("{0} (default)", row.Options[value]) : row.Options[value]);
        }

        form.Choice(label, options, shown, id);

        // The open list marks the default. The closed box shows the value alone, so a window of defaults is not a wall of the same word.
        form.Showing((uint)selected < (uint)row.Options.Count ? row.Options[selected] : string.Empty);
        Explain(form, row, (uint)normal < (uint)row.Options.Count ? row.Options[normal] : string.Empty, selected != normal);
    }

    /// <summary>Adds a setting that is on or off.</summary>
    /// <param name="form">Receives the row.</param>
    /// <param name="row">The setting.</param>
    /// <param name="label">The setting's name in the language shown.</param>
    /// <param name="preferences">The preferences.</param>
    /// <param name="id">The row's place in the list of rows.</param>
    private static void AddToggle(FormBuilder form, PreferenceRow row, string label, PreferencesViewModel preferences, int id)
    {
        var selected = row.Selected(preferences);
        var normal = row.Selected(preferences.Defaults);
        form.Toggle(label, selected != 0, id);
        Explain(form, row, UiText.Get(normal != 0 ? "On" : "Off"), selected != normal);
    }

    /// <summary>Adds a setting typed as text.</summary>
    /// <param name="form">Receives the row.</param>
    /// <param name="row">The setting.</param>
    /// <param name="label">The setting's name in the language shown.</param>
    /// <param name="preferences">The preferences.</param>
    /// <param name="id">The row's place in the list of rows.</param>
    private static void AddField(FormBuilder form, PreferenceRow row, string label, PreferencesViewModel preferences, int id)
    {
        var value = row.Value(preferences);
        if (row.Masked)
        {
            form.Secret(label, value, id);
        }
        else
        {
            form.Field(label, value, id);
        }

        var normal = row.Value(preferences.Defaults);
        Explain(form, row, normal, !string.Equals(value, normal, StringComparison.Ordinal));
    }

    /// <summary>Gives the row just added what its setting does, the value it starts with, and the button that puts it back while it differs.</summary>
    /// <param name="form">The rows.</param>
    /// <param name="row">The setting.</param>
    /// <param name="normal">The value a new install starts with, as it is shown.</param>
    /// <param name="changed">Whether the setting differs from that value now.</param>
    private static void Explain(FormBuilder form, PreferenceRow row, string normal, bool changed)
    {
        var help = row.Help.Length > 0 ? UiText.Get(row.Help) : string.Empty;
        if (!row.Resets)
        {
            form.Explain(help.Length > 0 ? help : null, false, null);
            return;
        }

        var starts = UiText.Format("Default: {0}.", normal);
        form.Explain(help.Length > 0 ? $"{help} {starts}" : starts, true, changed ? UiText.Format("Put this back to its default: {0}", normal) : null);
    }

    /// <summary>Tells whether a value of a list is shown: one this computer can use, or the one in use, which is never hidden.</summary>
    /// <param name="row">The setting.</param>
    /// <param name="value">The value.</param>
    /// <param name="selected">The value in use.</param>
    /// <returns><see langword="true"/> when it is shown.</returns>
    private static bool IsOffered(PreferenceRow row, int value, int selected) => value == selected || row.Offered is not { } offered || offered(value);

    /// <summary>Finds which value of a setting a place in its shown list stands for, since the list leaves out what this computer cannot use.</summary>
    /// <param name="row">The setting.</param>
    /// <param name="selected">The value in use.</param>
    /// <param name="place">The place in the shown list.</param>
    /// <returns>The value, or the value in use when the place is in no list.</returns>
    private static int ValueAt(PreferenceRow row, int selected, int place)
    {
        var at = 0;
        for (var value = 0; value < row.Options.Count; value++)
        {
            if (!IsOffered(row, value, selected))
            {
                continue;
            }

            if (at == place)
            {
                return value;
            }

            at++;
        }

        return selected;
    }

    /// <summary>Tells whether a setting differs from the value a new install starts with.</summary>
    /// <param name="row">The setting.</param>
    /// <param name="preferences">The preferences.</param>
    /// <returns><see langword="true"/> when it differs.</returns>
    private static bool Differs(PreferenceRow row, PreferencesViewModel preferences) =>
        row.Resets
        && (row.Selected(preferences) != row.Selected(preferences.Defaults) || !string.Equals(row.Value(preferences), row.Value(preferences.Defaults), StringComparison.Ordinal));

    /// <summary>Tells whether any setting differs from the value a new install starts with.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool AnyChanged(PreferencesViewModel preferences)
    {
        foreach (var row in Rows)
        {
            if (Differs(row, preferences))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes a change the user made to the preferences.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="resets">What the window remembers from before the settings were put back.</param>
    /// <param name="edit">The change.</param>
    private static void Apply(PreferencesViewModel preferences, PreferenceResets resets, FormEdit edit)
    {
        if (edit.Id == ResetAllId)
        {
            ResetAll(preferences, resets);
            return;
        }

        if (edit.Id == RestoreId)
        {
            Restore(preferences, resets);
            return;
        }

        if ((uint)edit.Id >= (uint)Rows.Length)
        {
            return;
        }

        var row = Rows[edit.Id];
        if (ReferenceEquals(edit.Target, FormBuilder.ResetRequest))
        {
            Set(row, preferences, row.Selected(preferences.Defaults), row.Value(preferences.Defaults));
            return;
        }

        // A list shows only what this computer can use, so a place in it is turned back into the value it stands for.
        row.Apply(preferences, row.Kind == FormRowKind.Choice ? edit with { Index = ValueAt(row, row.Selected(preferences), edit.Index) } : edit);
    }

    /// <summary>Gives a setting a value.</summary>
    /// <param name="row">The setting.</param>
    /// <param name="preferences">The preferences.</param>
    /// <param name="index">The value of a list, or 1 for a switch that is on.</param>
    /// <param name="text">The text of a typed setting.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Set(PreferenceRow row, PreferencesViewModel preferences, int index, string text) => row.Apply(preferences, new(0, index, text, null));

    /// <summary>Puts every setting back to the value a new install starts with, and remembers what each was so that can be undone.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="resets">Receives what the changed settings were.</param>
    private static void ResetAll(PreferencesViewModel preferences, PreferenceResets resets)
    {
        // Every value is noted before any is changed, because putting one setting back can move another, as a set of sharpness choices does.
        resets.Before.Clear();
        for (var i = 0; i < Rows.Length; i++)
        {
            if (Differs(Rows[i], preferences))
            {
                resets.Before.Add(new(i, Rows[i].Selected(preferences), Rows[i].Value(preferences)));
            }
        }

        foreach (var row in Rows)
        {
            if (Differs(row, preferences))
            {
                Set(row, preferences, row.Selected(preferences.Defaults), row.Value(preferences.Defaults));
            }
        }
    }

    /// <summary>Brings the settings back to what they were before every one was put back.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <param name="resets">What the settings were, which is forgotten once they are back.</param>
    private static void Restore(PreferencesViewModel preferences, PreferenceResets resets)
    {
        foreach (var kept in resets.Before)
        {
            Set(Rows[kept.Row], preferences, kept.Index, kept.Text);
        }

        resets.Before.Clear();
    }
}
