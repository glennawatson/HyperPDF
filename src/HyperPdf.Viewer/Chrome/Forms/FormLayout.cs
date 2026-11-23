// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// Places the lines of a dialog made of rows. The names of the settings go in one column and their controls in another,
/// a button is as wide as its words, each group of rows is ruled off from the one before, and a bar at the side shows
/// where the lines in view lie among all of them.
/// </summary>
internal static class FormLayout
{
    /// <summary>The paddings a button's box adds to the width of its words: two on each side.</summary>
    private const int ButtonPaddings = 4;

    /// <summary>The colour of the bar's track: a faint grey that shows on a light and on a dark dialog.</summary>
    private const uint TrackColor = 0x30808080;

    /// <summary>The colour of the bar's thumb: a stronger grey.</summary>
    private const uint ThumbColor = 0xB0909090;

    /// <summary>Adds the lines in view, one under another.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="lines">The lines in view.</param>
    /// <param name="row">Where the first line goes, which is as wide and as tall as every line.</param>
    /// <param name="look">The width of the column of names, the spaces around a control and what its words are measured with.</param>
    internal static void AddLines(List<ChromeItem> items, List<FormLine> lines, in LayoutRect row, in FormLineLook look)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var place = row with { Y = row.Y + (i * row.Height) };
            if (line.Kind == ChromeItemKind.Heading && i > 0)
            {
                // A rule above the name of a group sets the group off from the one before it.
                items.Add(new(ChromeItemKind.Separator, null, 0, string.Empty, new(place.X + look.Padding, place.Y, place.Width - look.Padding - look.Padding, look.Gap), false, false));
            }

            if (line.Label.Length > 0)
            {
                items.Add(new(ChromeItemKind.Label, null, line.Index, line.Label, place with { Width = look.LabelWidth }, line.Enabled, false));
            }

            items.Add(new(line.Kind, line.Action, line.Index, line.Text, BoundsOf(line, in place, in look), line.Enabled, line.Selected)
            {
                Trait = line.Trait,
                Icon = line.Icon,
                Framed = line.Framed,
                Help = line.Label.Length > 0 ? $"{line.Label}: {line.Text}" : null,
            });
        }
    }

    /// <summary>Adds a bar down the side of the lines when there are more lines than are in view, with a thumb where the lines in view lie.</summary>
    /// <param name="items">Receives the bar.</param>
    /// <param name="track">Where the bar goes.</param>
    /// <param name="first">The first line in view.</param>
    /// <param name="shown">The lines in view.</param>
    /// <param name="count">All the lines.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void AddScrollBar(List<ChromeItem> items, in LayoutRect track, int first, int shown, int count) =>
        AddScrollBar(items, in track, first, shown, count, default);

    /// <summary>Adds a bar down the side of the lines when there are more lines than are in view, in the colours of the window's theme.</summary>
    /// <param name="items">Receives the bar.</param>
    /// <param name="track">Where the bar goes.</param>
    /// <param name="first">The first line in view.</param>
    /// <param name="shown">The lines in view.</param>
    /// <param name="count">All the lines.</param>
    /// <param name="theme">The window's colours.</param>
    internal static void AddScrollBar(List<ChromeItem> items, in LayoutRect track, int first, int shown, int count, in ChromeTheme theme)
    {
        if (count <= shown || track.Height <= 0)
        {
            return;
        }

        var trackColor = theme.Bar != 0 && theme.Bar != theme.Surface ? theme.Bar : TrackColor;
        var thumbColor = theme.Faded != 0 ? theme.Faded : ThumbColor;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, track, false, false) { Ground = trackColor });
        var top = track.Y + (track.Height * first / count);
        var height = Math.Max(track.Width, track.Height * shown / count);
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, new(track.X, Math.Min(top, track.Bottom - height), track.Width, height), false, false) { Ground = thumbColor });
    }

    /// <summary>Works out where the control of a line goes.</summary>
    /// <param name="line">The line.</param>
    /// <param name="place">The whole line.</param>
    /// <param name="look">The column of names, the spaces and the measurer.</param>
    /// <returns>The control's place.</returns>
    private static LayoutRect BoundsOf(FormLine line, in LayoutRect place, in FormLineLook look)
    {
        if (line.Label.Length > 0 || line.InColumn)
        {
            // A box is set in from the line above and below it, so two boxes one under the other do not touch.
            var inset = line.Kind == ChromeItemKind.Label ? 0 : look.Gap >> 1;
            return new(place.X + look.LabelWidth, place.Y + inset, place.Width - look.LabelWidth - look.Padding - look.Padding, place.Height - inset - inset);
        }

        if (line.Kind != ChromeItemKind.Button)
        {
            return place;
        }

        var inner = place.Width - look.Padding - look.Padding;

        // An icon is as tall as its line at most, so a line's height is room enough for it beside the words.
        var iconRoom = line.Icon == ChromeIcon.None ? 0 : place.Height;
        var width = Math.Min(inner, look.Runs.Measure(line.Text, look.Style) + iconRoom + (look.Padding * ButtonPaddings));
        return new(place.X + look.Padding, place.Y + (look.Gap >> 1), width, place.Height - look.Gap);
    }
}
