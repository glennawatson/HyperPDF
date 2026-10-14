// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>Converts PDF samples (0 is black) to JBIG2 pixels (1 is black).</summary>
internal static class Jbig2Ink
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The value of a byte with every bit set.</summary>
    private const byte AllBits = 0xFF;

    /// <summary>Inverts rows into a buffer and clears the padding bits at the end of each row.</summary>
    /// <param name="samples">The packed sample rows.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="ink">Receives the rows.</param>
    internal static void FromSamples(ReadOnlySpan<byte> samples, int width, int height, ref PooledBuffer ink)
    {
        var length = Jbig2Bits.Stride(width) * height;
        FromSamples(samples, width, height, ink.GetSpan(length)[..length]);
        ink.Advance(length);
    }

    /// <summary>Inverts rows into a span and clears the padding bits at the end of each row.</summary>
    /// <param name="samples">The packed sample rows.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="target">Receives the rows; exactly the length of the rows.</param>
    internal static void FromSamples(ReadOnlySpan<byte> samples, int width, int height, Span<byte> target)
    {
        var stride = Jbig2Bits.Stride(width);
        var length = stride * height;
        for (var i = 0; i < length; i++)
        {
            target[i] = (byte)~samples[i];
        }

        var padding = (stride * ByteBits) - width;
        if (padding == 0)
        {
            return;
        }

        var mask = (byte)(AllBits << padding);
        for (var y = 0; y < height; y++)
        {
            target[(y * stride) + stride - 1] &= mask;
        }
    }

    /// <summary>Counts the black pixels.</summary>
    /// <param name="ink">The packed rows with zero padding.</param>
    /// <returns>The count.</returns>
    internal static long CountBlack(ReadOnlySpan<byte> ink)
    {
        long count = 0;
        foreach (var value in ink)
        {
            count += BitOperations.PopCount(value);
        }

        return count;
    }
}
