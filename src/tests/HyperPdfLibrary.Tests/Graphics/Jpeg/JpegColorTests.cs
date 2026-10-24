// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests for <see cref="JpegColor"/>, whose vector path must give the bytes of libjpeg's fixed-point formula.</summary>
public sealed class JpegColorTests
{
    /// <summary>The seed of the random rows.</summary>
    private const uint Seed = 20_260_523;

    /// <summary>The components of a YCbCr row.</summary>
    private const int Components = 3;

    /// <summary>The row of the red chroma samples.</summary>
    private const int RedRow = 2;

    /// <summary>The position of blue in an RGB pixel.</summary>
    private const int BlueChannel = 2;

    /// <summary>The fixed-point bits of libjpeg's constants.</summary>
    private const int FixedBits = 16;

    /// <summary>Half a unit in fixed point.</summary>
    private const int Half = 1 << (FixedBits - 1);

    /// <summary>The offset of chroma samples.</summary>
    private const int ChromaCenter = 128;

    /// <summary>The largest sample.</summary>
    private const int MaxSample = 255;

    /// <summary>1.40200 in fixed point.</summary>
    private const int RedFromCr = 91_881;

    /// <summary>1.77200 in fixed point.</summary>
    private const int BlueFromCb = 116_130;

    /// <summary>0.71414 in fixed point.</summary>
    private const int GreenFromCr = 46_802;

    /// <summary>0.34414 in fixed point.</summary>
    private const int GreenFromCb = 22_554;

    /// <summary>The widths tried, around the sixteen pixels a vector step takes.</summary>
    private static readonly int[] Widths = [1, 15, 16, 17, 31, 32, 33, 100, 1_001];

    /// <summary>Random YCbCr rows convert to the same RGB bytes as the formula, whatever the width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Interleave_RandomRows_MatchesFixedPointFormula()
    {
        var random = new SampleRandom(Seed);
        foreach (var width in Widths)
        {
            var rows = random.NextBytes(width * Components);
            var actual = new byte[width * Components];
            JpegColor.Interleave(rows, Components, width, true, actual);

            await Assert.That(actual).IsEquivalentTo(Reference(rows, width)).Because($"width {width}");
        }
    }

    /// <summary>Converts rows with the formula, one pixel at a time.</summary>
    /// <param name="rows">The Y, Cb and Cr rows.</param>
    /// <param name="width">The pixels in a row.</param>
    /// <returns>The RGB pixels.</returns>
    private static byte[] Reference(byte[] rows, int width)
    {
        var rgb = new byte[width * Components];
        for (var x = 0; x < width; x++)
        {
            int y = rows[x];
            var blue = rows[width + x] - ChromaCenter;
            var red = rows[(RedRow * width) + x] - ChromaCenter;
            rgb[x * Components] = Clamp(y + (((RedFromCr * red) + Half) >> FixedBits));
            rgb[(x * Components) + 1] = Clamp(y + ((Half - (GreenFromCb * blue) - (GreenFromCr * red)) >> FixedBits));
            rgb[(x * Components) + BlueChannel] = Clamp(y + (((BlueFromCb * blue) + Half) >> FixedBits));
        }

        return rgb;
    }

    /// <summary>Clamps a value to a byte.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value from 0 to 255.</returns>
    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, MaxSample);
}
