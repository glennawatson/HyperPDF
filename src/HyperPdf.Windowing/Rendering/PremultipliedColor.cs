// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Windowing.Rendering;

/// <summary>Colour arithmetic every render backend shares, so a colour comes out the same on the CPU and on each GPU.</summary>
public static class PremultipliedColor
{
    /// <summary>The largest channel value.</summary>
    private const uint ChannelMax = 255;

    /// <summary>Half of 256, added before the divide by 255 so it rounds to nearest.</summary>
    private const int RoundingBias = 128;

    /// <summary>The shift that divides by 256.</summary>
    private const int ByteShift = 8;

    /// <summary>The bit shift of the alpha channel in 0xAARRGGBB.</summary>
    private const int AlphaShift = 24;

    /// <summary>The bit shift of the red channel in 0xAARRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit shift of the green channel in 0xAARRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The mask of the alpha and green channels, which keep their place when red and blue swap.</summary>
    private const uint AlphaGreenMask = 0xFF00FF00;

    /// <summary>Divides a product of two channel values by 255 and rounds to nearest, without a division.</summary>
    /// <param name="product">The product, at most 255 * 255.</param>
    /// <returns>The rounded quotient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DivideBy255(int product)
    {
        var biased = product + RoundingBias;
        return (biased + (biased >> ByteShift)) >> ByteShift;
    }

    /// <summary>Multiplies the colour channels of a 0xAARRGGBB colour by its alpha.</summary>
    /// <param name="argb">The colour.</param>
    /// <returns>The premultiplied colour, still as 0xAARRGGBB.</returns>
    public static uint FromArgb(uint argb)
    {
        var alpha = argb >> AlphaShift;
        var red = (uint)DivideBy255((int)(((argb >> RedShift) & ChannelMax) * alpha));
        var green = (uint)DivideBy255((int)(((argb >> GreenShift) & ChannelMax) * alpha));
        var blue = (uint)DivideBy255((int)((argb & ChannelMax) * alpha));
        return (alpha << AlphaShift) | (red << RedShift) | (green << GreenShift) | blue;
    }

    /// <summary>Converts a 0xAARRGGBB colour to the premultiplied vertex colour of the GPU backends, which has red in the low byte.</summary>
    /// <param name="argb">The colour.</param>
    /// <returns>The vertex colour.</returns>
    public static uint ToVertexColor(uint argb)
    {
        var premultiplied = FromArgb(argb);
        return (premultiplied & AlphaGreenMask) | ((premultiplied >> RedShift) & ChannelMax) | ((premultiplied & ChannelMax) << RedShift);
    }
}
