// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Tracks pointer feedback independently of the cached window controls.</summary>
/// <param name="window">The window to repaint when feedback changes.</param>
internal sealed class ChromePointer(IPlatformWindow window)
{
    /// <summary>The delayed button help.</summary>
    private readonly ButtonTooltip _tooltip = new();

    /// <summary>The hover targets in the bars, in paint order.</summary>
    private readonly List<ChromeItem> _barTargets = [];

    /// <summary>The hover targets in the open overlay, in paint order.</summary>
    private readonly List<ChromeItem> _overlayTargets = [];

    /// <summary>The control where the left button went down.</summary>
    private ChromeItem? _pressed;

    /// <summary>Gets the frontmost control under the pointer.</summary>
    internal ChromeItem? Hover { get; private set; }

    /// <summary>Finds the control at a point that can be pressed.</summary>
    /// <param name="items">The controls, back to front.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns>The control in front, or <see langword="null"/>.</returns>
    internal static ChromeItem? Find(List<ChromeItem> items, int x, int y)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.IsPressable && item.Enabled && item.Bounds.Contains(x, y))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Checks whether a point is inside the overlay or one of its popups.</summary>
    /// <param name="items">The overlay controls.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <returns>Whether the overlay covers the point.</returns>
    internal static bool OverOverlay(List<ChromeItem> items, int x, int y)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if ((i == 0 || item.Kind == ChromeItemKind.Popup) && item.Bounds.Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Shortens the next window wait when hover help is due.</summary>
    /// <param name="timeout">The requested wait, or -1 to wait indefinitely.</param>
    /// <returns>The time to wait in milliseconds.</returns>
    internal int HoverTimeout(int timeout)
    {
        var due = _tooltip.Due;
        if (due == 0)
        {
            return timeout;
        }

        var now = Environment.TickCount64;
        if (_tooltip.OpenIfDue(now))
        {
            window.RequestFrame();
            return 0;
        }

        var remaining = (int)(due - now);
        return timeout < 0 ? remaining : Math.Min(timeout, remaining);
    }

    /// <summary>Collects hover targets once per layout, including disabled buttons that offer help.</summary>
    /// <param name="items">The new layout.</param>
    /// <param name="overlay">Whether this is the open overlay.</param>
    internal void SetTargets(List<ChromeItem> items, bool overlay)
    {
        var targets = overlay ? _overlayTargets : _barTargets;
        targets.Clear();
        foreach (var item in items)
        {
            // Words that say what a setting does are a target too, so their help shows though a press does nothing.
            if (item.Kind == ChromeItemKind.Button || (item.Enabled && item.IsPressable) || item.ShowsHelp)
            {
                targets.Add(item);
            }
        }
    }

    /// <summary>Finds the top hover target at the pointer position.</summary>
    /// <param name="overlay">Whether the pointer is in the open overlay.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <returns>The hovered control.</returns>
    internal ChromeItem? Hit(bool overlay, int x, int y)
    {
        var targets = overlay ? _overlayTargets : _barTargets;
        for (var i = targets.Count - 1; i >= 0; i--)
        {
            var target = targets[i];
            if (target.Bounds.Contains(x, y))
            {
                return target;
            }
        }

        return null;
    }

    /// <summary>Follows a hit test after pointer movement or layout.</summary>
    /// <param name="item">The new control under the pointer.</param>
    /// <returns>Whether the control changed.</returns>
    internal bool Move(ChromeItem? item)
    {
        var old = Hover;
        if (ReferenceEquals(old, item))
        {
            return false;
        }

        if (!SameAction(old, item))
        {
            _tooltip.Enter(item);
        }
        else if (ReferenceEquals(old, _pressed))
        {
            _pressed = item;
        }

        Hover = item;
        return true;
    }

    /// <summary>Clears feedback when the pointer leaves the window.</summary>
    internal void Leave()
    {
        _pressed = null;
        if (Move(null))
        {
            window.RequestFrame();
        }
    }

    /// <summary>Shows the held state until the pointer is released.</summary>
    /// <param name="left">Whether the left button went down.</param>
    internal void Press(bool left)
    {
        _tooltip.Dismiss();
        _pressed = left ? Hover : null;
        if (Hover is not null)
        {
            window.RequestFrame();
        }
    }

    /// <summary>Ends the held state.</summary>
    internal void Release()
    {
        if (_pressed is null)
        {
            return;
        }

        _pressed = null;
        window.RequestFrame();
    }

    /// <summary>Dismisses help on input.</summary>
    /// <returns>Whether visible help was dismissed.</returns>
    internal bool Dismiss()
    {
        var visible = _tooltip.Visible;
        _tooltip.Dismiss();
        if (visible)
        {
            window.RequestFrame();
        }

        return visible;
    }

    /// <summary>Draws transient pointer feedback after the window content.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="look">The current window appearance.</param>
    internal void DrawHover(IFramePass pass, ChromeLook look)
    {
        // Only something that can be pressed is outlined. Words with help show their help and are otherwise left alone.
        if (Hover is not { Enabled: true } item || !(item.Kind == ChromeItemKind.Button || item.IsPressable))
        {
            return;
        }

        ChromePainter.DrawHover(pass, item, look, ReferenceEquals(item, _pressed));
    }

    /// <summary>Draws the open tooltip above other controls.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="look">The current window appearance.</param>
    internal void DrawTooltip(IFramePass pass, ChromeLook look)
    {
        if (Hover is not { } item || !(_tooltip.Visible || (_tooltip.Due != 0 && _tooltip.OpenIfDue(Environment.TickCount64))))
        {
            return;
        }

        _tooltip.Draw(pass, item, look);
    }

    /// <summary>Recognises the same action across a layout while the pointer rests.</summary>
    /// <param name="old">The previous control.</param>
    /// <param name="item">The new control.</param>
    /// <returns>Whether its help and action are unchanged.</returns>
    private static bool SameAction(ChromeItem? old, ChromeItem? item) =>
        old is not null && item is not null && old.Kind == item.Kind && old.Command == item.Command && old.Action == item.Action && old.Index == item.Index && old.Text == item.Text;
}
