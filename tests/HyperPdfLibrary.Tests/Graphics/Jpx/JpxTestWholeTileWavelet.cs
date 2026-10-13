// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The inverse wavelet of a whole tile-component at once (ISO 15444-1 annex F): the sub-bands sit in place in one buffer,
/// each level rebuilds its rows horizontally and then lifts every column over the full height. The decoder never does
/// this, because it needs the whole tile in memory; the tests use it to check the numbers of the row-by-row rebuild.
/// </summary>
internal static class JpxTestWholeTileWavelet
{
    /// <summary>The samples of each low-pass and high-pass pair.</summary>
    private const int Pair = 2;

    /// <summary>Rebuilds a tile-component in place from its sub-bands.</summary>
    /// <param name="tile">The tile, which holds the resolution areas.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="buffer">The coefficients, sub-bands in place (floats as their bits for the 9/7 filter); replaced by the samples.</param>
    internal static void Inverse(JpxTile tile, in JpxTileComponent component, int[] buffer)
    {
        var stride = component.Area.Width;
        for (var r = 1; r <= component.Levels; r++)
        {
            var area = tile.Resolutions[component.FirstResolution + r].Area;
            var low = tile.Resolutions[component.FirstResolution + r - 1].Area;
            if (area.Width == 0 || area.Height == 0)
            {
                continue;
            }

            Horizontal(buffer, stride, area, low, component.Reversible);
            Vertical(buffer, stride, area, low, component.Reversible);
        }
    }

    /// <summary>Rebuilds every row of a resolution from its low-pass and high-pass halves.</summary>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="area">The resolution's area.</param>
    /// <param name="low">The next lower resolution's area.</param>
    /// <param name="reversible">Whether the 5/3 filter is used.</param>
    private static void Horizontal(int[] buffer, int stride, in JpxRectangle area, in JpxRectangle low, bool reversible)
    {
        var line = new int[area.Width];
        for (var y = 0; y < area.Height; y++)
        {
            var row = buffer.AsSpan(y * stride, area.Width);
            if (reversible)
            {
                JpxWavelet.SynthesizeRow53(row[..low.Width], row[low.Width..], area.X0 & 1, line);
            }
            else
            {
                var lows = MemoryMarshal.Cast<int, float>(row[..low.Width]);
                var highs = MemoryMarshal.Cast<int, float>(row[low.Width..]);
                JpxWavelet.SynthesizeRow97(lows, highs, area.X0 & 1, MemoryMarshal.Cast<int, float>(line.AsSpan()));
            }

            line.CopyTo(row);
        }
    }

    /// <summary>Lifts every column of a resolution over its full height and puts the rows in order.</summary>
    /// <param name="buffer">The tile-component.</param>
    /// <param name="stride">The tile-component width.</param>
    /// <param name="area">The resolution's area.</param>
    /// <param name="low">The next lower resolution's area.</param>
    /// <param name="reversible">Whether the 5/3 filter is used.</param>
    private static void Vertical(int[] buffer, int stride, in JpxRectangle area, in JpxRectangle low, bool reversible)
    {
        var width = area.Width;
        var work = new int[area.Height * width];
        for (var y = 0; y < area.Height; y++)
        {
            buffer.AsSpan(y * stride, width).CopyTo(work.AsSpan(y * width, width));
        }

        var parity = area.Y0 & 1;
        var highRows = area.Height - low.Height;
        var lows = work.AsSpan(0, low.Height * width);
        var highs = work.AsSpan(low.Height * width);
        if (reversible)
        {
            JpxWavelet.LiftRows53(lows, highs, low.Height, highRows, parity, width);
        }
        else
        {
            JpxWavelet.LiftRows97(MemoryMarshal.Cast<int, float>(lows), MemoryMarshal.Cast<int, float>(highs), low.Height, highRows, parity, width);
        }

        for (var y = 0; y < area.Height; y++)
        {
            var isLow = ((y + parity) & 1) == 0;
            var index = (y - (isLow ? parity : 1 - parity)) / Pair;
            var source = isLow ? lows.Slice(index * width, width) : highs.Slice(index * width, width);
            source.CopyTo(buffer.AsSpan(y * stride, width));
        }
    }
}
