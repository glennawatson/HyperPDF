// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests that JPEG samples go through the PDF colour space, /Decode array and colour-transform rules.</summary>
public sealed class JpegImageDecoderTests
{
    /// <summary>The bits per sample of the test headers.</summary>
    private const int SampleBits = 8;

    /// <summary>The divisor that finds the middle row.</summary>
    private const int MiddleDivisor = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The offset of red in a BGRA pixel.</summary>
    private const int Red = 2;

    /// <summary>The offset of blue in a BGRA pixel.</summary>
    private const int Blue = 0;

    /// <summary>The offset of green in a BGRA pixel.</summary>
    private const int Green = 1;

    /// <summary>The pixel tested in the middle of the cyan image.</summary>
    private const int CyanProbe = 8;

    /// <summary>The components of an RGB image.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK image.</summary>
    private const int CmykComponents = 4;

    /// <summary>The largest sample value of a dark colour.</summary>
    private const int DarkLimit = 70;

    /// <summary>The smallest blue of cyan.</summary>
    private const int CyanBlueMinimum = 150;

    /// <summary>The largest red of cyan.</summary>
    private const int CyanRedMaximum = 90;

    /// <summary>The largest difference allowed from the expected conversion.</summary>
    private const int Tolerance = 3;

    /// <summary>The luma of the hand-built RGB test pixel.</summary>
    private const byte First = 200;

    /// <summary>The first chroma of the hand-built test pixel.</summary>
    private const byte Second = 100;

    /// <summary>The second chroma of the hand-built test pixel.</summary>
    private const byte Third = 50;

    /// <summary>The red that YCbCr (200, 100, 50) converts to.</summary>
    private const int ConvertedRed = 91;

    /// <summary>The blue that YCbCr (200, 100, 50) converts to.</summary>
    private const int ConvertedBlue = 150;

    /// <summary>The centre value of chroma.</summary>
    private const byte Center = 128;

    /// <summary>The CMY value that YCbCr (200, 128, 128) converts to: 255 minus 200.</summary>
    private const byte Inverted = 55;

    /// <summary>A stored value of no ink in a file that stores CMYK inverted.</summary>
    private const byte Full = 255;

    /// <summary>The Adobe transform byte for no conversion.</summary>
    private const int AdobeNone = 0;

    /// <summary>The Adobe transform byte for YCbCr or YCCK.</summary>
    private const int AdobeConvert = 2;

    /// <summary>A YCCK file decodes to inverted CMYK, so the default /Decode gives a dark colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykUsesDefaultDecode()
    {
        var image = Decode(JpegFixtures.CmykCyan, Header(JpegFixtures.CmykCyanSide, PdfColorSpace.DeviceCmyk, null), JpegInfo.NoColorTransform);
        var pixel = Pixel(image, JpegFixtures.CmykCyanSide);

        await Assert.That(image.UnsupportedCodec).IsEqualTo(PdfImageCodec.None);
        await Assert.That((int)pixel[Red]).IsLessThan(DarkLimit);
        await Assert.That((int)pixel[Green]).IsLessThan(DarkLimit);
        await Assert.That((int)pixel[Blue]).IsLessThan(DarkLimit);
    }

    /// <summary>The PDF /Decode array does the CMYK inversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykDecodeArrayInverts()
    {
        float[] decode = [1, 0, 1, 0, 1, 0, 1, 0];
        var image = Decode(JpegFixtures.CmykCyan, Header(JpegFixtures.CmykCyanSide, PdfColorSpace.DeviceCmyk, decode), JpegInfo.NoColorTransform);
        var pixel = Pixel(image, JpegFixtures.CmykCyanSide);

        await Assert.That((int)pixel[Red]).IsLessThan(CyanRedMaximum);
        await Assert.That((int)pixel[Blue]).IsGreaterThan(CyanBlueMinimum);
    }

    /// <summary>A progressive CMYK JPEG decodes through the colour space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressiveCmykDecodes()
    {
        float[] decode = [1, 0, 1, 0, 1, 0, 1, 0];
        var header = Header(JpegFixtures.CmykProgressiveWidth, PdfColorSpace.DeviceCmyk, decode) with { Height = JpegFixtures.CmykProgressiveHeight };
        var image = Decode(JpegFixtures.CmykProgressive, header, JpegInfo.NoColorTransform);

        await Assert.That(image.Width).IsEqualTo(JpegFixtures.CmykProgressiveWidth);
        await Assert.That(image.Height).IsEqualTo(JpegFixtures.CmykProgressiveHeight);
        await Assert.That(image.Pixels.Length).IsEqualTo(JpegFixtures.CmykProgressiveWidth * JpegFixtures.CmykProgressiveHeight * BytesPerPixel);
        await Assert.That((int)image.Pixels[Blue]).IsGreaterThan(CyanBlueMinimum);
    }

    /// <summary>/ColorTransform 0 without an Adobe marker passes the three components through unconverted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorTransformZeroKeepsRawSamples()
    {
        var jpeg = JpegTestEncoder.Encode(new(1, 1, RgbComponents, JpegTestEncoder.NoAdobe, 0), [First, Second, Third]);
        var pixel = Pixel(Decode(jpeg, Header(JpegTestEncoder.BlockSide, PdfColorSpace.DeviceRgb, null), 0), JpegTestEncoder.BlockSide);

        await Assert.That((int)pixel[Red]).IsEqualTo(First);
        await Assert.That((int)pixel[Green]).IsEqualTo(Second);
        await Assert.That((int)pixel[Blue]).IsEqualTo(Third);
    }

    /// <summary>Without the entry or an Adobe marker, three components are YCbCr and convert to RGB.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task YcbcrConvertsByDefault()
    {
        var jpeg = JpegTestEncoder.Encode(new(1, 1, RgbComponents, JpegTestEncoder.NoAdobe, 0), [First, Second, Third]);
        var pixel = Pixel(Decode(jpeg, Header(JpegTestEncoder.BlockSide, PdfColorSpace.DeviceRgb, null), JpegInfo.NoColorTransform), JpegTestEncoder.BlockSide);

        await Assert.That(Math.Abs(pixel[Red] - ConvertedRed)).IsLessThanOrEqualTo(Tolerance);
        await Assert.That((int)pixel[Green]).IsEqualTo(Full);
        await Assert.That(Math.Abs(pixel[Blue] - ConvertedBlue)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>An Adobe transform of 0 wins over /ColorTransform 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AdobeZeroWinsOverColorTransform()
    {
        var jpeg = JpegTestEncoder.Encode(new(1, 1, RgbComponents, AdobeNone, 0), [First, Second, Third]);
        var pixel = Pixel(Decode(jpeg, Header(JpegTestEncoder.BlockSide, PdfColorSpace.DeviceRgb, null), 1), JpegTestEncoder.BlockSide);

        await Assert.That((int)pixel[Red]).IsEqualTo(First);
        await Assert.That((int)pixel[Blue]).IsEqualTo(Third);
    }

    /// <summary>With no Adobe marker, four components convert only when /ColorTransform is 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FourComponentsConvertWhenColorTransformIsOne()
    {
        var gray = JpegTestEncoder.Encode(new(1, 1, CmykComponents, JpegTestEncoder.NoAdobe, 0), [First, Center, Center, 0]);
        var plain = JpegTestEncoder.Encode(new(1, 1, CmykComponents, AdobeNone, 0), [Inverted, Inverted, Inverted, 0]);
        var header = Header(JpegTestEncoder.BlockSide, PdfColorSpace.DeviceCmyk, null);

        var converted = Pixel(Decode(gray, header, 1), JpegTestEncoder.BlockSide);
        var expected = Pixel(Decode(plain, header, JpegInfo.NoColorTransform), JpegTestEncoder.BlockSide);
        var untouched = Pixel(Decode(gray, header, JpegInfo.NoColorTransform), JpegTestEncoder.BlockSide);

        await Assert.That(converted).IsEquivalentTo(expected);
        await Assert.That(untouched).IsNotEquivalentTo(expected);
    }

    /// <summary>An Adobe YCCK marker converts four components.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AdobeYcckConverts()
    {
        var ycck = JpegTestEncoder.Encode(new(1, 1, CmykComponents, AdobeConvert, 0), [First, Center, Center, 0]);
        var plain = JpegTestEncoder.Encode(new(1, 1, CmykComponents, AdobeNone, 0), [Inverted, Inverted, Inverted, 0]);
        var header = Header(JpegTestEncoder.BlockSide, PdfColorSpace.DeviceCmyk, null);

        await Assert.That(Pixel(Decode(ycck, header, JpegInfo.NoColorTransform), JpegTestEncoder.BlockSide))
            .IsEquivalentTo(Pixel(Decode(plain, header, JpegInfo.NoColorTransform), JpegTestEncoder.BlockSide));
    }

    /// <summary>A JPEG no decoder can read is reported, not dropped, so the caller can fall back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreadableJpegIsReportedUnsupported()
    {
        var image = JpegImageDecoder.Decode(Header(JpegTestEncoder.BlockSide, PdfColorSpace.DeviceRgb, null), "not a jpeg"u8);

        await Assert.That(image).IsNotNull();
        await Assert.That(image!.UnsupportedCodec).IsEqualTo(PdfImageCodec.Jpeg);
        await Assert.That(image.Pixels.Length).IsEqualTo(0);
    }

    /// <summary>Creates a header for a square 8-bit image.</summary>
    /// <param name="side">The width, and height unless changed.</param>
    /// <param name="space">The colour space.</param>
    /// <param name="decode">The /Decode array, or <see langword="null"/>.</param>
    /// <returns>The header.</returns>
    private static ImageHeader Header(int side, PdfColorSpace space, float[]? decode) =>
        new(side, side, SampleBits, false, false, space, decode, null);

    /// <summary>Decodes a JPEG and requires an image.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="header">The header.</param>
    /// <param name="colorTransform">The /ColorTransform value, or -1.</param>
    /// <returns>The image.</returns>
    private static PdfImageData Decode(byte[] data, in ImageHeader header, int colorTransform) =>
        JpegImageDecoder.Decode(header, data, colorTransform)!;

    /// <summary>Reads the BGRA bytes of the first pixel of a row in the middle of an image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="side">The width.</param>
    /// <returns>The four bytes.</returns>
    private static byte[] Pixel(PdfImageData image, int side)
    {
        var row = Math.Min(side / MiddleDivisor, image.Height - 1);
        var offset = ((row * image.Width) + Math.Min(CyanProbe, image.Width - 1)) * BytesPerPixel;
        return image.Pixels.AsSpan(offset, BytesPerPixel).ToArray();
    }
}
