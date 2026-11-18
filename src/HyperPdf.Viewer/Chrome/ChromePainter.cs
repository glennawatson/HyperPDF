// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Draws laid out controls into a frame.</summary>
internal static class ChromePainter
{
    /// <summary>The lines of thickness of the mark on the chosen tab and the typing caret.</summary>
    private const int MarkLines = 2;

    /// <summary>The colour behind a page picture that is not ready yet.</summary>
    private const uint Paper = 0xFFFFFFFF;

    /// <summary>The colour of the line under a misspelled word.</summary>
    private const uint MisspelledColor = 0xFFD32F2F;

    /// <summary>The colour of the ink on a drawing pad.</summary>
    private const uint PadInkColor = 0xFF202020;

    /// <summary>The mask of the red, green and blue bits of a colour.</summary>
    private const uint ColorBits = 0x00FFFFFF;

    /// <summary>The alpha, in the top byte, that dimmed text is drawn with.</summary>
    private const uint DimmedAlpha = 0x70000000;

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

    /// <summary>Draws a ring round the control the keyboard is on, in the accent colour and twice the weight of a border.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The colours and sizes.</param>
    internal static void DrawFocus(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var weight = look.Metrics.Line * MarkLines;
        var bounds = item.Bounds;
        var color = look.Theme.Accent;
        pass.FillRect(new(bounds.X, bounds.Y, bounds.Width, weight), color);
        pass.FillRect(new(bounds.X, bounds.Bottom - weight, bounds.Width, weight), color);
        pass.FillRect(new(bounds.X, bounds.Y, weight, bounds.Height), color);
        pass.FillRect(new(bounds.Right - weight, bounds.Y, weight, bounds.Height), color);
    }

    /// <summary>Gets the space between an icon and the text after it.</summary>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The space in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IconGap(in ChromeMetrics metrics) => metrics.Gap + (metrics.Gap >> 1);

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
        if (DrawMark(pass, item, look))
        {
            return;
        }

        // A popup or a panel with a colour of its own is filled with it alone, as the page behind focus mode's text and the mark under a word are.
        if (item.Ground != 0 && item.Kind is ChromeItemKind.Popup or ChromeItemKind.Panel)
        {
            pass.FillRect(bounds, item.Ground);
            return;
        }

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
                    DrawSeparator(pass, bounds, look);
                    break;
                }

            case ChromeItemKind.Thumbnail:
                {
                    // The page's own paper shows until its picture is ready, and the page shown has a frame in the accent colour.
                    DrawBox(pass, bounds.Inflate(line * MarkLines, line * MarkLines), Paper, item.Selected ? theme.Accent : theme.Border, line * MarkLines);
                    if (look.Thumbnail(item.Index) is { } picture)
                    {
                        pass.DrawSurface(picture, bounds, true);
                    }

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

    /// <summary>Draws the controls that are not a button or a row: the line under a misspelled word, and the drawing pad.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    /// <returns><see langword="true"/> when the control is one of those and was drawn.</returns>
    private static bool DrawMark(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var bounds = item.Bounds;
        var line = look.Metrics.Line;
        if (item.Kind == ChromeItemKind.Underline)
        {
            pass.FillRect(bounds, MisspelledColor);
            return true;
        }

        if (item.Kind != ChromeItemKind.Pad)
        {
            return false;
        }

        // The strokes are quads in the frame itself, so the pad costs the CPU nothing once a stroke is made.
        DrawBox(pass, bounds, Paper, look.Theme.Border, line);
        if (look.Ink() is { } ink)
        {
            PadInks.Draw(pass, bounds, ink, PadInkColor, line * MarkLines);
        }

        return true;
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

        if (!item.Selected)
        {
            return;
        }

        // The chosen row has a bar down its left edge, and the chosen button of a group a line under it.
        var mark = look.Metrics.Line * MarkLines;
        var place = item.Kind == ChromeItemKind.Row
            ? new LayoutRect(item.Bounds.X, item.Bounds.Y, mark, item.Bounds.Height)
            : new LayoutRect(item.Bounds.X, item.Bounds.Bottom - mark, item.Bounds.Width, mark);
        pass.FillRect(place, look.Theme.Accent);
    }

    /// <summary>Draws the text of a control: in the middle of a button, on the left of everything else.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    private static void DrawText(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var metrics = look.Metrics;
        TextStyle style = new(item.TextSize > 0 ? item.TextSize : metrics.TextSize, item.Kind == ChromeItemKind.Heading, TextColorOf(item, look)) { Serif = item.Serif };
        var run = look.Runs.Get(item.HidesText ? string.Empty : item.Text, style);
        var bounds = item.Bounds;
        var left = DrawIcon(pass, item, look, run.Width);
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

    /// <summary>Draws the icon of a control, in the middle of a button with its text and on the left of everything else.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The icons, colours and sizes.</param>
    /// <param name="textWidth">The width of the text that follows the icon.</param>
    /// <returns>Where the text starts across.</returns>
    private static double DrawIcon(IFramePass pass, ChromeItem item, ChromeLook look, int textWidth)
    {
        var bounds = item.Bounds;
        var iconSize = IconSizeOf(item, look);
        var iconRoom = iconSize > 0 && textWidth > 0 ? iconSize + IconGap(look.Metrics) : iconSize;
        var centred = item.Kind == ChromeItemKind.Button || (item.Kind == ChromeItemKind.Picture && item.Text.Length == 0);
        var left = centred ? bounds.X + Math.Floor((bounds.Width - iconRoom - textWidth) / MarkLines) : bounds.X + look.Metrics.Padding;
        if (iconSize > 0 && look.Icons?.Get(item.Icon, iconSize, IconColorOf(item, look)) is { } icon)
        {
            pass.DrawSurface(icon, new(left, bounds.Y + Math.Floor((bounds.Height - iconSize) / MarkLines), iconSize, iconSize), false);
        }

        return left + iconRoom;
    }

    /// <summary>Gets the side of the icon a control is drawn with.</summary>
    /// <param name="item">The control.</param>
    /// <param name="look">The icons and the sizes.</param>
    /// <returns>The side in device pixels, or 0 for a control with no icon.</returns>
    private static int IconSizeOf(ChromeItem item, ChromeLook look)
    {
        if (item.Icon == ChromeIcon.None || look.Icons is null)
        {
            return 0;
        }

        return item.IconSize > 0 ? item.IconSize : look.Metrics.IconSize;
    }

    /// <summary>Picks the colour a control's icon is drawn in: faded when it cannot be pressed, its own tint when it has one, otherwise the tint of what the icon does.</summary>
    /// <param name="item">The control.</param>
    /// <param name="look">The colours.</param>
    /// <returns>The colour as ARGB.</returns>
    private static uint IconColorOf(ChromeItem item, ChromeLook look)
    {
        if (!item.Enabled)
        {
            return look.Theme.Faded;
        }

        return item.Tint != 0 ? item.Tint : look.Theme.IconColor(IconPaths.ToneOf(item.Icon));
    }

    /// <summary>Draws a line between controls: down the middle of a tall place, across the middle of a wide one.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="bounds">The place.</param>
    /// <param name="look">The colours and sizes.</param>
    private static void DrawSeparator(IFramePass pass, in LayoutRect bounds, ChromeLook look)
    {
        var line = look.Metrics.Line;
        if (bounds.Height > bounds.Width)
        {
            var inset = look.Metrics.Gap;
            pass.FillRect(new(bounds.X + Math.Floor(bounds.Width / MarkLines), bounds.Y + inset, line, bounds.Height - inset - inset), look.Theme.Border);
            return;
        }

        pass.FillRect(new(bounds.X, bounds.Y + Math.Floor(bounds.Height / MarkLines), bounds.Width, line), look.Theme.Border);
    }

    /// <summary>Picks the colour a control's text is drawn in: its own when it has one, otherwise the theme's, and fainter when it is dimmed.</summary>
    /// <param name="item">The control.</param>
    /// <param name="look">The colours.</param>
    /// <returns>The colour as ARGB.</returns>
    private static uint TextColorOf(ChromeItem item, ChromeLook look)
    {
        var color = look.Theme.Faded;
        if (item.Tint != 0)
        {
            color = item.Tint;
        }
        else if (item.Enabled)
        {
            color = look.Theme.Text;
        }

        return item.Dimmed ? (color & ColorBits) | DimmedAlpha : color;
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
