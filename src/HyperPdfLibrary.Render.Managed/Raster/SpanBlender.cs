// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Blends a run of colour onto a line of pixels through a line of coverage. Pixels are premultiplied, so source over destination is
/// the source plus the destination scaled by what the source leaves uncovered.
/// </summary>
internal static class SpanBlender
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The shift that moves alpha to the lowest byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The mask of the blue and red bytes of a pixel, which are scaled together.</summary>
    private const uint EvenBytes = 0x00FF00FF;

    /// <summary>The rounding term for both bytes of a pair.</summary>
    private const uint RoundPair = 0x00800080;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Blends one colour onto a line.</summary>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="coverage">The coverage of each pixel of the run.</param>
    /// <param name="color">The premultiplied colour.</param>
    internal static void Solid(Span<uint> pixels, ReadOnlySpan<byte> coverage, uint color)
    {
        var opaque = color >> AlphaShift == Full;
        var run = 0;
        for (var at = 0; at < coverage.Length; at += run)
        {
            var value = coverage[at];
            var rest = coverage[at..];
            run = rest.IndexOfAnyExcept(value);
            run = run < 0 ? rest.Length : run;
            if (value == Full && opaque)
            {
                // A covered run of an opaque colour replaces the pixels, which the runtime fills many at a time.
                pixels.Slice(at, run).Fill(color);
            }
            else if (value != 0)
            {
                Over(pixels.Slice(at, run), Scale(color, value));
            }
        }
    }

    /// <summary>Blends a line of colours onto a line.</summary>
    /// <param name="pixels">The pixels of the run.</param>
    /// <param name="coverage">The coverage of each pixel of the run.</param>
    /// <param name="source">The premultiplied colour of each pixel of the run.</param>
    internal static void Shaded(Span<uint> pixels, ReadOnlySpan<byte> coverage, ReadOnlySpan<uint> source)
    {
        for (var i = 0; i < coverage.Length; i++)
        {
            var value = coverage[i];
            if (value == 0)
            {
                continue;
            }

            var color = value == Full ? source[i] : Scale(source[i], value);
            pixels[i] = color >> AlphaShift == Full ? color : color + Scale(pixels[i], Full - (color >> AlphaShift));
        }
    }

    /// <summary>Multiplies every byte of a pixel by a fraction.</summary>
    /// <param name="pixel">The pixel.</param>
    /// <param name="fraction">The fraction, where 255 is one.</param>
    /// <returns>The scaled pixel, rounded to nearest.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Scale(uint pixel, uint fraction)
    {
        // Two bytes are scaled in one multiply, with the exact division by 255 done as two shifts.
        var even = ((pixel & EvenBytes) * fraction) + RoundPair;
        even = ((even + ((even >> ByteBits) & EvenBytes)) >> ByteBits) & EvenBytes;
        var odd = (((pixel >> ByteBits) & EvenBytes) * fraction) + RoundPair;
        odd = ((odd + ((odd >> ByteBits) & EvenBytes)) >> ByteBits) & EvenBytes;
        return even | (odd << ByteBits);
    }

    /// <summary>Multiplies two lines of coverage, as a clip does to a shape.</summary>
    /// <param name="coverage">The shape's coverage, which receives the product.</param>
    /// <param name="mask">The clip's coverage.</param>
    internal static void Multiply(Span<byte> coverage, ReadOnlySpan<byte> mask)
    {
        for (var i = 0; i < coverage.Length; i++)
        {
            var product = (coverage[i] * (uint)mask[i]) + (RoundPair & Full);
            coverage[i] = (byte)((product + (product >> ByteBits)) >> ByteBits);
        }
    }

    /// <summary>Puts one premultiplied colour over every pixel of a run.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="color">The colour, already scaled by the coverage.</param>
    private static void Over(Span<uint> pixels, uint color)
    {
        var keep = Full - (color >> AlphaShift);
        foreach (ref var pixel in pixels)
        {
            pixel = color + Scale(pixel, keep);
        }
    }
}
