// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Times and paints passive button help on the window thread.</summary>
internal sealed class ButtonTooltip
{
    /// <summary>The time the pointer must rest before help opens.</summary>
    internal const int DelayMilliseconds = 400;

    /// <summary>The maximum number of wrapped hints retained across changes in scale, language and size.</summary>
    private const int Capacity = 128;

    /// <summary>Wrapped text by its content, font and available width. TextRuns owns the actual surfaces.</summary>
    private readonly Dictionary<TextFitKey, TooltipLayout> _layouts = [];

    /// <summary>Gets the deadline in monotonic milliseconds, or zero when no wake is needed.</summary>
    internal long Due { get; private set; }

    /// <summary>Gets a value indicating whether the current button has an open tooltip.</summary>
    internal bool Visible { get; private set; }

    /// <summary>Starts the delay when entering a button. Moving directly between open tips keeps help visible.</summary>
    /// <param name="item">The new target.</param>
    internal void Enter(ChromeItem? item)
    {
        // A button says its name. Anything else speaks only when it was given something to say, as a setting of a dialog is.
        if (item is not ({ Kind: ChromeItemKind.Button, Text.Length: > 0 } or { ShowsHelp: true, Help.Length: > 0 }))
        {
            Dismiss();
        }
        else if (!Visible)
        {
            Due = Environment.TickCount64 + DelayMilliseconds;
        }
    }

    /// <summary>Stops waiting and closes the tooltip.</summary>
    internal void Dismiss()
    {
        Due = 0;
        Visible = false;
    }

    /// <summary>Opens help once its deadline arrives.</summary>
    /// <param name="now">The monotonic time in milliseconds.</param>
    /// <returns>Whether a tooltip has just opened.</returns>
    internal bool OpenIfDue(long now)
    {
        if (Due == 0 || now < Due)
        {
            return false;
        }

        Visible = true;
        Due = 0;
        return true;
    }

    /// <summary>Draws the hint below or above its button, inside the window.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="item">The button.</param>
    /// <param name="look">The theme, font and scale.</param>
    internal void Draw(IFramePass pass, ChromeItem item, ChromeLook look)
    {
        var metrics = look.Metrics;
        var size = pass.Size;
        var padding = metrics.Padding;
        var inner = Math.Min(metrics.PopupWidth + metrics.PopupWidth, size.Width - padding - padding);
        if (inner < 1)
        {
            return;
        }

        var style = new TextStyle(metrics.TextSize, false, look.Theme.Text);
        var key = new TextFitKey(item.Help ?? item.Text, style, inner);
        var layout = Layout(key, look.Runs);
        var width = Math.Min(size.Width, layout.Width + padding + padding);
        var height = Math.Min(size.Height, (layout.LineHeight * layout.Lines.Length) + metrics.Gap + metrics.Gap);
        var x = Math.Clamp(item.Bounds.X, 0, size.Width - width);
        var below = item.Bounds.Bottom + metrics.Gap;
        var y = below + height <= size.Height ? below : Math.Max(0, item.Bounds.Y - metrics.Gap - height);
        ChromePainter.DrawBox(pass, new(x, y, width, height), look.Theme.Surface, look.Theme.Border, metrics.Line);
        var top = y + metrics.Gap;
        foreach (var line in layout.Lines)
        {
            var run = look.Runs.Get(line, style);
            if (top + run.Height <= y + height && run.Surface is { } surface)
            {
                pass.DrawSurface(surface, new(x + padding, top, run.Width, run.Height), false);
            }

            top += layout.LineHeight;
        }
    }

    /// <summary>Wraps a translation once for each display width and style.</summary>
    /// <param name="key">The text and its layout constraints.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The lines and their dimensions.</returns>
    private TooltipLayout Layout(in TextFitKey key, TextRuns runs)
    {
        if (_layouts.TryGetValue(key, out var layout))
        {
            return layout;
        }

        if (_layouts.Count >= Capacity)
        {
            _layouts.Clear();
        }

        var width = runs.Width(key.Text, key.Style);
        string[] lines;
        if (width <= key.Width)
        {
            lines = [key.Text];
        }
        else
        {
            var wrapped = new List<string>();
            ChromeText.Wrap(key.Text, key.Style, key.Width, runs, wrapped);
            lines = [..wrapped];
            width = 0;
            foreach (var line in lines)
            {
                width = Math.Max(width, runs.Width(line, key.Style));
            }
        }

        layout = new(lines, width, runs.LineHeight(key.Style));
        _layouts.Add(key, layout);
        return layout;
    }

    /// <summary>The measured lines of a tooltip.</summary>
    /// <param name="Lines">The lines in reading order.</param>
    /// <param name="Width">The width of the longest line.</param>
    /// <param name="LineHeight">The height of each line.</param>
    private readonly record struct TooltipLayout(string[] Lines, int Width, int LineHeight);
}
