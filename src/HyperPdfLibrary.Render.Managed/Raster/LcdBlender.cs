// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Blends text whose coverage was measured three times across each pixel, once for each colour stripe of a screen's pixel. Each
/// colour of a pixel is blended with the coverage of its own stripe, which triples the detail across a line of text. The coverage
/// is spread over five stripes first, so no stripe is lit far more than its neighbours and edges show no colour fringes.
/// </summary>
internal static class LcdBlender
{
    /// <summary>The stripes in a pixel.</summary>
    internal const int Stripes = 3;

    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The shift that moves alpha to the lowest byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The shift that moves red to the lowest byte of a pixel. Red is the left stripe of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift that moves green to the lowest byte of a pixel.</summary>
    private const int GreenShift = 8;

    /// <summary>The weight of the stripe a colour belongs to.</summary>
    private const uint CentreWeight = 3;

    /// <summary>The weight of the stripes beside it.</summary>
    private const uint NearWeight = 2;

    /// <summary>The sum of the five weights: 1, 2, 3, 2, 1.</summary>
    private const uint WeightSum = 9;

    /// <summary>Half the sum of the weights, which rounds the division to nearest.</summary>
    private const uint WeightRound = 4;

    /// <summary>The rounding term of a product of two bytes.</summary>
    private const uint Round = 128;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Blends one colour onto a line through the coverage of each stripe.</summary>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="stripes">The coverage of each stripe, three for each pixel of the run.</param>
    /// <param name="mask">The clip's coverage of each pixel of the run, or empty when the clip is a rectangle.</param>
    /// <param name="color">The premultiplied colour.</param>
    /// <param name="table">The table that reshapes the coverage, as darker text does, or <see langword="null"/>.</param>
    internal static void Blend(Span<uint> pixels, ReadOnlySpan<byte> stripes, ReadOnlySpan<byte> mask, uint color, byte[]? table)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            var first = i * Stripes;
            var red = Spread(stripes, first);
            var green = Spread(stripes, first + 1);
            var blue = Spread(stripes, first + 1 + 1);
            if ((red | green | blue) == 0)
            {
                continue;
            }

            if (table is not null)
            {
                red = table[red];
                green = table[green];
                blue = table[blue];
            }

            if (!mask.IsEmpty)
            {
                red = Multiply(red, mask[i]);
                green = Multiply(green, mask[i]);
                blue = Multiply(blue, mask[i]);
            }

            pixels[i] = Over(pixels[i], color, red, green, blue);
        }
    }

    /// <summary>Puts a colour over a pixel, each colour through its own coverage.</summary>
    /// <param name="pixel">The pixel under the text.</param>
    /// <param name="color">The premultiplied colour of the text.</param>
    /// <param name="red">The coverage of the red stripe.</param>
    /// <param name="green">The coverage of the green stripe.</param>
    /// <param name="blue">The coverage of the blue stripe.</param>
    /// <returns>The pixel with the text on it.</returns>
    private static uint Over(uint pixel, uint color, uint red, uint green, uint blue)
    {
        var alpha = color >> AlphaShift;
        var outRed = Channel(pixel >> RedShift, color >> RedShift, alpha, red);
        var outGreen = Channel(pixel >> GreenShift, color >> GreenShift, alpha, green);
        var outBlue = Channel(pixel, color, alpha, blue);

        // The pixel's own alpha follows the middle of the three coverages, and never falls under one of its colours.
        var outAlpha = Channel(pixel >> AlphaShift, alpha, alpha, (red + green + blue + 1) / Stripes);
        outAlpha = Math.Max(outAlpha, Math.Max(outRed, Math.Max(outGreen, outBlue)));
        return outBlue | (outGreen << GreenShift) | (outRed << RedShift) | (outAlpha << AlphaShift);
    }

    /// <summary>Blends one colour of a pixel.</summary>
    /// <param name="under">The colour under the text, in the lowest byte.</param>
    /// <param name="over">The premultiplied colour of the text, in the lowest byte.</param>
    /// <param name="alpha">The alpha of the text.</param>
    /// <param name="coverage">The coverage of the colour's stripe.</param>
    /// <returns>The blended colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Channel(uint under, uint over, uint alpha, uint coverage) =>
        Multiply(over & Full, coverage) + Multiply(under & Full, Full - Multiply(alpha, coverage));

    /// <summary>Spreads the coverage around one stripe over five stripes, weighted 1, 2, 3, 2, 1.</summary>
    /// <param name="stripes">The coverage of each stripe.</param>
    /// <param name="at">The stripe.</param>
    /// <returns>The spread coverage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Spread(ReadOnlySpan<byte> stripes, int at)
    {
        var sum = (At(stripes, at) * CentreWeight) + ((At(stripes, at - 1) + At(stripes, at + 1)) * NearWeight) + At(stripes, at - 1 - 1) + At(stripes, at + 1 + 1);
        return (sum + WeightRound) / WeightSum;
    }

    /// <summary>Reads the coverage of a stripe. A stripe outside the line is uncovered.</summary>
    /// <param name="stripes">The coverage of each stripe.</param>
    /// <param name="at">The stripe.</param>
    /// <returns>The coverage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint At(ReadOnlySpan<byte> stripes, int at) => (uint)at < (uint)stripes.Length ? stripes[at] : 0U;

    /// <summary>Multiplies two bytes that each stand for a fraction.</summary>
    /// <param name="a">The first byte.</param>
    /// <param name="b">The second byte.</param>
    /// <returns>The product, rounded to nearest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Multiply(uint a, uint b)
    {
        var product = (a * b) + Round;
        return (product + (product >> ByteBits)) >> ByteBits;
    }
}
