// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Export;
using HyperPdfLibrary.ImageFiles;
using HyperPdfLibrary.Tests.Filters;

namespace HyperPdfLibrary.Tests.ImageFiles;

/// <summary>Tests that bitmaps of 24 and 32-bit pixels decode to the pixels they were written from, whatever the width.</summary>
public sealed class BmpByteRowsTests
{
    /// <summary>The seed of the random pixels.</summary>
    private const uint Seed = 31_337;

    /// <summary>The height of each test image.</summary>
    private const int Height = 3;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The widths tried, around the pixels a vector step finishes and the four byte padding of rows.</summary>
    private static readonly int[] Widths = [1, 2, 3, 4, 5, 6, 7, 8, 9, 15, 16, 17, 31, 33, 100];

    /// <summary>Random 24-bit pictures survive a round trip through a bitmap file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Rgb24_RoundTripsThroughBmp()
    {
        var random = new SampleRandom(Seed);
        foreach (var width in Widths)
        {
            var source = new PdfRasterImage(PdfRasterFormat.Rgb24, width, Height, random.NextBytes(width * Height * RgbBytes));

            var decoded = PdfImageFiles.Decode(PdfRasterFiles.Encode(source, PdfRasterFileFormat.Bmp, CancellationToken.None), CancellationToken.None);

            await Assert.That(decoded.Format).IsEqualTo(PdfRasterFormat.Rgb24).Because($"width {width}");
            await Assert.That(decoded.Pixels.ToArray()).IsEquivalentTo(source.Pixels.ToArray()).Because($"width {width}");
        }
    }

    /// <summary>Random pictures with alpha survive a round trip through a bitmap file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Bgra32_RoundTripsThroughBmp()
    {
        var random = new SampleRandom(Seed);
        foreach (var width in Widths)
        {
            var source = new PdfRasterImage(PdfRasterFormat.Bgra32, width, Height, random.NextBytes(width * Height * BgraBytes));

            var decoded = PdfImageFiles.Decode(PdfRasterFiles.Encode(source, PdfRasterFileFormat.Bmp, CancellationToken.None), CancellationToken.None);

            await Assert.That(decoded.Format).IsEqualTo(PdfRasterFormat.Bgra32).Because($"width {width}");
            await Assert.That(decoded.Pixels.ToArray()).IsEquivalentTo(source.Pixels.ToArray()).Because($"width {width}");
        }
    }
}
