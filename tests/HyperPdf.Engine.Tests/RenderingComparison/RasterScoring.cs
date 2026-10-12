// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Measures rasters against independent standard expectations or another raster.</summary>
internal static class RasterScoring
{
    /// <summary>Checks only the oracle's validated interior pixels.</summary>
    /// <param name="raster">The renderer's output.</param>
    /// <param name="oracle">The independent standard expectations.</param>
    /// <returns>Exact counts and errors for the supplied expectations.</returns>
    internal static RasterScore AgainstOracle(ComparisonRaster raster, StandardsOracle oracle)
    {
        if (raster.Width != oracle.Width || raster.Height != oracle.Height)
        {
            return default;
        }

        var score = new RasterScore(true, 0, 0, 0, 0, 0, 0, 0, 0);
        foreach (var region in oracle.Regions.Span)
        {
            score = ScoreRegion(raster, region, score);
        }

        return score;
    }

    /// <summary>Measures similarity; this score alone cannot establish standards conformance.</summary>
    /// <param name="actual">The first raster.</param>
    /// <param name="reference">The second raster.</param>
    /// <param name="tolerance">The largest allowed channel difference.</param>
    /// <returns>Exact counts and errors over the whole raster.</returns>
    internal static RasterScore Compare(ComparisonRaster actual, ComparisonRaster reference, byte tolerance)
    {
        if (actual.Width != reference.Width || actual.Height != reference.Height)
        {
            return default;
        }

        var score = new RasterScore(true, 0, 0, 0, 0, 0, 0, 0, 0);
        var expected = reference.Pixels.Span;
        for (var offset = 0; offset < expected.Length; offset += ComparisonRaster.BytesPerPixel)
        {
            score = AddPixel(score, actual.Pixels.Span.Slice(offset, ComparisonRaster.BytesPerPixel), ReadColor(expected, offset), tolerance);
        }

        return score;
    }

    /// <summary>Scores one disjoint expected rectangle.</summary>
    /// <param name="raster">The actual pixels.</param>
    /// <param name="region">The validated expectation.</param>
    /// <param name="score">The existing totals.</param>
    /// <returns>The accumulated totals.</returns>
    private static RasterScore ScoreRegion(ComparisonRaster raster, in ExpectedPixelRegion region, RasterScore score)
    {
        for (var y = region.Y; y < region.Y + region.Height; y++)
        {
            for (var x = region.X; x < region.X + region.Width; x++)
            {
                var offset = ((y * raster.Width) + x) * ComparisonRaster.BytesPerPixel;
                score = AddPixel(score, raster.Pixels.Span.Slice(offset, ComparisonRaster.BytesPerPixel), region.Expected, region.Tolerance);
            }
        }

        return score;
    }

    /// <summary>Adds one pixel's four channel errors.</summary>
    /// <param name="score">The existing totals.</param>
    /// <param name="actual">The actual BGRA channels.</param>
    /// <param name="expected">The expected BGRA channels.</param>
    /// <param name="tolerance">The largest allowed channel error.</param>
    /// <returns>The accumulated totals.</returns>
    private static RasterScore AddPixel(in RasterScore score, ReadOnlySpan<byte> actual, Bgra32Color expected, byte tolerance)
    {
        const int red = 2;
        const int alpha = 3;
        var blueError = Math.Abs(actual[0] - expected.Blue);
        var greenError = Math.Abs(actual[1] - expected.Green);
        var redError = Math.Abs(actual[red] - expected.Red);
        var alphaError = Math.Abs(actual[alpha] - expected.Alpha);
        var maximum = Math.Max(Math.Max(blueError, greenError), Math.Max(redError, alphaError));
        return new(
            true,
            score.CheckedPixels + 1,
            score.ErrorPixels + (maximum > tolerance ? 1 : 0),
            score.BlueAbsoluteError + blueError,
            score.GreenAbsoluteError + greenError,
            score.RedAbsoluteError + redError,
            score.AlphaAbsoluteError + alphaError,
            (byte)Math.Max(score.MaximumChannelError, maximum),
            score.AllowedDifferencePixels + (maximum > 0 && maximum <= tolerance ? 1 : 0));
    }

    /// <summary>Reads a tightly packed BGRA color.</summary>
    /// <param name="pixels">The raster bytes.</param>
    /// <param name="offset">The pixel's byte offset.</param>
    /// <returns>The four channels.</returns>
    private static Bgra32Color ReadColor(ReadOnlySpan<byte> pixels, int offset)
    {
        const int red = 2;
        const int alpha = 3;
        return new(pixels[offset], pixels[offset + 1], pixels[offset + red], pixels[offset + alpha]);
    }
}
