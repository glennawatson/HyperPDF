// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>Makes the picture drawn in place of an image that cannot be read: a pale box with a darker frame and a cross.</summary>
internal static class ImagePlaceholder
{
    /// <summary>The side of the placeholder in pixels; it is scaled to the image's place.</summary>
    private const int Side = 32;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the green sample in a pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red sample in a pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha sample in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The grey of the box.</summary>
    private const byte Fill = 0xE8;

    /// <summary>The grey of the frame and the cross.</summary>
    private const byte Line = 0x90;

    /// <summary>The width of the frame in pixels.</summary>
    private const int Frame = 2;

    /// <summary>How far a pixel may be from a diagonal and still be part of the cross.</summary>
    private const int CrossReach = 1;

    /// <summary>Makes the placeholder.</summary>
    /// <returns>An opaque premultiplied BGRA image.</returns>
    internal static PdfImageData Create()
    {
        var pixels = new byte[Side * Side * BgraBytes];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var onLine = x < Frame || y < Frame || x >= Side - Frame || y >= Side - Frame || IsOnCross(x, y);
                var grey = onLine ? Line : Fill;
                var offset = ((y * Side) + x) * BgraBytes;
                pixels[offset] = grey;
                pixels[offset + GreenOffset] = grey;
                pixels[offset + RedOffset] = grey;
                pixels[offset + AlphaOffset] = byte.MaxValue;
            }
        }

        return new(Side, Side, pixels, false, true, PdfImageCodec.None);
    }

    /// <summary>Determines whether a pixel lies on one of the two diagonals.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when the pixel is part of the cross.</returns>
    private static bool IsOnCross(int x, int y) => Math.Abs(x - y) <= CrossReach || Math.Abs(x + y - (Side - 1)) <= CrossReach;
}
