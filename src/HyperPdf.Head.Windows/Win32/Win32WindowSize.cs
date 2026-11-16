// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Gives a window's drawing area a size, which Windows asks for as the size of the whole window with its frame.</summary>
internal static unsafe class Win32WindowSize
{
    /// <summary>Keeps the place, the stacking order and the focus, so only the size changes: SWP_NOMOVE, SWP_NOZORDER and SWP_NOACTIVATE.</summary>
    private const uint SizeOnly = 0x0002 | 0x0004 | 0x0010;

    /// <summary>The ints in a rectangle.</summary>
    private const int RectInts = 4;

    /// <summary>The index of the right edge in a rectangle.</summary>
    private const int RightIndex = 2;

    /// <summary>The index of the bottom edge in a rectangle.</summary>
    private const int BottomIndex = 3;

    /// <summary>Resizes a window so its drawing area is a size.</summary>
    /// <param name="window">The window.</param>
    /// <param name="width">The width of the drawing area in pixels.</param>
    /// <param name="height">The height of the drawing area in pixels.</param>
    internal static void SetClient(nint window, int width, int height)
    {
        int* outer = stackalloc int[RectInts];
        int* client = stackalloc int[RectInts];
        _ = NativeMethods.GetWindowRect(window, outer);
        _ = NativeMethods.GetClientRect(window, client);

        // The frame is what the whole window has more than its drawing area, and a client rectangle starts at zero.
        var frameWidth = (outer[RightIndex] - outer[0]) - client[RightIndex];
        var frameHeight = (outer[BottomIndex] - outer[1]) - client[BottomIndex];
        _ = NativeMethods.SetWindowPos(window, 0, 0, 0, width + frameWidth, height + frameHeight, SizeOnly);
    }
}
