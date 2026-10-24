// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Turns a picture of one line of text into the values a model reads: scaled to the model's height and stretched so the ink and paper fill the range -1 to 1.</summary>
public static class LinePreparer
{
    /// <summary>The number of grey levels.</summary>
    private const int Levels = 256;

    /// <summary>The brightest level.</summary>
    private const int White = Levels - 1;

    /// <summary>The share of the dark samples the black point is taken from the bottom of.</summary>
    private const double BlackShare = 0.25;

    /// <summary>The share of the light samples the white point is taken from the bottom of.</summary>
    private const double WhiteShare = 0.75;

    /// <summary>Half, the share of the range between black and white the contrast covers on each side of the middle.</summary>
    private const float Half = 0.5F;

    /// <summary>Works out the width a line has once scaled to the model's height.</summary>
    /// <param name="width">The line's width.</param>
    /// <param name="height">The line's height.</param>
    /// <param name="targetHeight">The height the model takes.</param>
    /// <returns>The width the line has once scaled.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ScaledWidth(int width, int height, int targetHeight) =>
        Math.Max(1, (int)Math.Round((double)width * targetHeight / height, MidpointRounding.AwayFromZero));

    /// <summary>Prepares a line.</summary>
    /// <param name="grey">The pixels of the line, row after row.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="targetHeight">The height the model takes.</param>
    /// <param name="scratch">The working memory.</param>
    /// <returns>The values for the model, one channel, from the scratch memory.</returns>
    public static FeatureMap Prepare(ReadOnlySpan<byte> grey, int width, int height, int targetHeight, RecognitionScratch scratch)
    {
        ArgumentNullException.ThrowIfNull(scratch);
        var scaledWidth = ScaledWidth(width, height, targetHeight);
        var count = scaledWidth * targetHeight;
        var scaled = scratch.RentBytes(count);
        GreyScaler.Scale(grey, width, height, scaled, scaledWidth, targetHeight, scratch);
        var (black, white) = ComputeBlackWhite(scaled.AsSpan((targetHeight >> 1) * scaledWidth, scaledWidth));
        var contrast = (white - black) * Half;
        if (contrast <= 0F)
        {
            contrast = 1F;
        }

        var map = scratch.RentMap(targetHeight, scaledWidth, 1);
        for (var i = 0; i < count; i++)
        {
            map.Values[i] = ((scaled[i] - black) / contrast) - 1F;
        }

        scratch.Return(scaled);
        return map;
    }

    /// <summary>Determines whether a sample is a low point of its row.</summary>
    /// <param name="previous">The sample before.</param>
    /// <param name="current">The sample.</param>
    /// <param name="next">The sample after.</param>
    /// <returns><see langword="true"/> when it is lower than one neighbour and no higher than the other.</returns>
    private static bool IsLowPoint(int previous, int current, int next) =>
        (current < previous && current <= next) || (current <= previous && current < next);

    /// <summary>Determines whether a sample is a high point of its row.</summary>
    /// <param name="previous">The sample before.</param>
    /// <param name="current">The sample.</param>
    /// <param name="next">The sample after.</param>
    /// <returns><see langword="true"/> when it is higher than one neighbour and no lower than the other.</returns>
    private static bool IsHighPoint(int previous, int current, int next) =>
        (current > previous && current >= next) || (current >= previous && current > next);

    /// <summary>
    /// Finds the grey levels to treat as black and white. A row through the middle of a line of text crosses some of it, so
    /// the row's low points sample the ink and its high points sample the paper.
    /// </summary>
    /// <param name="row">The pixels of the middle row.</param>
    /// <returns>The black and white levels.</returns>
    private static BlackWhite ComputeBlackWhite(ReadOnlySpan<byte> row)
    {
        Span<int> lows = stackalloc int[Levels];
        Span<int> highs = stackalloc int[Levels];
        var lowCount = 0;
        var highCount = 0;
        for (var x = 1; x + 1 < row.Length; x++)
        {
            if (IsLowPoint(row[x - 1], row[x], row[x + 1]))
            {
                lows[row[x]]++;
                lowCount++;
            }

            if (!IsHighPoint(row[x - 1], row[x], row[x + 1]))
            {
                continue;
            }

            highs[row[x]]++;
            highCount++;
        }

        if (lowCount == 0)
        {
            lows[0] = 1;
            lowCount = 1;
        }

        if (highCount == 0)
        {
            highs[White] = 1;
            highCount = 1;
        }

        return new((float)Percentile(lows, lowCount, BlackShare), (float)Percentile(highs, highCount, WhiteShare));
    }

    /// <summary>Finds the level a share of the samples lie below, interpolating inside the level.</summary>
    /// <param name="counts">The number of samples at each level.</param>
    /// <param name="total">The total number of samples.</param>
    /// <param name="share">The share, from 0 to 1.</param>
    /// <returns>The level.</returns>
    private static double Percentile(ReadOnlySpan<int> counts, int total, double share)
    {
        var target = Math.Clamp(share * total, 1.0, total);
        var sum = 0;
        var index = 0;
        while (index < counts.Length && sum < target)
        {
            sum += counts[index];
            index++;
        }

        return index > 0 ? index - ((sum - target) / counts[index - 1]) : 0;
    }
}
