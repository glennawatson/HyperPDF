// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks the area average that reduces a decoded image by halvings.</summary>
public sealed class ImageBoxReductionTests
{
    /// <summary>The side of the checkerboards in pixels.</summary>
    private const int Side = 8;

    /// <summary>The sRGB value of an equal mix of black and white light: 0.5 linear is 188 in sRGB.</summary>
    private const int LightMix = 188;

    /// <summary>The sRGB value of half the coverage.</summary>
    private const int HalfCoverage = 128;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The largest difference accepted between computed and expected values.</summary>
    private const int Tolerance = 1;

    /// <summary>The width of the odd-sized test image.</summary>
    private const int OddWidth = 5;

    /// <summary>The height of the odd-sized test image.</summary>
    private const int OddHeight = 3;

    /// <summary>One halving.</summary>
    private const int One = 1;

    /// <summary>Two halvings.</summary>
    private const int Two = 2;

    /// <summary>Three halvings.</summary>
    private const int Three = 3;

    /// <summary>The pixels across one output pixel after two halvings.</summary>
    private const int FourPixels = 4;

    /// <summary>A dark gray used in the edge test.</summary>
    private const byte Dark = 40;

    /// <summary>A light gray used in the edge test.</summary>
    private const byte Light = 200;

    /// <summary>The alpha of one opaque pixel among four.</summary>
    private const int QuarterAlpha = 64;

    /// <summary>The width and height of the large JPEG used to check the level choice.</summary>
    private const int LargeSide = 1000;

    /// <summary>The page side the large image is drawn at, in points.</summary>
    private const int DrawnSide = 100;

    /// <summary>A scale at which the image is drawn larger than its pixels.</summary>
    private const int ZoomedScale = 100;

    /// <summary>A black and white checkerboard reduces to the grey of an equal mix of light, not to the darker midpoint of the codes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayCheckerboardAveragesAsLight()
    {
        var reduced = ImageBoxReduction.Reduce(Checkerboard(), One);

        await Assert.That(reduced.Width).IsEqualTo(Side / Two);
        await Assert.That(reduced.Height).IsEqualTo(Side / Two);
        await Assert.That(reduced.IsGray).IsTrue();
        foreach (var value in reduced.Pixels)
        {
            await Assert.That(Math.Abs(value - LightMix)).IsLessThanOrEqualTo(Tolerance);
        }
    }

    /// <summary>Two levels average a block of sixteen pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoLevelsAverageSixteenPixels()
    {
        var reduced = ImageBoxReduction.Reduce(Checkerboard(), Two);

        await Assert.That(reduced.Width).IsEqualTo(Side / FourPixels);
        await Assert.That(Math.Abs(reduced.Pixels[0] - LightMix)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>A flat colour stays that colour, so smooth images are not shifted by the conversion.</summary>
    /// <param name="value">The gray value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(77)]
    [Arguments(200)]
    [Arguments(255)]
    public async Task FlatGrayIsUnchanged(int value)
    {
        var reduced = ImageBoxReduction.Reduce(Gray(Side, Side, (_, _) => (byte)value), Two);

        foreach (var pixel in reduced.Pixels)
        {
            await Assert.That(pixel).IsEqualTo((byte)value);
        }
    }

    /// <summary>Stencil coverage is averaged as coverage, with no gamma.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilCoverageAveragesLinearly()
    {
        var pixels = new byte[Side * Side];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (i % Side) % Two == 0 ? byte.MaxValue : (byte)0;
        }

        var reduced = ImageBoxReduction.Reduce(new(Side, Side, pixels, true, false, PdfImageCodec.None), One);

        await Assert.That(reduced.IsStencilMask).IsTrue();
        foreach (var value in reduced.Pixels)
        {
            await Assert.That(Math.Abs(value - HalfCoverage)).IsLessThanOrEqualTo(Tolerance);
        }
    }

    /// <summary>An odd size keeps the partial edge blocks and averages only the pixels they hold.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OddSizesAverageTheEdgeBlocks()
    {
        var reduced = ImageBoxReduction.Reduce(Gray(OddWidth, OddHeight, static (x, _) => x == OddWidth - 1 ? Dark : Light), One);

        await Assert.That(reduced.Width).IsEqualTo(Three);
        await Assert.That(reduced.Height).IsEqualTo(Two);
        await Assert.That(reduced.Pixels[0]).IsEqualTo(Light);
        await Assert.That(reduced.Pixels[Two]).IsEqualTo(Dark);
    }

    /// <summary>Opaque colour pixels average per channel as light.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpaqueColorAveragesPerChannel()
    {
        var pixels = new byte[Side * Side * BytesPerPixel];
        for (var i = 0; i < Side * Side; i++)
        {
            pixels[i * BytesPerPixel] = (i % Side) % Two == 0 ? byte.MaxValue : (byte)0;
            pixels[(i * BytesPerPixel) + One] = byte.MaxValue;
            pixels[(i * BytesPerPixel) + Two] = 0;
            pixels[(i * BytesPerPixel) + Three] = byte.MaxValue;
        }

        var reduced = ImageBoxReduction.Reduce(new(Side, Side, pixels, false, false, PdfImageCodec.None), One);

        await Assert.That(Math.Abs(reduced.Pixels[0] - LightMix)).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(reduced.Pixels[One]).IsEqualTo(byte.MaxValue);
        await Assert.That(reduced.Pixels[Two]).IsEqualTo((byte)0);
        await Assert.That(reduced.Pixels[Three]).IsEqualTo(byte.MaxValue);
    }

    /// <summary>A transparent pixel does not darken or colour its neighbours, and the average alpha is the mean.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransparentPixelsAreWeightedByAlpha()
    {
        var pixels = new byte[Two * Two * BytesPerPixel];

        // One opaque white pixel and three clear pixels.
        pixels[0] = byte.MaxValue;
        pixels[One] = byte.MaxValue;
        pixels[Two] = byte.MaxValue;
        pixels[Three] = byte.MaxValue;

        var reduced = ImageBoxReduction.Reduce(new(Two, Two, pixels, false, false, PdfImageCodec.None), One);

        // Alpha is a quarter; the colour is still white, so the premultiplied channels equal alpha.
        await Assert.That(Math.Abs(reduced.Pixels[Three] - QuarterAlpha)).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(Math.Abs(reduced.Pixels[0] - reduced.Pixels[Three])).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>No levels, or an image without pixels, returns the image itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingToReduceReturnsTheSource()
    {
        var gray = Gray(Side, Side, static (_, _) => 1);
        var unsupported = new PdfImageData(Side, Side, [], false, false, PdfImageCodec.Jpeg2000);

        await Assert.That(ReferenceEquals(ImageBoxReduction.Reduce(gray, 0), gray)).IsTrue();
        await Assert.That(ReferenceEquals(ImageBoxReduction.Reduce(unsupported, One), unsupported)).IsTrue();
    }

    /// <summary>Codecs that decode a lower resolution reduce natively; other codecs are averaged after decoding, but only when asked to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherCodecsAreAveragedOnlyWhenAsked()
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(LargeSide));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(LargeSide));
        dictionary.Add(KnownName.Filter, PdfValue.FromName(KnownName.DCTDecode));
        var matrix = Matrix3x2.CreateScale(DrawnSide, DrawnSide);

        await Assert.That(ImageReduction.Select(dictionary, matrix, 1, false)).IsEqualTo(0);
        await Assert.That(ImageReduction.Select(dictionary, matrix, 1, true)).IsEqualTo(Three);
        await Assert.That(ImageReduction.NativeLevels(dictionary, Three)).IsEqualTo(0);

        dictionary.Add(KnownName.Mask, PdfValue.FromArray(PdfArray.FromNumbers(null, [0, 0])));
        await Assert.That(ImageReduction.Select(dictionary, matrix, 1, true)).IsEqualTo(Three);
        await Assert.That(ImageReduction.Select(dictionary, matrix, ZoomedScale, true)).IsEqualTo(0);
    }

    /// <summary>A codec that took off some halvings leaves only the rest to the area average; one that took them all leaves none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyTheHalvingsTheCodecLeftAreAveraged()
    {
        await Assert.That(ImageReduction.RemainingLevels(LargeSide, LargeSide, Three)).IsEqualTo(Three);
        await Assert.That(ImageReduction.RemainingLevels(LargeSide, LargeSide / Two, Three)).IsEqualTo(Two);
        await Assert.That(ImageReduction.RemainingLevels(LargeSide, ImageBoxReduction.ReducedSize(LargeSide, Three), Three)).IsEqualTo(0);
        await Assert.That(ImageReduction.RemainingLevels(LargeSide, LargeSide / Three, Three)).IsEqualTo(0);
    }

    /// <summary>Creates the one pixel black and white checkerboard.</summary>
    /// <returns>The image.</returns>
    private static PdfImageData Checkerboard() => Gray(Side, Side, static (x, y) => ((x + y) & 1) == 0 ? (byte)0 : byte.MaxValue);

    /// <summary>Creates a gray image.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="value">Gives the value at a position.</param>
    /// <returns>The image.</returns>
    private static PdfImageData Gray(int width, int height, Func<int, int, byte> value)
    {
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = value(x, y);
            }
        }

        return new(width, height, pixels, false, false, PdfImageCodec.None) { IsGray = true };
    }
}
