// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

/// <summary>
/// The forward 9/7 irreversible wavelet transform (ISO 15444-1 F.4.8) by lifting, on floats: four lifting steps and the
/// scaling by K and 1/K. The sub-bands are laid out as for the 5/3 transform. Each step multiplies before it adds, so no
/// fused multiply-add changes the result between processors.
/// </summary>
internal static class JpxForwardWavelet97
{
    /// <summary>The distance between samples of the same kind in an interleaved line.</summary>
    private const int Pair = 2;

    /// <summary>The first lifting coefficient, on the high-pass samples (equation F-9).</summary>
    private const float Alpha = -1.586134342F;

    /// <summary>The second lifting coefficient, on the low-pass samples.</summary>
    private const float Beta = -0.052980118F;

    /// <summary>The third lifting coefficient, on the high-pass samples.</summary>
    private const float Gamma = 0.882911075F;

    /// <summary>The fourth lifting coefficient, on the low-pass samples.</summary>
    private const float Delta = 0.443506852F;

    /// <summary>The scale K: high-pass samples are multiplied by it (equation F-13).</summary>
    private const float HighScale = 1.230174105F;

    /// <summary>The scale 1/K: low-pass samples are multiplied by it (equation F-14).</summary>
    private const float LowScale = 1F / HighScale;

    /// <summary>Transforms a tile-component in place.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="buffer">The samples, row by row at the component's width, replaced by the sub-bands.</param>
    /// <param name="scratch">Working space of the same size.</param>
    internal static void Forward(JpxTile tile, in JpxTileComponent component, Span<float> buffer, Span<float> scratch)
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

    /// <summary>Lifts the columns of a level: each lifting step over whole rows, then the scaling.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="stride">The samples between rows.</param>
    /// <param name="level">The level.</param>
    private static void LiftColumns(Span<float> data, int stride, in JpxWaveletLevel level)
    {
        var width = level.Width;
        var height = level.Height;
        if (height == 1)
        {
            DoubleIfHigh(data[..width], level.RowParity);
            return;
        }

        LiftRows(data, stride, level, 1 - level.RowParity, Alpha);
        LiftRows(data, stride, level, level.RowParity, Beta);
        LiftRows(data, stride, level, 1 - level.RowParity, Gamma);
        LiftRows(data, stride, level, level.RowParity, Delta);
        for (var row = 0; row < height; row++)
        {
            var isLow = ((row + level.RowParity) & 1) == 0;
            JpxLiftRows.Scale(data.Slice(row * stride, width), isLow ? LowScale : HighScale);
        }
    }

    /// <summary>Applies one lifting step to every second row.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="stride">The samples between rows.</param>
    /// <param name="level">The level.</param>
    /// <param name="first">The first row to change.</param>
    /// <param name="factor">The lifting coefficient.</param>
    private static void LiftRows(Span<float> data, int stride, in JpxWaveletLevel level, int first, float factor)
    {
        for (var row = first; row < level.Height; row += Pair)
        {
            var up = JpxWaveletLevel.Mirror(row - 1, level.Height);
            var down = JpxWaveletLevel.Mirror(row + 1, level.Height);
            JpxLiftRows.AddScaledSum(data.Slice(row * stride, level.Width), data.Slice(up * stride, level.Width), data.Slice(down * stride, level.Width), factor);
        }
    }

    /// <summary>Copies the rows of a level to the scratch space, the low-pass rows first.</summary>
    /// <param name="data">The samples.</param>
    /// <param name="stride">The samples between rows of <paramref name="data"/>.</param>
    /// <param name="scratch">Receives the rows, packed at the level's width.</param>
    /// <param name="level">The level.</param>
    private static void SplitRows(ReadOnlySpan<float> data, int stride, Span<float> scratch, in JpxWaveletLevel level)
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
    private static void SplitColumns(Span<float> scratch, Span<float> data, int stride, in JpxWaveletLevel level)
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

    /// <summary>Lifts one line.</summary>
    /// <param name="line">The samples, changed in place.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate.</param>
    private static void LiftRow(Span<float> line, int parity)
    {
        if (line.Length == 1)
        {
            DoubleIfHigh(line, parity);
            return;
        }

        LiftSamples(line, 1 - parity, Alpha);
        LiftSamples(line, parity, Beta);
        LiftSamples(line, 1 - parity, Gamma);
        LiftSamples(line, parity, Delta);
        for (var i = 0; i < line.Length; i++)
        {
            line[i] *= ((i + parity) & 1) == 0 ? LowScale : HighScale;
        }
    }

    /// <summary>Applies one lifting step to every second sample of a line.</summary>
    /// <param name="line">The samples, changed in place.</param>
    /// <param name="first">The first sample to change.</param>
    /// <param name="factor">The lifting coefficient.</param>
    private static void LiftSamples(Span<float> line, int first, float factor)
    {
        var count = line.Length;
        for (var i = first; i < count; i += Pair)
        {
            line[i] += (line[JpxWaveletLevel.Mirror(i - 1, count)] + line[JpxWaveletLevel.Mirror(i + 1, count)]) * factor;
        }
    }

    /// <summary>Doubles a lone sample on an odd coordinate, which is high-pass.</summary>
    /// <param name="samples">The samples of a line of one sample.</param>
    /// <param name="parity">1 when the line starts on an odd coordinate.</param>
    private static void DoubleIfHigh(Span<float> samples, int parity)
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
