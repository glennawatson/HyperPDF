// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.ImageFiles;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.ImageFiles;

/// <summary>Tests that the eight bit TIFF conversions give the pictures the general per sample conversion gives.</summary>
public sealed class TiffEightBitTests
{
    /// <summary>The seed of the random samples.</summary>
    private const uint Seed = 4_242_424;

    /// <summary>The height of each test picture.</summary>
    private const int Height = 3;

    /// <summary>The bits in each sample.</summary>
    private const int BitsPerSample = 8;

    /// <summary>The photometric value where zero is white.</summary>
    private const int WhiteIsZero = 0;

    /// <summary>The photometric value where zero is black.</summary>
    private const int BlackIsZero = 1;

    /// <summary>The photometric value for red, green and blue.</summary>
    private const int Rgb = 2;

    /// <summary>The photometric value for a palette.</summary>
    private const int Palette = 3;

    /// <summary>The meaning of an alpha sample that is not premultiplied.</summary>
    private const int UnassociatedAlpha = 2;

    /// <summary>The colours in an eight bit palette, three channels.</summary>
    private const int PaletteValues = 3 * 256;

    /// <summary>The widths tried, around the pixels a vector finishes.</summary>
    private static readonly int[] Widths = [1, 2, 3, 4, 5, 7, 16, 17, 33];

    /// <summary>The layouts tried: photometric value, samples in a pixel and the meaning of the alpha sample.</summary>
    private static readonly int[][] Layouts = [[WhiteIsZero, 1, 0], [BlackIsZero, 1, 0], [Rgb, 3, 0], [Rgb, 4, UnassociatedAlpha], [Palette, 1, 0]];

    /// <summary>Each layout converts to the same picture by the fast and the general route.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Convert_EveryLayout_MatchesTheGeneralConversion()
    {
        var random = new SampleRandom(Seed);
        foreach (var layout in Layouts)
        {
            foreach (var width in Widths)
            {
                var info = new TiffInfo(width, Height, BitsPerSample, layout[1], 1, layout[0], false, 1, Height, false, 0, layout[2]);
                var bytes = random.NextBytes(width * Height * layout[1]);
                var colorMap = Map(random);
                var samples = bytes.Select(static value => (ushort)value).ToArray();

                var expected = TiffPixels.Convert(info, samples, colorMap);
                await Assert.That(TiffEightBit.Handles(info)).IsTrue();
                var actual = TiffEightBit.Convert(info, (byte[])bytes.Clone(), colorMap);

                await Assert.That(actual.Format).IsEqualTo(expected.Format).Because($"photometric {layout[0]}, {layout[1]} samples, width {width}");
                await Assert.That(actual.Pixels.ToArray()).IsEquivalentTo(expected.Pixels.ToArray()).Because($"photometric {layout[0]}, {layout[1]} samples, width {width}");
            }
        }
    }

    /// <summary>Makes a random palette of 16 bit values.</summary>
    /// <param name="random">The generator.</param>
    /// <returns>All the reds, then all the greens, then all the blues.</returns>
    private static uint[] Map(SampleRandom random)
    {
        var map = new uint[PaletteValues];
        for (var i = 0; i < map.Length; i++)
        {
            map[i] = (uint)random.Next(ushort.MaxValue + 1);
        }

        return map;
    }
}
