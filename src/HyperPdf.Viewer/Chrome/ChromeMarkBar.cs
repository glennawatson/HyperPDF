// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels.Markup;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the bar of marking tools, shown under the toolbar while the user is annotating.</summary>
internal static class ChromeMarkBar
{
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
    /// <returns>The height the bar takes, which is more than one line when the window is too narrow for every button.</returns>
    internal static int Add(List<ChromeItem> items, AnnotationsViewModel marks, ChromeLook look, int top, int width)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, ChromeCommand.None, 0, string.Empty, default, true, false));

        // Eleven tools and their settings share one row, so they stand closer together than the main toolbar's buttons.
        var tight = metrics with { Padding = metrics.Gap + metrics.Gap };
        var row = new ChromeRow(items, look.Runs, tight, tight.Gap, top + tight.Gap, tight.ToolbarHeight - tight.Gap - tight.Gap) { Limit = width - tight.Gap };
        foreach (var tool in Tools)
        {
            row = row.Button(NameOf(tool), ChromeCommand.MarkTool, (int)tool, marks.Tool == tool);
        }

        row = row.Button(UiText.Get("More…"), ChromeCommand.MarkMore, true);
        row = row.Space();
        row = row.Button(UiText.Format("Colour: {0}", marks.ColorName), ChromeCommand.MarkColor, true);
        row = row.Button(marks.LineWidthName, ChromeCommand.MarkWidth, true);
        row = row.Space();
        row = row.Button(UiText.Get("Undo"), ChromeCommand.Undo, marks.CanUndo);
        row = row.Button(UiText.Get("Redo"), ChromeCommand.Redo, marks.CanRedo);
        row = row.Button(UiText.Get("Delete"), ChromeCommand.DeleteMark, marks.Selected is not null);
        var height = row.Lines * metrics.ToolbarHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }

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
