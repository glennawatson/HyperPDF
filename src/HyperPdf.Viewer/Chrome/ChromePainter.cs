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

    /// <summary>
    /// Draws a list of controls that is the same in most frames, such as the bars and the side panel. A pass that keeps pixels
    /// between frames is first shown what the controls would draw, and puts back the pixels it kept when that is draw for draw
    /// what it kept them from. Otherwise the controls are drawn, and the pass keeps what they leave.
    /// </summary>
    /// <param name="pass">The frame.</param>
    /// <param name="items">The controls.</param>
    /// <param name="look">The text, colours and sizes to draw with.</param>
    internal static void DrawKept(IFramePass pass, List<ChromeItem> items, ChromeLook look)
    {
        if (pass.BeginKeptTrial())
        {
            Draw(pass, items, look);
            if (pass.EndKeptTrial())
            {
                return;
            }
        }

        pass.BeginKeptGroup();
        Draw(pass, items, look);
        pass.EndKeptGroup();
    }

    /// <summary>Draws a list of controls, back to front.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="items">The controls.</param>
    /// <param name="look">The text, colours and sizes to draw with.</param>
    internal static void Draw(IFramePass pass, List<ChromeItem> items, ChromeLook look)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item.Framed)
            {
                // A control in a dialog is boxed at rest, so it is told apart from the words around it without pointing at it.
                DrawFrame(pass, item, look);
            }

            DrawBackground(pass, item, look);
            DrawText(pass, item, look);
        }
    }

    /// <summary>Draws a ring round the control the keyboard is on, in the accent colour and twice the weight of a border.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The colours and sizes.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DrawFocus(IFramePass pass, ChromeItem item, ChromeLook look) =>
        DrawBorder(pass, item.Bounds, look.Theme.Accent, look.Metrics.Line * MarkLines);

    /// <summary>Draws the hover border over the cached controls without obscuring their labels.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control under the pointer.</param>
    /// <param name="look">The window theme and scale.</param>
    /// <param name="pressed">Whether the button is held down.</param>
    internal static void DrawHover(IFramePass pass, ChromeItem item, ChromeLook look, bool pressed)
    {
        if (item.Kind == ChromeItemKind.Button)
        {
            pass.FillRect(item.Bounds, look.Theme.ButtonHover != 0 ? look.Theme.ButtonHover : look.Theme.Surface);
            DrawState(pass, item, look);
            DrawText(pass, item, look);
        }
        else if (item.Kind == ChromeItemKind.Row)
        {
            // Menu shortcuts and list decorations can be separate controls drawn over the row.
            pass.FillRect(item.Bounds, look.Theme.Hover);
        }

        DrawBorder(pass, item.Bounds, pressed ? look.Theme.Accent : look.Theme.Border, pressed ? look.Metrics.Line * MarkLines : look.Metrics.Line);
    }

    /// <summary>Draws a filled box with a border.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="bounds">The box.</param>
    /// <param name="fill">The fill colour.</param>
    /// <param name="border">The border colour.</param>
    /// <param name="line">The border thickness.</param>
    internal static void DrawBox(IFramePass pass, in LayoutRect bounds, uint fill, uint border, int line)
    {
        pass.FillRect(bounds, border);
        pass.FillRect(new(bounds.X + line, bounds.Y + line, bounds.Width - line - line, bounds.Height - line - line), fill);
    }

    /// <summary>Gets the space between an icon and the text after it.</summary>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The space in device pixels.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IconGap(in ChromeMetrics metrics) => metrics.Gap + (metrics.Gap >> 1);

    /// <summary>
    /// Gets the room after the words of a button that opens a menu, for the mark that says so. A button whose own icon is
    /// already that mark, as the zoom button's is, gets no second one.
    /// </summary>
    /// <param name="icon">The button's own icon.</param>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The room in device pixels, or 0 for a button that needs no mark.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MenuMarkRoom(ChromeIcon icon, in ChromeMetrics metrics) => icon == ChromeIcon.PageDown ? 0 : metrics.Gap + metrics.TextSize;

    /// <summary>
    /// Draws the box of a control that is boxed at rest. The button that agrees to a dialog is filled, and has a tick before its
    /// words, so it is told from the button that backs out by more than colour. The ring of the keyboard's place stays its own.
    /// </summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The colours and sizes.</param>
    private static void DrawFrame(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var theme = look.Theme;
        if (!item.Enabled)
        {
            DrawBox(pass, item.Bounds, theme.Surface, theme.Faded, look.Metrics.Line);
            return;
        }

        // The fill is the opaque colour of a chosen thing; the hover wash is see-through and would grey the words over a border.
        var agrees = item.Action == OverlayAction.Accept && theme.ButtonHover != 0;
        DrawBox(pass, item.Bounds, agrees ? theme.ButtonHover : theme.Surface, theme.Border, look.Metrics.Line);
    }

    /// <summary>
    /// Draws the box of a text box. One filled in on the page brings its own paper, and gets a heavier outline that stands
    /// out from the page.
    /// </summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The text box.</param>
    /// <param name="look">The colours and sizes.</param>
    private static void DrawField(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var onPage = item.Ground != 0;
        var line = onPage ? look.Metrics.Line * MarkLines : look.Metrics.Line;
        DrawBox(pass, item.Bounds, onPage ? item.Ground : look.Theme.Surface, item.Selected ? look.Theme.Accent : look.Theme.Border, line);
    }

    /// <summary>Draws what lies behind the text of a control.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    private static void DrawBackground(IFramePass pass, ChromeItem item, ChromeLook look)
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
                    DrawBox(pass, bounds, theme.Surface, theme.Border, line * MarkLines);
                    break;
                }

            case ChromeItemKind.Field:
                {
                    DrawField(pass, item, look);
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
                    DrawState(pass, item, look);
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

    /// <summary>Marks the selected control of a group.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The control.</param>
    /// <param name="look">The text, colours and sizes.</param>
    private static void DrawState(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        if (!item.Selected || !item.IsPressable || !item.Enabled)
        {
            return;
        }

        if (item.Icon is ChromeIcon.CheckOn or ChromeIcon.RadioOn)
        {
            // A ticked box or a filled ring already says the row is chosen, so the row gets no band or bar as well.
            return;
        }

        if (item.Trait == ChromeItemTrait.Switch)
        {
            pass.FillRect(item.Bounds, look.Theme.Hover);
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
        if (item.Text.Length == 0 && item.Icon == ChromeIcon.None && item.Kind != ChromeItemKind.Field)
        {
            return;
        }

        var metrics = look.Metrics;
        TextStyle style = new(item.TextSize > 0 ? item.TextSize : metrics.TextSize, item.Kind == ChromeItemKind.Heading, TextColorOf(item, look)) { Serif = item.Serif };
        var run = look.Runs.Get(item.HidesText ? string.Empty : item.Text, style);
        var bounds = item.Bounds;
        var menuMark = item is { Kind: ChromeItemKind.Button, Trait: ChromeItemTrait.OpensMore, HidesText: false } ? MenuMarkRoom(item.Icon, metrics) : 0;
        var left = DrawIcon(pass, item, look, run.Width + menuMark);
        var top = bounds.Y + Math.Floor((bounds.Height - run.Height) / MarkLines);
        if (run.Surface is { } surface)
        {
            pass.DrawSurface(surface, new(left, top, run.Width, run.Height), false);
        }

        if (menuMark > 0)
        {
            DrawMenuMark(pass, look, new(left + run.Width + metrics.Gap, bounds.Y + Math.Floor((bounds.Height - metrics.TextSize) / MarkLines), metrics.TextSize, metrics.TextSize), style.Color);
        }

        if (item is { Kind: ChromeItemKind.Field, Selected: true, HidesText: false })
        {
            // The caret sits after the text, where the next typed character goes.
            pass.FillRect(new(left + run.Width, bounds.Y + metrics.Gap, metrics.Line * MarkLines, bounds.Height - metrics.Gap - metrics.Gap), style.Color);
        }
    }

    /// <summary>
    /// Draws the mark that says a button opens a menu. It is an icon: a triangle in the words is in no font that ships with
    /// the viewer, so drawing one sent the window to read the system's font files as it opened.
    /// </summary>
    /// <param name="pass">The frame.</param>
    /// <param name="look">The icons.</param>
    /// <param name="place">Where the mark goes.</param>
    /// <param name="color">The colour of the button's words.</param>
    private static void DrawMenuMark(IFramePass pass, ChromeLook look, in LayoutRect place, uint color)
    {
        if (look.Icons?.Get(ChromeIcon.Choose, (int)place.Width, color) is { } mark)
        {
            pass.DrawSurface(mark, place, false);
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

    /// <summary>Draws an outline inside a control.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="bounds">The control bounds.</param>
    /// <param name="color">The outline colour.</param>
    /// <param name="weight">The outline thickness.</param>
    private static void DrawBorder(IFramePass pass, in LayoutRect bounds, uint color, int weight)
    {
        pass.FillRect(new(bounds.X, bounds.Y, bounds.Width, weight), color);
        pass.FillRect(new(bounds.X, bounds.Bottom - weight, bounds.Width, weight), color);
        pass.FillRect(new(bounds.X, bounds.Y, weight, bounds.Height), color);
        pass.FillRect(new(bounds.Right - weight, bounds.Y, weight, bounds.Height), color);
    }
}
