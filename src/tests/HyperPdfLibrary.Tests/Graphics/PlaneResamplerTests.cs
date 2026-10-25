// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests that <see cref="ImageAlpha.ResamplePlane"/> gives the bytes of averaging each target pixel's source block one pixel at a time.</summary>
public sealed class PlaneResamplerTests
{
    /// <summary>The seed of the random planes.</summary>
    private const uint Seed = 987_654;

    /// <summary>Two, which doubles an index to work in half pixels.</summary>
    private const int PairFactor = 2;

    /// <summary>The source and target sizes tried: source width, source height, target width, target height.</summary>
    private static readonly int[][] Sizes =
    [
        [1, 1, 1, 1], [1, 1, 5, 3], [7, 5, 3, 2], [8, 8, 8, 8], [9, 4, 2, 7], [33, 21, 5, 4], [100, 64, 37, 41], [64, 100, 130, 77], [1_000, 3, 1, 1], [3, 1_000, 2, 999],
    ];

    /// <summary>Random planes resample to the same bytes as the per pixel average, whatever the sizes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResamplePlane_RandomPlanes_MatchesPerPixelAverage()
    {
        var random = new SampleRandom(Seed);
        foreach (var size in Sizes)
        {
            var sourceWidth = size[0];
            var sourceHeight = size[1];
            var width = size[2];
            var height = size[3];
            var source = random.NextBytes(sourceWidth * sourceHeight);
            var actual = new byte[width * height];
            ImageAlpha.ResamplePlane(source, sourceWidth, sourceHeight, actual, width, height);

            await Assert.That(actual).IsEquivalentTo(Reference(source, sourceWidth, sourceHeight, width, height)).Because($"{sourceWidth}x{sourceHeight} to {width}x{height}");
        }
    }

    /// <summary>Resamples a plane one target pixel at a time.</summary>
    /// <param name="source">The source plane.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <returns>The resampled plane.</returns>
    private static byte[] Reference(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        var target = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var rowLow = RangeStart(y, sourceHeight, height);
            var rowHigh = RangeEnd(y, sourceHeight, height, rowLow);
            for (var x = 0; x < width; x++)
            {
                var columnLow = RangeStart(x, sourceWidth, width);
                var columnHigh = RangeEnd(x, sourceWidth, width, columnLow);
                long sum = 0;
                for (var row = rowLow; row < rowHigh; row++)
                {
                    for (var column = columnLow; column < columnHigh; column++)
                    {
                        sum += source[(row * sourceWidth) + column];
                    }
                }

                var count = (columnHigh - columnLow) * (rowHigh - rowLow);
                target[(y * width) + x] = (byte)((sum + (count / PairFactor)) / count);
            }
        }

        return target;
    }

    /// <summary>Gets the first source index a target index covers.</summary>
    /// <param name="index">The target index.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="targetSize">The target size.</param>
    /// <returns>The first source index.</returns>
    private static int RangeStart(int index, int sourceSize, int targetSize) => targetSize < sourceSize
        ? (int)((long)index * sourceSize / targetSize)
        : (int)(((PairFactor * index) + 1L) * sourceSize / (PairFactor * (long)targetSize));

    /// <summary>Gets one past the last source index a target index covers.</summary>
    /// <param name="index">The target index.</param>
    /// <param name="sourceSize">The source size.</param>
    /// <param name="targetSize">The target size.</param>
    /// <param name="start">The first source index.</param>
    /// <returns>The end of the covered range.</returns>
    private static int RangeEnd(int index, int sourceSize, int targetSize, int start) => targetSize < sourceSize
        ? Math.Max(start + 1, (int)((long)(index + 1) * sourceSize / targetSize))
        : start + 1;
}
