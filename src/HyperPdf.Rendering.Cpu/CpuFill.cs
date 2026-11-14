// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Fills BGRA pixels with one colour.</summary>
public static class CpuFill
{
    /// <summary>The largest channel value.</summary>
    private const uint ChannelMax = 255;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>Fills a span with one colour, stored premultiplied. The colour replaces what was there.</summary>
    /// <param name="span">The pixels to fill.</param>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fill(Span<byte> span, uint argb) => MemoryMarshal.Cast<byte, uint>(span).Fill(PremultipliedColor.FromArgb(argb));

    /// <summary>Draws one colour over a span: an opaque colour replaces the pixels and any other colour is blended over them.</summary>
    /// <param name="span">The pixels to draw over.</param>
    /// <param name="argb">The colour as 0xAARRGGBB.</param>
    public static void Over(Span<byte> span, uint argb)
    {
        // A premultiplied 0xAARRGGBB colour read as a little endian integer has the byte order of a BGRA pixel.
        var pixel = PremultipliedColor.FromArgb(argb);
        if (argb >> AlphaShift == ChannelMax)
        {
            MemoryMarshal.Cast<byte, uint>(span).Fill(pixel);
            return;
        }

        CpuBlend.BlendColor(span, pixel);
    }
}
