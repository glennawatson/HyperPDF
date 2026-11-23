// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// Turns the rows of a dialog into the lines it shows. Every setting looks like what it is: a switch is a box that is
/// ticked or empty, a value is a ring that is filled or empty, a list and a text box are boxes beside the name of their
/// setting, and something to do is a button in a box. Shape tells them apart, never colour alone.
/// </summary>
internal static class FormLines
{
    /// <summary>The number a row's index is multiplied by to make room for the index of one of its list values.</summary>
    internal const int OptionSpan = 4096;

    /// <summary>The character shown in place of each letter of a secret.</summary>
    private const char Dot = '•';

    /// <summary>Turns the rows into the lines that are shown.</summary>
    /// <param name="builder">The rows.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="fit">The text measurer, how the text looks, and the widths a whole line and a control beside a name must fit.</param>
    /// <returns>The lines.</returns>
    internal static List<FormLine> Flatten(FormBuilder builder, FormUi ui, in FormFit fit)
    {
        var lines = new List<FormLine>();
        var wrapped = new List<string>();
        for (var i = 0; i < builder.Rows.Count; i++)
        {
            var row = builder.Rows[i];
            switch (row.Kind)
            {
                case FormRowKind.Heading:
                    {
                        lines.Add(new(ChromeItemKind.Heading, OverlayAction.None, i, ChromeText.Fit(row.Label, fit.Style, fit.Width, fit.Runs), true, false));
                        break;
                    }

                case FormRowKind.Text:
                    {
                        AddText(lines, wrapped, row, i, in fit);
                        break;
                    }

                default:
                    {
                        AddSetting(lines, row, i, ui, in fit);
                        break;
                    }
            }
        }

        return lines;
    }

    /// <summary>Gets the text a field row shows: its text, or dots in place of each letter when the text is secret.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The text to show.</returns>
    private static string Shown(FormRow row) => ReferenceEquals(row.Target, FormBuilder.MaskedField) ? new string(Dot, row.Value.Length) : row.Value;

    /// <summary>Adds the lines of words: a note across the dialog, or a name with its value beside it.</summary>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="wrapped">A list to wrap the words into, emptied first.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="fit">The text measurer and the widths.</param>
    private static void AddText(List<FormLine> lines, List<string> wrapped, FormRow row, int index, in FormFit fit)
    {
        wrapped.Clear();
        var paired = row.Value.Length > 0;
        var iconRoom = !paired && row.Icon != ChromeIcon.None ? fit.IconRoom : 0;
        ChromeText.Wrap(paired ? row.Value : row.Label, fit.Style, paired ? fit.ControlWidth : Math.Max(1, fit.Width - iconRoom), fit.Runs, wrapped);
        for (var line = 0; line < wrapped.Count; line++)
        {
            // The name sits beside the first line of its value, and the lines that follow stay in the value's column.
            lines.Add(new(ChromeItemKind.Label, OverlayAction.None, index, wrapped[line], true, false)
            {
                Label = paired && line == 0 ? ChromeText.Fit(row.Label, fit.Style, fit.LabelWidth, fit.Runs) : string.Empty,
                InColumn = paired,
                Icon = !paired && line == 0 ? row.Icon : ChromeIcon.None,
            });
        }
    }

    /// <summary>Adds the lines of a row that can be pressed.</summary>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="ui">What the user has done.</param>
    /// <param name="fit">The text measurer and the widths.</param>
    private static void AddSetting(List<FormLine> lines, FormRow row, int index, FormUi ui, in FormFit fit)
    {
        switch (row.Kind)
        {
            case FormRowKind.Toggle:
                {
                    // The box is ticked or empty, so on and off differ by shape and the name needs no "On" or "Off" after it.
                    lines.Add(new(ChromeItemKind.Row, OverlayAction.PickRow, index, ChromeText.Fit(row.Label, fit.Style, fit.Width - fit.IconRoom, fit.Runs), row.Enabled, row.Selected != 0)
                    {
                        Trait = ChromeItemTrait.Switch,
                        Icon = row.Selected != 0 ? ChromeIcon.CheckOn : ChromeIcon.CheckOff,
                    });
                    break;
                }

            case FormRowKind.Choice:
                {
                    AddChoice(lines, row, index, ui.Open == index, in fit);
                    break;
                }

            case FormRowKind.Options:
                {
                    lines.Add(new(ChromeItemKind.Label, OverlayAction.None, index, ChromeText.Fit(row.Label, fit.Style, fit.Width, fit.Runs), true, false));
                    AddValues(lines, row, index, false, in fit);
                    break;
                }

            case FormRowKind.Field:
                {
                    lines.Add(new(ChromeItemKind.Field, OverlayAction.PickRow, index, ChromeText.Fit(Shown(row), fit.Style, fit.ControlWidth, fit.Runs), true, ui.Focus == index)
                    {
                        Label = ChromeText.Fit(row.Label, fit.Style, fit.LabelWidth, fit.Runs),
                    });
                    break;
                }

            default:
                {
                    lines.Add(new(ChromeItemKind.Button, OverlayAction.PickRow, index, ChromeText.Fit(row.Label, fit.Style, fit.Width - fit.IconRoom, fit.Runs), row.Enabled, false)
                    {
                        Trait = ui.Open == index ? ChromeItemTrait.Expanded : ChromeItemTrait.Collapsed,
                        Icon = row.Icon,
                        Framed = true,
                    });
                    break;
                }
        }
    }

    /// <summary>Adds a list of values: a box that shows the chosen one beside the setting's name, and the values under it while it is open.</summary>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="open">Whether the list is open.</param>
    /// <param name="fit">The text measurer and the widths.</param>
    private static void AddChoice(List<FormLine> lines, FormRow row, int index, bool open, in FormFit fit)
    {
        var chosen = (uint)row.Selected < (uint)row.Options.Count ? row.Options[row.Selected] : string.Empty;
        lines.Add(new(ChromeItemKind.Row, OverlayAction.PickRow, index, ChromeText.Fit(chosen, fit.Style, fit.ControlWidth - fit.IconRoom, fit.Runs), row.Enabled, false)
        {
            Trait = open ? ChromeItemTrait.Expanded : ChromeItemTrait.Collapsed,
            Label = ChromeText.Fit(row.Label, fit.Style, fit.LabelWidth, fit.Runs),
            Icon = ChromeIcon.Choose,
            Framed = true,
        });
        if (open)
        {
            AddValues(lines, row, index, true, in fit);
        }
    }

    /// <summary>Adds the values of a setting, each with a ring that is filled for the one chosen.</summary>
    /// <param name="lines">Receives the lines.</param>
    /// <param name="row">The row.</param>
    /// <param name="index">The row's index.</param>
    /// <param name="inColumn">Whether the values sit in the column of controls, under the box of their list.</param>
    /// <param name="fit">The text measurer and the widths.</param>
    private static void AddValues(List<FormLine> lines, FormRow row, int index, bool inColumn, in FormFit fit)
    {
        var width = (inColumn ? fit.ControlWidth : fit.Width) - fit.IconRoom;
        for (var option = 0; option < row.Options.Count; option++)
        {
            var text = ChromeText.Fit(row.Options[option], fit.Style, width, fit.Runs);
            lines.Add(new(ChromeItemKind.Row, OverlayAction.PickOption, (index * OptionSpan) + option, text, row.Enabled, option == row.Selected)
            {
                InColumn = inColumn,
                Icon = option == row.Selected ? ChromeIcon.RadioOn : ChromeIcon.RadioOff,
            });
        }
    }
}
