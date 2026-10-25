// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Checks that the vectorised sample writing gives the bytes the plain loop gives.</summary>
public sealed class JpxSampleWriterTests
{
    /// <summary>The samples of an RGB pixel.</summary>
    private const int Rgb = 3;

    /// <summary>The index of the blue channel.</summary>
    private const int BlueChannel = 2;

    /// <summary>The multiplier that scrambles positions into sample values.</summary>
    private const int Scramble = 37;

    /// <summary>The highest 8-bit sample plus one.</summary>
    private const int ByteRange = 256;

    /// <summary>Rows of every length around the vector width interleave to the same bytes as the plain loop.</summary>
    /// <param name="width">The pixels in the row.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(15)]
    [Arguments(16)]
    [Arguments(17)]
    [Arguments(31)]
    [Arguments(32)]
    [Arguments(33)]
    [Arguments(100)]
    [Arguments(1023)]
    public async Task InterleaveRgbMatchesPlainLoop(int width)
    {
        var red = Samples(width, 0);
        var green = Samples(width, 1);
        var blue = Samples(width, BlueChannel);
        var expected = new byte[width * Rgb];
        for (var i = 0; i < width; i++)
        {
            expected[i * Rgb] = (byte)red[i];
            expected[(i * Rgb) + 1] = (byte)green[i];
            expected[(i * Rgb) + BlueChannel] = (byte)blue[i];
        }

        var actual = new byte[width * Rgb];
        JpxSampleWriter.InterleaveRgb(red, green, blue, actual);

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>Makes sample values from 0 to 255 that differ by channel and position.</summary>
    /// <param name="width">The count.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The values.</returns>
    private static int[] Samples(int width, int channel)
    {
        var values = new int[width];
        for (var i = 0; i < width; i++)
        {
            values[i] = ((i * Scramble) + (channel * ByteRange / Rgb) + (i >> 3)) % ByteRange;
        }

        return values;
    }
}
