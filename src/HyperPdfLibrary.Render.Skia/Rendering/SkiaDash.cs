// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Builds the Skia dash effect of a stroke's dash array.</summary>
internal static class SkiaDash
{
    /// <summary>The smallest scale used when dividing by a matrix scale.</summary>
    internal const float MinScale = 1e-6F;

    /// <summary>The factor that makes an odd-length dash array even by repeating it.</summary>
    private const int PairLength = 2;

    /// <summary>
    /// The length, in device pixels, given to a zero-length dash. ISO 32000-2 8.4.3.6 says a zero-length dash paints a dot
    /// with round or square caps and nothing with butt caps. Skia draws a cap only on a dash of some length, and a tenth
    /// of a pixel gives the full dot (a hundredth drew visibly smaller dots in a render comparison) while a butt-capped
    /// dash of that length stays under 10% coverage.
    /// </summary>
    private const float DotLength = 0.1F;

    /// <summary>
    /// The shortest dash cycle, in device pixels, that is dashed. A pattern repeating faster than a sixteenth of a pixel
    /// reads as an even tone, so it is drawn solid instead of building an enormous number of segments.
    /// </summary>
    private const float MinDashCycle = 0.0625F;

    /// <summary>
    /// Creates a dash effect (ISO 32000-2 8.4.3.6). A zero-length dash becomes a tenth of a device pixel so round or
    /// square caps still draw a dot, negative lengths (invalid) count as zero, and an array that is all zero or whose cycle
    /// is finer than a sixteenth of a pixel is drawn solid.
    /// </summary>
    /// <param name="lengths">The dash lengths; empty for a solid line.</param>
    /// <param name="phase">The phase.</param>
    /// <param name="scale">The device units one unit of the path's space spans.</param>
    /// <returns>The effect, or null for a solid line.</returns>
    internal static SKPathEffect? Create(ReadOnlyMemory<float> lengths, float phase, float scale)
    {
        if (lengths.IsEmpty)
        {
            return null;
        }

        var source = lengths.Span;
        var cycle = 0F;
        foreach (var length in source)
        {
            if (!float.IsFinite(length))
            {
                return null;
            }

            cycle += Math.Max(0, length);
        }

        scale = Math.Max(scale, MinScale);
        if (cycle <= 0 || cycle * scale < MinDashCycle)
        {
            return null;
        }

        var dot = DotLength / scale;

        var count = source.Length % PairLength == 0 ? source.Length : source.Length * PairLength;
        var array = new float[count];
        for (var i = 0; i < count; i++)
        {
            var length = source[i % source.Length];
            array[i] = length <= dot ? dot : length;
        }

        return SKPathEffect.CreateDash(array, phase);
    }
}
