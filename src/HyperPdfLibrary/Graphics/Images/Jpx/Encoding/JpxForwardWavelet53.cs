// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The forward 5/3 reversible wavelet transform (ISO 15444-1 F.4) by lifting, on integers. Each level lifts the columns
/// of whole rows, gathers the low-pass rows above the high-pass ones, then lifts each row and splits it the same way, so
/// the sub-bands land where the decoder expects them: LL top left, HL top right, LH bottom left, HH bottom right.
/// </summary>
internal static class JpxForwardWavelet53
{
    /// <summary>The distance between samples of the same kind in an interleaved line.</summary>
    private const int Pair = 2;

    /// <summary>The shift that divides the low-pass update by four (equation F-6).</summary>
    private const int QuarterShift = 2;

    /// <summary>Transforms a tile-component in place.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="buffer">The samples, row by row at the component's width, replaced by the sub-bands.</param>
    /// <param name="scratch">Working space of the same size.</param>
    internal static void Forward(JpxTile tile, in JpxTileComponent component, Span<int> buffer, Span<int> scratch)
    {
        var stride = component.Area.Width;
        for (var resolution = component.Levels; resolution >= 1; resolution--)
        {
            var level = JpxWaveletLevel.At(tile, component, resolution);
            if (level.Width == 0 || level.Height == 0)
            {
                continue;
            }

            LiftColumns(buffer, stride, level);
            SplitRows(buffer, stride, scratch, level);
            SplitColumns(scratch, buffer, stride, level);
        }
    }

    /// <summary>Lifts the columns of a level: every row at once, the high-pass rows first and then the low-pass rows.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="stride">The samples between rows.</param>
    /// <param name="level">The level.</param>
    private static void LiftColumns(Span<int> data, int stride, in JpxWaveletLevel level)
    {
        var width = level.Width;
        var height = level.Height;
        if (height == 1)
        {
            DoubleIfHigh(data[..width], level.RowParity);
            return;
        }

        for (var row = 1 - level.RowParity; row < height; row += Pair)
        {
            var up = JpxWaveletLevel.Mirror(row - 1, height);
            var down = JpxWaveletLevel.Mirror(row + 1, height);
            JpxLiftRows.SubtractHalfSum(data.Slice(row * stride, width), data.Slice(up * stride, width), data.Slice(down * stride, width));
        }

        for (var row = level.RowParity; row < height; row += Pair)
        {
            var up = JpxWaveletLevel.Mirror(row - 1, height);
            var down = JpxWaveletLevel.Mirror(row + 1, height);
            JpxLiftRows.AddQuarterSum(data.Slice(row * stride, width), data.Slice(up * stride, width), data.Slice(down * stride, width));
        }
    }

    /// <summary>Copies the rows of a level to the scratch space, the low-pass rows first.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="stride">The samples between rows of <paramref name="data"/>.</param>
    /// <param name="scratch">Receives the rows, packed at the level's width.</param>
    /// <param name="level">The level.</param>
    private static void SplitRows(ReadOnlySpan<int> data, int stride, Span<int> scratch, in JpxWaveletLevel level)
    {
        var width = level.Width;
        var low = 0;
        var high = level.LowRows;
        for (var row = 0; row < level.Height; row++)
        {
            var isLow = ((row + level.RowParity) & 1) == 0;
            var target = isLow ? low : high;
            data.Slice(row * stride, width).CopyTo(scratch.Slice(target * width, width));
            if (isLow)
            {
                low++;
            }
            else
            {
                high++;
            }
        }
    }

    /// <summary>Lifts each row of the scratch space, then writes it back with the low-pass samples first.</summary>
    /// <param name="scratch">The rows, packed at the level's width.</param>
    /// <param name="data">Receives the rows at <paramref name="stride"/>.</param>
    /// <param name="stride">The samples between rows of <paramref name="data"/>.</param>
    /// <param name="level">The level.</param>
    private static void SplitColumns(Span<int> scratch, Span<int> data, int stride, in JpxWaveletLevel level)
    {
        var width = level.Width;
        for (var row = 0; row < level.Height; row++)
        {
            var line = scratch.Slice(row * width, width);
            LiftRow(line, level.ColumnParity);
            var target = data.Slice(row * stride, width);
            var low = 0;
            var high = level.LowColumns;
            for (var column = 0; column < width; column++)
            {
                if (((column + level.ColumnParity) & 1) == 0)
                {
                    target[low] = line[column];
                    low++;
                }
                else
                {
                    target[high] = line[column];
                    high++;
                }
            }
        }
    }

    /// <summary>Lifts one line: the high-pass step on the odd coordinates, then the low-pass step on the even ones.</summary>
    /// <param name="line">The samples, changed in place.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate.</param>
    private static void LiftRow(Span<int> line, int parity)
    {
        var count = line.Length;
        if (count == 1)
        {
            DoubleIfHigh(line, parity);
            return;
        }

        for (var i = 1 - parity; i < count; i += Pair)
        {
            line[i] -= (line[JpxWaveletLevel.Mirror(i - 1, count)] + line[JpxWaveletLevel.Mirror(i + 1, count)]) >> 1;
        }

        for (var i = parity; i < count; i += Pair)
        {
            line[i] += (line[JpxWaveletLevel.Mirror(i - 1, count)] + line[JpxWaveletLevel.Mirror(i + 1, count)] + Pair) >> QuarterShift;
        }
    }

    /// <summary>Doubles a lone sample on an odd coordinate, which is high-pass (equation F-7 inverted).</summary>
    /// <param name="samples">The samples of a line of one sample.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate.</param>
    private static void DoubleIfHigh(Span<int> samples, int parity)
    {
        if (parity != 1)
        {
            return;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= Pair;
        }
    }
}
