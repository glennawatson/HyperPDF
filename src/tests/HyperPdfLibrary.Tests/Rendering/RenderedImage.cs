// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A rendered tile: BGRA premultiplied pixels with accessors for assertions.</summary>
[DebuggerDisplay("RenderedImage: {Width}x{Height}")]
internal sealed class RenderedImage
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    internal const int BytesPerPixel = 4;

    /// <summary>The offset of the green byte in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>Initializes a new instance of the <see cref="RenderedImage"/> class.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal RenderedImage(byte[] pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the pixels, tightly packed.</summary>
    internal byte[] Pixels { get; }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Determines whether a pixel is near a colour.</summary>
    /// <param name="x">The pixel's x.</param>
    /// <param name="y">The pixel's y.</param>
    /// <param name="expected">The expected colour.</param>
    /// <param name="tolerance">The largest difference allowed in any channel.</param>
    /// <returns><see langword="true"/> when every channel is within the tolerance.</returns>
    internal bool IsNear(int x, int y, Rgb expected, int tolerance)
    {
        var offset = ((y * Width) + x) * BytesPerPixel;
        return Math.Abs(Pixels[offset + RedOffset] - expected.Red) <= tolerance
            && Math.Abs(Pixels[offset + GreenOffset] - expected.Green) <= tolerance
            && Math.Abs(Pixels[offset] - expected.Blue) <= tolerance;
    }

    /// <summary>Gets a pixel as text, for assertion messages.</summary>
    /// <param name="x">The pixel's x.</param>
    /// <param name="y">The pixel's y.</param>
    /// <returns>The channels as red, green, blue and alpha.</returns>
    internal string Describe(int x, int y)
    {
        var offset = ((y * Width) + x) * BytesPerPixel;
        return $"({x},{y}) = R{Pixels[offset + RedOffset]} G{Pixels[offset + GreenOffset]} B{Pixels[offset]} A{Pixels[offset + RedOffset + 1]}";
    }
}
