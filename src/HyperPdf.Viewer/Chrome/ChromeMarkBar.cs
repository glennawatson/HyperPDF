// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.ViewModels.Markup;
using HyperPdf.Viewer.Icons;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the bar of marking tools, shown under the toolbar while the user is annotating.</summary>
internal static class ChromeMarkBar
{
    /// <summary>The colours a new mark can have, in the order the colour button steps through them.</summary>
    private static readonly uint[] MarkColors =
    [
        AnnotationColors.Sand, AnnotationColors.Sage, AnnotationColors.Slate, AnnotationColors.Clay, AnnotationColors.Heather, AnnotationColors.Peach,
        AnnotationColors.Stone, AnnotationColors.Ink,
    ];

    /// <summary>Gets the tools on the bar, in the order they are shown.</summary>
    internal static ReadOnlySpan<AnnotationTool> Tools =>
    [
        AnnotationTool.Select, AnnotationTool.Highlight, AnnotationTool.Underline, AnnotationTool.StrikeOut, AnnotationTool.Draw, AnnotationTool.Note,
        AnnotationTool.Text, AnnotationTool.Rectangle, AnnotationTool.Ellipse, AnnotationTool.Arrow, AnnotationTool.Line,
    ];

    /// <summary>Adds the bar.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">The top of the bar.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The height the bar takes, which is more than one line when the window is too narrow for every button.</returns>
    internal static int Add(List<ChromeItem> items, AnnotationsViewModel marks, ChromeLook look, int top, int width, ChromeActions actions)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, default, true, false));

        // Eleven tools and their settings share one row, so they stand closer together than the main toolbar's buttons.
        var tight = metrics with { Padding = metrics.Gap + metrics.Gap };
        var row = new ChromeRow(items, look.Runs, tight, tight.Gap, top + tight.Gap, tight.ToolbarHeight - tight.Gap - tight.Gap) { Limit = width - tight.Gap, Describe = ButtonHelp.Marking };
        foreach (var tool in Tools)
        {
            // Every tool button runs the one command that picks a tool, and passes its own tool to it.
            row = row.Button(IconOf(tool), NameOf(tool), marks.SetToolCommand, new GroupChoice((int)tool, tool, marks.Tool == tool));
        }

        row = row.Button(ChromeIcon.More, UiText.Get("More…"), actions.ShowMoreMarks, true);
        row = row.Divider();

        // The colour and the width buttons step to the next one, which is worked out here so the command is given what to set.
        row = row.Button(ChromeIcon.Shape, UiText.Format("Colour: {0}", marks.ColorName), marks.SetColorCommand, new GroupChoice(0, NextColor(marks), false));
        row = row.Button(ChromeIcon.LineWidth, marks.LineWidthName, marks.SetLineWidthCommand, new GroupChoice(0, NextWidth(marks), false));
        row = row.Divider();
        row = row.Button(ChromeIcon.Undo, UiText.Get("Undo"), marks.UndoCommand, marks.CanUndo);
        row = row.Button(ChromeIcon.Redo, UiText.Get("Redo"), marks.RedoCommand, marks.CanRedo);
        row = row.Button(ChromeIcon.Close, UiText.Get("Delete"), marks.DeleteCommand, marks.Selected is not null);
        var height = row.Lines * metrics.ToolbarHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }

    /// <summary>Picks the colour after the one in use, by its name, which also recolours the mark that is picked.</summary>
    /// <param name="marks">The marking tools.</param>
    /// <returns>The name of the next colour.</returns>
    private static string NextColor(AnnotationsViewModel marks)
    {
        var colors = MarkColors.AsSpan();
        return AnnotationNames.GetColor(colors[(colors.IndexOf(marks.Color) + 1) % colors.Length]);
    }

    /// <summary>Picks the line width after the one in use, by its name.</summary>
    /// <param name="marks">The marking tools.</param>
    /// <returns>The name of the next line width.</returns>
    private static string NextWidth(AnnotationsViewModel marks)
    {
        var widths = AnnotationsViewModel.LineWidths;
        var at = 0;
        while (at < widths.Length && widths[at].Value < marks.LineWidth)
        {
            at++;
        }

        return widths[(at + 1) % widths.Length].NameKey;
    }

    /// <summary>Gets the icon of a tool.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The icon.</returns>
    private static ChromeIcon IconOf(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Select => ChromeIcon.Cursor,
        AnnotationTool.Highlight => ChromeIcon.Highlight,
        AnnotationTool.Underline => ChromeIcon.Underline,
        AnnotationTool.StrikeOut => ChromeIcon.Strike,
        AnnotationTool.Draw => ChromeIcon.Draw,
        AnnotationTool.Note => ChromeIcon.Note,
        AnnotationTool.Text => ChromeIcon.Text,
        AnnotationTool.Rectangle => ChromeIcon.Box,
        AnnotationTool.Ellipse => ChromeIcon.Oval,
        AnnotationTool.Arrow => ChromeIcon.Arrow,
        _ => ChromeIcon.Line,
    };

    /// <summary>Gets the name of a tool as its button shows it.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The name.</returns>
    private static string NameOf(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Select => UiText.Get("Select"),
        AnnotationTool.Highlight => UiText.Get("Highlight"),
        AnnotationTool.Underline => UiText.Get("Underline"),
        AnnotationTool.StrikeOut => UiText.Get("Strike out"),
        AnnotationTool.Draw => UiText.Get("Draw"),
        AnnotationTool.Note => UiText.Get("Note"),
        AnnotationTool.Text => UiText.Get("Text"),
        AnnotationTool.Rectangle => UiText.Get("Box"),
        AnnotationTool.Ellipse => UiText.Get("Oval"),
        AnnotationTool.Arrow => UiText.Get("Arrow"),
        _ => UiText.Get("Line"),
    };
}
