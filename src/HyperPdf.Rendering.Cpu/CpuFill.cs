// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Cpu;

/// <summary>Fills BGRA pixels with one colour.</summary>
public static class CpuFill
{
    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of the green byte in a pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red byte in a pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha byte in a pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The largest channel value.</summary>
    private const int ChannelMax = 255;

    /// <summary>Half a channel step, added before a divide to round it.</summary>
    private const int HalfChannel = 127;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The bit shift of the red channel in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>Fills a span with one colour, stored premultiplied.</summary>
    /// <param name="span">The pixels to fill.</param>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    public static void Fill(Span<byte> span, uint argb)
    {
        var alpha = (byte)(argb >> AlphaShift);
        var b = Premultiply((byte)argb, alpha);
        var g = Premultiply((byte)(argb >> GreenShift), alpha);
        var r = Premultiply((byte)(argb >> RedShift), alpha);
        for (var i = 0; i + BytesPerPixel <= span.Length; i += BytesPerPixel)
        {
            span[i] = b;
            span[i + GreenOffset] = g;
            span[i + RedOffset] = r;
            span[i + AlphaOffset] = alpha;
        }
    }

    /// <summary>Multiplies a colour channel by alpha.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="alpha">The alpha.</param>
    /// <returns>The premultiplied channel.</returns>
    private static byte Premultiply(byte channel, byte alpha) => (byte)(((channel * alpha) + HalfChannel) / ChannelMax);
}
