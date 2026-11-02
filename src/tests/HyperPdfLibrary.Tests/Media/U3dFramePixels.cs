// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>Reads pixels from the frames the drawing tests make.</summary>
internal static class U3dFramePixels
{
    /// <summary>Opaque red, packed as red, green, blue, alpha.</summary>
    internal const uint Red = 0xFF0000FF;

    /// <summary>Opaque blue, packed as red, green, blue, alpha.</summary>
    internal const uint Blue = 0x0000FFFF;

    /// <summary>Opaque green, packed as red, green, blue, alpha.</summary>
    internal const uint Green = 0x00FF00FF;

    /// <summary>Opaque white, packed as red, green, blue, alpha.</summary>
    internal const uint White = 0xFFFFFFFF;

    /// <summary>The bits to move a packed pixel's red to the bottom.</summary>
    internal const int RedShift = 24;

    /// <summary>The bits to move a packed pixel's green to the bottom.</summary>
    internal const int GreenShift = 16;

    /// <summary>The bits to move a packed pixel's blue to the bottom.</summary>
    internal const int BlueShift = 8;

    /// <summary>The mask of one channel.</summary>
    internal const uint ChannelMask = 0xFF;

    /// <summary>The bytes of a pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The offset of the second channel in a pixel.</summary>
    private const int SecondChannel = 1;

    /// <summary>The offset of the third channel in a pixel.</summary>
    private const int ThirdChannel = 2;

    /// <summary>The offset of the fourth channel in a pixel.</summary>
    private const int FourthChannel = 3;

    /// <summary>Reads a pixel as red, green, blue and alpha packed into one number.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The pixel.</returns>
    internal static uint Pixel(U3dFrame frame, int x, int y)
    {
        var at = ((y * frame.Width) + x) * PixelBytes;
        return ((uint)frame.Rgba[at] << RedShift) | ((uint)frame.Rgba[at + SecondChannel] << GreenShift) | ((uint)frame.Rgba[at + ThirdChannel] << BlueShift) | frame.Rgba[at + FourthChannel];
    }

    /// <summary>Counts the pixels of one colour.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="color">The packed colour.</param>
    /// <returns>The number of pixels.</returns>
    internal static int Count(U3dFrame frame, uint color)
    {
        var count = 0;
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                count += Pixel(frame, x, y) == color ? 1 : 0;
            }
        }

        return count;
    }
}
