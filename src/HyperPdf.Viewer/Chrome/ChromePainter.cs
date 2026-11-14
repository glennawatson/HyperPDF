// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Draws laid out controls into a frame.</summary>
internal static class ChromePainter
{
    /// <summary>The lines of thickness of the mark on the chosen tab and the typing caret.</summary>
    private const int MarkLines = 2;

    /// <summary>Draws a list of controls, back to front.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="items">The controls.</param>
    /// <param name="look">The text, colours and sizes to draw with.</param>
    /// <param name="hover">The control under the pointer, or <see langword="null"/>.</param>
    internal static void Draw(IFramePass pass, List<ChromeItem> items, ChromeLook look, ChromeItem? hover)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            DrawBackground(pass, item, look, ReferenceEquals(item, hover));
            DrawText(pass, item, look);
        }
    }

    /// <summary>Draws what lies behind the text of a control.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    /// <param name="hovered">Whether the pointer is over it.</param>
    private static void DrawBackground(IFramePass pass, ChromeItem item, ChromeLook look, bool hovered)
    {
        var theme = look.Theme;
        var line = look.Metrics.Line;
        var bounds = item.Bounds;
        switch (item.Kind)
        {
            case ChromeItemKind.Panel:
                {
                    pass.FillRect(bounds, theme.Bar);
                    pass.FillRect(new(bounds.X, bounds.Bottom - line, bounds.Width, line), theme.Border);
                    break;
                }

            case ChromeItemKind.Popup:
                {
                    DrawBox(pass, bounds, theme.Surface, theme.Border, line);
                    break;
                }

            case ChromeItemKind.Field:
                {
                    DrawBox(pass, bounds, theme.Surface, item.Selected ? theme.Accent : theme.Border, line);
                    break;
                }

            case ChromeItemKind.Separator:
                {
                    pass.FillRect(new(bounds.X, bounds.Y + Math.Floor(bounds.Height / MarkLines), bounds.Width, line), theme.Border);
                    break;
                }

            case ChromeItemKind.Tab when item.Selected:
                {
                    pass.FillRect(bounds, theme.Surface);
                    pass.FillRect(new(bounds.X, bounds.Y, bounds.Width, line * MarkLines), theme.Accent);
                    break;
                }

            default:
                {
                    DrawState(pass, item, look, hovered);
                    break;
                }
        }
    }

    /// <summary>Draws the wash of a control under the pointer and the mark of a chosen one.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    /// <param name="hovered">Whether the pointer is over it.</param>
    private static void DrawState(IFramePass pass, ChromeItem item, ChromeLook look, bool hovered)
    {
        if (item.Command == ChromeCommand.None || !item.Enabled)
        {
            return;
        }

        if (hovered)
        {
            pass.FillRect(item.Bounds, look.Theme.Hover);
        }

        if (item.Selected && item.Kind == ChromeItemKind.Row)
        {
            pass.FillRect(new(item.Bounds.X, item.Bounds.Y, look.Metrics.Line * MarkLines, item.Bounds.Height), look.Theme.Accent);
        }
    }

    /// <summary>Draws the text of a control: in the middle of a button, on the left of everything else.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    private static void DrawText(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var metrics = look.Metrics;
        var color = item.Enabled ? look.Theme.Text : look.Theme.Faded;
        var run = look.Runs.Get(item.Text, new(metrics.TextSize, item.Kind == ChromeItemKind.Heading, color));
        var bounds = item.Bounds;
        var left = item.Kind == ChromeItemKind.Button ? bounds.X + Math.Floor((bounds.Width - run.Width) / MarkLines) : bounds.X + metrics.Padding;
        var top = bounds.Y + Math.Floor((bounds.Height - run.Height) / MarkLines);
        if (run.Surface is { } surface)
        {
            pass.DrawSurface(surface, new(left, top, run.Width, run.Height), false);
        }

        if (item is { Kind: ChromeItemKind.Field, Selected: true })
        {
            // The caret sits after the text, where the next typed character goes.
            pass.FillRect(new(left + run.Width, bounds.Y + metrics.Gap, metrics.Line * MarkLines, bounds.Height - metrics.Gap - metrics.Gap), look.Theme.Text);
        }
    }

    /// <summary>Draws a filled box with a border.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="bounds">The box.</param>
    /// <param name="fill">The fill colour.</param>
    /// <param name="border">The border colour.</param>
    /// <param name="line">The border thickness.</param>
    private static void DrawBox(IFramePass pass, in LayoutRect bounds, uint fill, uint border, int line)
    {
        pass.FillRect(bounds, border);
        pass.FillRect(new(bounds.X + line, bounds.Y + line, bounds.Width - line - line, bounds.Height - line - line), fill);
    }
}
