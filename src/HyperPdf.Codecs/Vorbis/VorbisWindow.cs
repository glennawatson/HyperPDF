// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Vorbis;

/// <summary>The window of a Vorbis block: each half rises (or falls) along a power sine curve, with flat parts and zeros when a long block meets a short one.</summary>
internal static class VorbisWindow
{
    /// <summary>A quarter turn, the angle the slope's inner sine runs over.</summary>
    private const double QuarterTurn = Math.PI / 2;

    /// <summary>Half, which puts a sample in the middle of its interval.</summary>
    private const double Half = 0.5;

    /// <summary>Makes the rising slope of a window half of a given length.</summary>
    /// <param name="length">The length of the half.</param>
    /// <returns>The slope.</returns>
    internal static float[] BuildSlope(int length)
    {
        var slope = new float[length];
        for (var index = 0; index < length; index++)
        {
            var inner = Math.Sin(QuarterTurn * (index + Half) / length);
            slope[index] = (float)Math.Sin(QuarterTurn * inner * inner);
        }

        return slope;
    }

    /// <summary>Multiplies half a block by its window: zero before the slope, the slope, then one.</summary>
    /// <param name="values">The half block.</param>
    /// <param name="slope">The rising slope.</param>
    /// <param name="start">Where the slope starts, measured from the outer edge of the half block.</param>
    /// <param name="falling">Whether the half is the right half, where the slope falls towards the outer edge.</param>
    internal static void Apply(Span<float> values, ReadOnlySpan<float> slope, int start, bool falling)
    {
        for (var index = 0; index < values.Length; index++)
        {
            var position = falling ? values.Length - 1 - index : index;
            values[index] *= Factor(position, start, slope);
        }
    }

    /// <summary>Gets the window value at a distance from the outer edge of a half.</summary>
    /// <param name="position">The distance from the outer edge.</param>
    /// <param name="start">Where the slope starts.</param>
    /// <param name="slope">The rising slope.</param>
    /// <returns>The window value.</returns>
    private static float Factor(int position, int start, ReadOnlySpan<float> slope)
    {
        if (position < start)
        {
            return 0F;
        }

        var inSlope = position - start;
        return inSlope < slope.Length ? slope[inSlope] : 1F;
    }
}
