// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.Codecs.Mp3;

/// <summary>
/// The hybrid filterbank of layer 3: the coefficients of a granule are put back in time order if they are short windows, the aliasing the
/// encoder added between neighbouring subbands is undone, and each subband's 18 coefficients become 18 samples by an inverse MDCT that overlaps
/// the granule before.
/// </summary>
internal static class Mp3Hybrid
{
    /// <summary>The windows in a block of short windows.</summary>
    private const int Windows = 3;

    /// <summary>The subbands at the bottom of a mixed block that use the long transform.</summary>
    private const int MixedLongSubbands = 2;

    /// <summary>The values a long transform produces for a subband.</summary>
    private const int LongOutputs = 36;

    /// <summary>The samples of the overlap kept for the next granule.</summary>
    private const int Overlap = Mp3Tables.SubbandSamples;

    /// <summary>The block type of the start window.</summary>
    private const int StartBlock = 1;

    /// <summary>The block type of the stop window.</summary>
    private const int StopBlock = 3;

    /// <summary>The window shape of the stop block in <see cref="Mp3Tables.LongTransform"/>.</summary>
    private const int StopShape = 2;

    /// <summary>The first output of the first short window in the 36 outputs of a subband.</summary>
    private const int ShortStart = 6;

    /// <summary>The distance between the starts of neighbouring short windows.</summary>
    private const int ShortStride = 6;

    /// <summary>Runs the filterbank for one channel of one granule.</summary>
    /// <param name="header">The frame header.</param>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="widths">The widths of the scale factor entries.</param>
    /// <param name="used">The number of coefficients up to the last one that is not zero.</param>
    /// <param name="coefficients">The 576 coefficients; reordered and de-aliased in place.</param>
    /// <param name="overlap">The second half of the last granule's transforms, 576 values; replaced.</param>
    /// <param name="slots">Receives the samples, 32 for each of the 18 time steps, one for each subband.</param>
    internal static void Run(in Mp3FrameHeader header, Mp3GranuleChannel info, ReadOnlySpan<byte> widths, int used, Span<float> coefficients, Span<float> overlap, Span<float> slots)
    {
        if (info.IsShort)
        {
            Reorder(widths, info.MixedBlock ? Mp3Dequantizer.MixedLongEntries(header) : 0, coefficients);
        }

        var plan = PlanOf(info, used);
        ReduceAliasing(coefficients, Math.Min(plan.LongSubbands, plan.UsedSubbands + 1));
        Span<float> output = stackalloc float[Mp3Tables.LongRowLength];
        for (var subband = 0; subband < Mp3Tables.Subbands; subband++)
        {
            var input = coefficients.Slice(subband * Overlap, Overlap);
            if (subband <= plan.UsedSubbands && input.ContainsAnyExcept(0F))
            {
                Transform(input, output, subband < plan.LongSubbands, plan.Shape);
            }
            else
            {
                output.Clear();
            }

            Overlay(output, overlap.Slice(subband * Overlap, Overlap), slots, subband);
        }
    }

    /// <summary>Works out which transform each subband of a granule gets.</summary>
    /// <param name="info">The coding of the granule.</param>
    /// <param name="used">The number of coefficients up to the last one coded.</param>
    /// <returns>The plan.</returns>
    private static SubbandPlan PlanOf(Mp3GranuleChannel info, int used)
    {
        var shape = info.BlockType switch
        {
            StartBlock => 1,
            StopBlock => StopShape,
            _ => 0,
        };
        var longSubbands = Mp3Tables.Subbands;
        var usedSubbands = (used + Overlap - 1) / Overlap;
        if (info.IsShort)
        {
            // Putting the windows in time order spreads the lines of a band over the whole width of the band's three windows, so any
            // subband above the last coded line may hold data.
            longSubbands = info.MixedBlock ? MixedLongSubbands : 0;
            usedSubbands = Mp3Tables.Subbands - 1;
        }

        return new(longSubbands, shape, usedSubbands);
    }

    /// <summary>Puts the coefficients of short windows in time order: the lines of the three windows of each band, which are stored one window after another, are interleaved.</summary>
    /// <param name="widths">The widths of the scale factor entries.</param>
    /// <param name="longEntries">The entries at the start that are long bands and stay as they are.</param>
    /// <param name="coefficients">The 576 coefficients; changed in place.</param>
    private static void Reorder(ReadOnlySpan<byte> widths, int longEntries, Span<float> coefficients)
    {
        Span<float> scratch = stackalloc float[Mp3Tables.GranuleSamples];
        var line = 0;
        for (var entry = 0; entry < longEntries; entry++)
        {
            line += widths[entry];
        }

        var start = line;
        for (var entry = longEntries; entry + Windows <= widths.Length; entry += Windows)
        {
            var width = widths[entry];
            for (var index = 0; index < width; index++)
            {
                for (var window = 0; window < Windows; window++)
                {
                    scratch[line - start + (index * Windows) + window] = coefficients[line + (window * width) + index];
                }
            }

            line += width * Windows;
        }

        scratch[..(line - start)].CopyTo(coefficients[start..]);
    }

    /// <summary>Undoes the aliasing between each pair of neighbouring subbands.</summary>
    /// <param name="coefficients">The 576 coefficients.</param>
    /// <param name="boundaries">The subband boundaries to work on, counted from the first.</param>
    private static void ReduceAliasing(Span<float> coefficients, int boundaries)
    {
        var cosine = Mp3Tables.AliasCosine.Span;
        var sine = Mp3Tables.AliasSine.Span;
        for (var boundary = 1; boundary < boundaries; boundary++)
        {
            var edge = boundary * Overlap;
            for (var index = 0; index < Mp3Tables.AliasButterflies; index++)
            {
                var lower = coefficients[edge - 1 - index];
                var upper = coefficients[edge + index];
                coefficients[edge - 1 - index] = (lower * cosine[index]) - (upper * sine[index]);
                coefficients[edge + index] = (upper * cosine[index]) + (lower * sine[index]);
            }
        }
    }

    /// <summary>Runs the inverse MDCT of one subband.</summary>
    /// <param name="input">The 18 coefficients.</param>
    /// <param name="output">Receives 36 values, in a span of the padded row length.</param>
    /// <param name="useLong">Whether to use the long transform rather than three short ones.</param>
    /// <param name="shape">The window shape of the long transform.</param>
    private static void Transform(ReadOnlySpan<float> input, Span<float> output, bool useLong, int shape)
    {
        output.Clear();
        if (useLong)
        {
            var rows = Mp3Tables.LongTransform.Span.Slice(shape * Overlap * Mp3Tables.LongRowLength, Overlap * Mp3Tables.LongRowLength);
            for (var index = 0; index < Overlap; index++)
            {
                AddScaled(output, rows.Slice(index * Mp3Tables.LongRowLength, Mp3Tables.LongRowLength), input[index]);
            }

            return;
        }

        var table = Mp3Tables.ShortTransform.Span;
        for (var window = 0; window < Windows; window++)
        {
            for (var index = 0; index < Mp3Tables.ShortInputs; index++)
            {
                var value = input[(index * Windows) + window];
                var row = table.Slice(index * Mp3Tables.ShortOutputs, Mp3Tables.ShortOutputs);
                var target = output.Slice(ShortStart + (window * ShortStride), Mp3Tables.ShortOutputs);
                for (var sample = 0; sample < row.Length; sample++)
                {
                    target[sample] += row[sample] * value;
                }
            }
        }
    }

    /// <summary>Adds a row times a number to the output.</summary>
    /// <param name="output">The output, changed.</param>
    /// <param name="row">The row.</param>
    /// <param name="factor">The number.</param>
    private static void AddScaled(Span<float> output, ReadOnlySpan<float> row, float factor)
    {
        var scale = new Vector<float>(factor);
        for (var index = 0; index < row.Length; index += Vector<float>.Count)
        {
            var sum = new Vector<float>(output[index..]) + (new Vector<float>(row[index..]) * scale);
            sum.CopyTo(output[index..]);
        }
    }

    /// <summary>Adds the transform to the overlap of the granule before, stores the samples and keeps the second half for the next granule.</summary>
    /// <param name="output">The 36 values of the transform.</param>
    /// <param name="tail">The overlap of this subband from the granule before; replaced.</param>
    /// <param name="slots">Receives the samples.</param>
    /// <param name="subband">The subband.</param>
    private static void Overlay(ReadOnlySpan<float> output, Span<float> tail, Span<float> slots, int subband)
    {
        var odd = (subband & 1) != 0;
        for (var time = 0; time < Overlap; time++)
        {
            var sample = output[time] + tail[time];

            // Every second sample of every second subband is turned over to undo the frequency inversion the filterbank adds.
            slots[(time * Mp3Tables.Subbands) + subband] = odd && (time & 1) != 0 ? -sample : sample;
            tail[time] = output[Overlap + time];
        }
    }

    /// <summary>Which transform each subband of a granule gets.</summary>
    /// <param name="LongSubbands">The subbands from the bottom that use the long transform.</param>
    /// <param name="Shape">The window shape of the long transform.</param>
    /// <param name="UsedSubbands">The subbands up to the last that has coefficients.</param>
    private readonly record struct SubbandPlan(int LongSubbands, int Shape, int UsedSubbands);
}
