// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests that the vector upscaler in <see cref="ImageAlpha.UpscaleBgra"/> gives the bytes of interpolating each channel on its own.</summary>
public sealed class BgraUpscalerTests
{
    /// <summary>The seed of the random pixels.</summary>
    private const uint Seed = 424_242;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>Half a pixel.</summary>
    private const float CenterOffset = 0.5F;

    /// <summary>The source and target sizes tried: source width, source height, target width, target height.</summary>
    private static readonly int[][] Sizes = [[1, 1, 5, 3], [3, 2, 7, 5], [16, 9, 17, 10], [10, 10, 10, 10], [33, 21, 130, 77]];

    /// <summary>Random images enlarge to the same bytes as the per channel formula.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UpscaleBgra_RandomImages_MatchesPerChannelInterpolation()
    {
        var random = new SampleRandom(Seed);
        foreach (var size in Sizes)
        {
            var sourceWidth = size[0];
            var sourceHeight = size[1];
            var width = size[2];
            var height = size[3];
            var source = random.NextBytes(sourceWidth * sourceHeight * PixelBytes);
            var actual = new byte[width * height * PixelBytes];
            ImageAlpha.UpscaleBgra(source, sourceWidth, sourceHeight, actual, width, height);

            await Assert.That(actual).IsEquivalentTo(Reference(source, sourceWidth, sourceHeight, width, height)).Because($"{sourceWidth}x{sourceHeight} to {width}x{height}");
        }
    }

    /// <summary>Enlarges an image one channel at a time.</summary>
    /// <param name="source">The source pixels.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="width">The target width.</param>
    /// <param name="height">The target height.</param>
    /// <returns>The enlarged pixels.</returns>
    private static byte[] Reference(byte[] source, int sourceWidth, int sourceHeight, int width, int height)
    {
        var target = new byte[width * height * PixelBytes];
        for (var y = 0; y < height; y++)
        {
            var sourceY = Math.Max(((y + CenterOffset) * sourceHeight / height) - CenterOffset, 0);
            var y0 = Math.Min((int)sourceY, sourceHeight - 1);
            var y1 = Math.Min(y0 + 1, sourceHeight - 1);
            for (var x = 0; x < width; x++)
            {
                var sourceX = Math.Max(((x + CenterOffset) * sourceWidth / width) - CenterOffset, 0);
                var x0 = Math.Min((int)sourceX, sourceWidth - 1);
                var x1 = Math.Min(x0 + 1, sourceWidth - 1);
                for (var c = 0; c < PixelBytes; c++)
                {
                    var top = Lerp(source[(((y0 * sourceWidth) + x0) * PixelBytes) + c], source[(((y0 * sourceWidth) + x1) * PixelBytes) + c], sourceX - x0);
                    var bottom = Lerp(source[(((y1 * sourceWidth) + x0) * PixelBytes) + c], source[(((y1 * sourceWidth) + x1) * PixelBytes) + c], sourceX - x0);
                    target[(((y * width) + x) * PixelBytes) + c] = (byte)(Lerp(top, bottom, sourceY - y0) + CenterOffset);
                }
            }
        }

        return target;
    }

    /// <summary>Interpolates between two values.</summary>
    /// <param name="from">The value at fraction 0.</param>
    /// <param name="to">The value at fraction 1.</param>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The interpolated value.</returns>
    private static float Lerp(float from, float to, float fraction) => from + ((to - from) * fraction);
}
