// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The forward 5/3 reversible wavelet of ISO 15444-1 annex F, written from the equations with explicit symmetric
/// extension: each level filters columns, then rows, and leaves low-pass samples before high-pass ones.
/// </summary>
internal static class JpxTestWavelet
{
    /// <summary>The samples of each low-pass and high-pass pair.</summary>
    private const int Pair = 2;

    /// <summary>The rounding offset of the update step.</summary>
    private const int UpdateRounding = 2;

    /// <summary>The divisor of the update step.</summary>
    private const int UpdateDivisor = 4;

    /// <summary>Transforms a tile-component in place.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="buffer">The samples, replaced by the sub-bands.</param>
    internal static void Forward(JpxTile tile, in JpxTileComponent component, int[] buffer) =>
        Forward(tile, component, buffer, component.Levels);

    /// <summary>Transforms a tile-component in place, running only the finest decomposition levels.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="buffer">The samples, replaced by the sub-bands.</param>
    /// <param name="decompositions">The levels to run, starting from the full resolution; the low-pass band of the last one is the image at a reduced resolution.</param>
    internal static void Forward(JpxTile tile, in JpxTileComponent component, int[] buffer, int decompositions)
    {
        var stride = component.Area.Width;
        for (var r = component.Levels; r > component.Levels - decompositions; r--)
        {
            var area = tile.Resolutions[component.FirstResolution + r].Area;
            var low = tile.Resolutions[component.FirstResolution + r - 1].Area;
            for (var x = 0; x < area.Width; x++)
            {
                var column = new int[area.Height];
                for (var y = 0; y < area.Height; y++)
                {
                    column[y] = buffer[(y * stride) + x];
                }

                var result = Line(column, area.Y0, low.Height);
                for (var y = 0; y < area.Height; y++)
                {
                    buffer[(y * stride) + x] = result[y];
                }
            }

            for (var y = 0; y < area.Height; y++)
            {
                Line(buffer.AsSpan(y * stride, area.Width).ToArray(), area.X0, low.Width).CopyTo(buffer.AsSpan(y * stride));
            }
        }
    }

    /// <summary>Transforms one line and puts the low-pass samples first.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="start">The line's first coordinate, whose parity says which samples are low-pass.</param>
    /// <param name="lowCount">The low-pass sample count.</param>
    /// <returns>The transformed line.</returns>
    private static int[] Line(int[] samples, int start, int lowCount)
    {
        var count = samples.Length;
        if (count == 1)
        {
            // A lone sample on an odd coordinate is high-pass and doubles (equation F-7 inverted).
            return [(start & 1) == 1 ? samples[0] * Pair : samples[0]];
        }

        var predicted = (int[])samples.Clone();
        for (var i = 0; i < count; i++)
        {
            if (((start + i) & 1) == 1)
            {
                predicted[i] = samples[i] - (int)Math.Floor((At(samples, i - 1) + At(samples, i + 1)) / (double)Pair);
            }
        }

        var updated = (int[])predicted.Clone();
        for (var i = 0; i < count; i++)
        {
            if (((start + i) & 1) == 0)
            {
                updated[i] = predicted[i] + (int)Math.Floor((At(predicted, i - 1) + At(predicted, i + 1) + UpdateRounding) / (double)UpdateDivisor);
            }
        }

        var result = new int[count];
        var lowIndex = 0;
        var highIndex = lowCount;
        for (var i = 0; i < count; i++)
        {
            if (((start + i) & 1) == 0)
            {
                result[lowIndex] = updated[i];
                lowIndex++;
            }
            else
            {
                result[highIndex] = updated[i];
                highIndex++;
            }
        }

        return result;
    }

    /// <summary>Reads a sample with whole-sample symmetric extension at both ends.</summary>
    /// <param name="samples">The samples.</param>
    /// <param name="index">The index, which may be one outside the line.</param>
    /// <returns>The sample.</returns>
    private static int At(int[] samples, int index)
    {
        if (index < 0)
        {
            index = -index;
        }

        if (index >= samples.Length)
        {
            index = (Pair * (samples.Length - 1)) - index;
        }

        return samples[index];
    }
}
