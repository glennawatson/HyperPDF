// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Icons;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// What the viewer does for a window the system gives no title bar or frame, as some Wayland compositors do: the buttons that put it
/// away, make it as big as the screen and close it, and the edges that size it.
/// </summary>
internal static class WindowFrame
{
    /// <summary>The buttons the window has: put away, make bigger or smaller, close.</summary>
    private const int Buttons = 3;

    /// <summary>The presses close together that count as a double press.</summary>
    private const int DoublePress = 2;

    /// <summary>Brings what the controls are drawn with up to date with the window: whether it needs its own buttons, and whether it fills the screen.</summary>
    /// <param name="look">What the controls are drawn with.</param>
    /// <param name="window">The window.</param>
    /// <param name="fullScreen">Whether the window is in full screen, where it has no buttons.</param>
    /// <returns>The look with the window's state in it.</returns>
    internal static ChromeLook Follow(ChromeLook look, IPlatformWindow window, bool fullScreen) =>
        look with { OwnsFrame = !window.HasSystemFrame && !fullScreen, IsMaximized = window.IsMaximized };

    /// <summary>Gets the width the window's buttons take at the right of a bar.</summary>
    /// <param name="look">The sizes, and whether the window needs its own buttons.</param>
    /// <returns>The width in device pixels, or 0 when the system draws the title bar.</returns>
    internal static int ButtonsWidth(ChromeLook look) => look.OwnsFrame ? look.Metrics.TabHeight * Buttons : 0;

    /// <summary>Adds the window's buttons at the right of the bar at the top of the window.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="look">The sizes, and whether the window needs its own buttons.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    internal static void AddButtons(List<ChromeItem> items, ChromeLook look, int width, ChromeActions actions)
    {
        if (!look.OwnsFrame)
        {
            return;
        }

        var side = look.Metrics.TabHeight;
        var x = width - (side * Buttons);
        items.Add(new(ChromeItemKind.Button, actions.Minimize, 0, UiText.Get("Minimise"), new(x, 0, side, side), true, false) { Icon = ChromeIcon.Minimize, HidesText = true });
        var grow = look.IsMaximized ? UiText.Get("Restore") : UiText.Get("Maximise");
        var growIcon = look.IsMaximized ? ChromeIcon.Restore : ChromeIcon.Maximize;
        items.Add(new(ChromeItemKind.Button, actions.ToggleMaximize, 0, grow, new(x + side, 0, side, side), true, false) { Icon = growIcon, HidesText = true });
        items.Add(new(ChromeItemKind.Button, actions.CloseWindow, 0, UiText.Get("Close"), new(x + side + side, 0, side, side), true, false) { Icon = ChromeIcon.Close, HidesText = true });
    }

    /// <summary>Handles a press on the empty part of the bar at the top: the first moves the window, and the second makes it as big as the screen or puts it back.</summary>
    /// <param name="window">The window.</param>
    /// <param name="presses">How many presses have come close together, this one included.</param>
    internal static void PressTitle(IPlatformWindow window, int presses)
    {
        if (presses == DoublePress)
        {
            window.SetMaximized(!window.IsMaximized);
            return;
        }

        window.BeginMove();
    }

    /// <summary>Finds the edges of the window a point is on, where a press sizes the window.</summary>
    /// <param name="look">The sizes, and whether the window needs its own frame.</param>
    /// <param name="size">The window size in device pixels.</param>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    /// <returns>The edges, or none when the system sizes the window, the window fills the screen or the point is inside.</returns>
    internal static WindowEdges EdgesAt(ChromeLook look, SurfaceSize size, int x, int y)
    {
        if (!look.OwnsFrame || look.IsMaximized)
        {
            return WindowEdges.None;
        }

        var edge = look.Metrics.Gap + (look.Metrics.Gap >> 1);
        var edges = WindowEdges.None;
        if (y < edge)
        {
            edges |= WindowEdges.Top;
        }
        else if (y >= size.Height - edge)
        {
            edges |= WindowEdges.Bottom;
        }

        if (x < edge)
        {
            edges |= WindowEdges.Left;
        }
        else if (x >= size.Width - edge)
        {
            edges |= WindowEdges.Right;
        }

        return edges;
    }
}
