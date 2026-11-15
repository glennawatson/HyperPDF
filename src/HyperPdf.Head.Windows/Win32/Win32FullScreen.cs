// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Makes a window fill the screen it is on with nothing round it, and puts it back as it was.</summary>
internal static unsafe class Win32FullScreen
{
    /// <summary>The index of the window style in the window's long values.</summary>
    private const int StyleIndex = -16;

    /// <summary>The WS_OVERLAPPEDWINDOW style: the border, the title bar and the buttons.</summary>
    private const nint Frame = 0x00CF0000;

    /// <summary>The flag that picks the screen the window is mostly on.</summary>
    private const uint NearestMonitor = 2;

    /// <summary>Keeps the stacking order, and makes the window draw its frame again.</summary>
    private const uint FrameChanged = 0x0200 | 0x0020;

    /// <summary>The size of the monitor information in bytes.</summary>
    private const int MonitorInfoSize = 40;

    /// <summary>The offset of the screen's rectangle in the monitor information.</summary>
    private const int MonitorRectOffset = 4;

    /// <summary>The ints in a rectangle.</summary>
    private const int RectInts = 4;

    /// <summary>The index of the right edge in a rectangle.</summary>
    private const int RightIndex = 2;

    /// <summary>The index of the bottom edge in a rectangle.</summary>
    private const int BottomIndex = 3;

    /// <summary>Makes the window fill its screen.</summary>
    /// <param name="window">The window.</param>
    /// <returns>Where the window was and how it looked, to put it back with.</returns>
    internal static Win32Placement Enter(nint window)
    {
        var style = NativeMethods.GetWindowLongPtr(window, StyleIndex);
        int* rect = stackalloc int[RectInts];
        _ = NativeMethods.GetWindowRect(window, rect);
        var before = new Win32Placement((long)style, rect[0], rect[1], rect[RightIndex] - rect[0], rect[BottomIndex] - rect[1]);
        byte* info = stackalloc byte[MonitorInfoSize];
        new Span<byte>(info, MonitorInfoSize).Clear();
        *(int*)info = MonitorInfoSize;
        var monitor = NativeMethods.MonitorFromWindow(window, NearestMonitor);
        if (NativeMethods.GetMonitorInfo(monitor, info) == 0)
        {
            return before;
        }

        var screen = (int*)(info + MonitorRectOffset);
        _ = NativeMethods.SetWindowLongPtr(window, StyleIndex, style & ~Frame);
        _ = NativeMethods.SetWindowPos(window, 0, screen[0], screen[1], screen[RightIndex] - screen[0], screen[BottomIndex] - screen[1], FrameChanged);
        return before;
    }

    /// <summary>Puts the window back as it was before it filled the screen.</summary>
    /// <param name="window">The window.</param>
    /// <param name="before">Where the window was and how it looked.</param>
    internal static void Leave(nint window, Win32Placement before)
    {
        _ = NativeMethods.SetWindowLongPtr(window, StyleIndex, (nint)before.Style);
        _ = NativeMethods.SetWindowPos(window, 0, before.X, before.Y, before.Width, before.Height, FrameChanged);
    }
}
