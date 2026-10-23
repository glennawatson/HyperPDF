// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Tests for image decoding.</summary>
public sealed class ImageDecoderTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of the red byte in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The index of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The largest byte.</summary>
    private const byte Full = 0xFF;

    /// <summary>Half of a byte, after premultiplying by a half alpha.</summary>
    private const byte HalfByte = 0x80;

    /// <summary>The width of the small test images.</summary>
    private const int SmallWidth = 3;

    /// <summary>The height of the small test images.</summary>
    private const int SmallHeight = 2;

    /// <summary>The size of the square test images.</summary>
    private const int Square = 2;

    /// <summary>The size of the JPEG test image.</summary>
    private const int JpegSize = 16;

    /// <summary>The JPEG quality.</summary>
    private const int JpegQuality = 95;

    /// <summary>The largest difference allowed after lossy JPEG compression.</summary>
    private const int JpegTolerance = 8;

    /// <summary>The red of the JPEG test colour.</summary>
    private const byte JpegRed = 200;

    /// <summary>The green of the JPEG test colour.</summary>
    private const byte JpegGreen = 100;

    /// <summary>The blue of the JPEG test colour.</summary>
    private const byte JpegBlue = 50;

    /// <summary>The offset of the third pixel in a BGRA row.</summary>
    private const int ThirdPixel = 8;

    /// <summary>The bits of a 16-bit sample.</summary>
    private const int WideBits = 16;

    /// <summary>The bits of an 8-bit sample.</summary>
    private const int ByteBits = 8;

    /// <summary>A width past the decode limit.</summary>
    private const int HugeSide = 70_000;

    /// <summary>The width of the fax test image.</summary>
    private const int FaxWidth = 8;

    /// <summary>The height of the fax test image.</summary>
    private const int FaxHeight = 3;

    /// <summary>The first black column of the fax pattern row.</summary>
    private const int FaxBlackStart = 2;

    /// <summary>The K value for Group 4.</summary>
    private const int Group4 = -1;

    /// <summary>The gray tolerance after a 16 to 8 bit reduction.</summary>
    private const int WideTolerance = 1;

    /// <summary>Group 4 fax rows: white, two white then three black then three white, then end of block (see the fax tests).</summary>
    private static readonly byte[] Group4Pattern = [0x97, 0xA0, 0x02, 0x00, 0x20];

    /// <summary>The first pixel of the RGB image: red as BGRA.</summary>
    private static readonly byte[] RedPixel = [0x00, 0x00, Full, Full];

    /// <summary>The fourth pixel of the RGB image as BGRA.</summary>
    private static readonly byte[] FourthPixel = [0x30, 0x20, 0x10, Full];

    /// <summary>The coverage of the inverted image mask.</summary>
    private static readonly byte[] MaskCoverage = [Full, 0, Full, 0, Full, 0];

    /// <summary>The pixels of the inline image.</summary>
    private static readonly byte[] InlinePixels = [0, 0, 0, Full, Full, Full, Full, Full];

    /// <summary>An 8-bit RGB image decodes to opaque BGRA.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesEightBitRgb()
    {
        byte[] samples = [0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80, 0x90];
        var image = PdfImageDecoder.Decode(Image(SmallWidth, SmallHeight, ByteBits, Name(KnownName.DeviceRGB), samples))!;

        await Assert.That(image.Width).IsEqualTo(SmallWidth);
        await Assert.That(image.Height).IsEqualTo(SmallHeight);
        await Assert.That(image.Pixels.Length).IsEqualTo(SmallWidth * SmallHeight * BytesPerPixel);
        await Assert.That(image.Pixels[..BytesPerPixel]).IsEquivalentTo(RedPixel);
        await Assert.That(image.Pixels[(BytesPerPixel * SmallWidth)..(BytesPerPixel * (SmallWidth + 1))]).IsEquivalentTo(FourthPixel);
    }

    /// <summary>A 1-bit image mask with an inverted /Decode paints where the bits are 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesInvertedImageMask()
    {
        var dictionary = Dictionary(SmallWidth, SmallHeight);
        dictionary.Add(KnownName.ImageMask, PdfValue.FromBoolean(true));
        dictionary.Add(KnownName.Decode, Numbers(1, 0));
        var image = PdfImageDecoder.Decode(new(dictionary, [0b1010_0000, 0b0100_0000]))!;

        await Assert.That(image.IsStencilMask).IsTrue();
        await Assert.That(image.Pixels).IsEquivalentTo(MaskCoverage);
    }

    /// <summary>A 16-bit gray image keeps the high byte of each sample.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesSixteenBitGray()
    {
        byte[] samples = [0x00, 0x00, 0x80, 0x00, 0xFF, 0xFF, 0x12, 0x34];
        var image = PdfImageDecoder.Decode(Image(Square, Square, WideBits, Name(KnownName.DeviceGray), samples))!;

        await Assert.That(image.Pixels[0]).IsEqualTo((byte)0);
        await Assert.That(Math.Abs(image.Pixels[BytesPerPixel] - HalfByte)).IsLessThanOrEqualTo(WideTolerance);
        await Assert.That(image.Pixels[ThirdPixel]).IsEqualTo(Full);
        await Assert.That(Math.Abs(image.Pixels[BytesPerPixel * AlphaByte] - 0x12)).IsLessThanOrEqualTo(WideTolerance);
    }

    /// <summary>Colour-key masking makes matching pixels transparent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorKeyMasksMatchingPixels()
    {
        var stream = Image(Square, 1, ByteBits, Name(KnownName.DeviceRGB), [0x00, 0x00, 0x00, 0xFF, 0x00, 0x00]);
        stream.Dictionary.Add(KnownName.Mask, Numbers(0, 0, 0, 0, 0, 0));
        var image = PdfImageDecoder.Decode(stream)!;

        await Assert.That(image.Pixels[..BytesPerPixel]).IsEquivalentTo(new byte[4]);
        await Assert.That(image.Pixels[BytesPerPixel + RedByte]).IsEqualTo(Full);
        await Assert.That(image.Pixels[BytesPerPixel + AlphaByte]).IsEqualTo(Full);
    }

    /// <summary>A soft mask of a different size is resampled and premultiplied into the image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftMaskIsResampledAndPremultiplied()
    {
        var mask = Image(1, 1, ByteBits, Name(KnownName.DeviceGray), [HalfByte]);
        var stream = Image(Square, Square, ByteBits, Name(KnownName.DeviceGray), [Full, Full, Full, Full]);
        stream.Dictionary.Add(KnownName.SMask, PdfValue.FromStream(mask));
        var image = PdfImageDecoder.Decode(stream)!;

        for (var p = 0; p < Square * Square; p++)
        {
            await Assert.That(image.Pixels[(p * BytesPerPixel) + AlphaByte]).IsEqualTo(HalfByte);
            await Assert.That(image.Pixels[p * BytesPerPixel]).IsEqualTo(HalfByte);
        }
    }

    /// <summary>A stencil /Mask stream hides the pixels its 1 bits cover.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilMaskStreamHidesPixels()
    {
        var maskDictionary = Dictionary(1, 1);
        maskDictionary.Add(KnownName.ImageMask, PdfValue.FromBoolean(true));
        var stream = Image(Square, 1, ByteBits, Name(KnownName.DeviceGray), [Full, Full]);
        stream.Dictionary.Add(KnownName.Mask, PdfValue.FromStream(new(maskDictionary, [0x80])));
        var image = PdfImageDecoder.Decode(stream)!;

        await Assert.That(image.Pixels).IsEquivalentTo(new byte[Square * BytesPerPixel]);
    }

    /// <summary>A JPEG made with SkiaSharp decodes back to its colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesJpeg()
    {
        var stream = Image(JpegSize, JpegSize, ByteBits, Name(KnownName.DeviceRGB), EncodeJpeg());
        stream.Dictionary.Add(KnownName.Filter, Name(KnownName.DCTDecode));
        var image = PdfImageDecoder.Decode(stream)!;
        var pixel = image.Pixels.AsSpan(0, BytesPerPixel).ToArray();

        await Assert.That(image.Width).IsEqualTo(JpegSize);
        await Assert.That(Math.Abs(pixel[RedByte] - JpegRed)).IsLessThanOrEqualTo(JpegTolerance);
        await Assert.That(Math.Abs(pixel[1] - JpegGreen)).IsLessThanOrEqualTo(JpegTolerance);
        await Assert.That(Math.Abs(pixel[0] - JpegBlue)).IsLessThanOrEqualTo(JpegTolerance);
        await Assert.That(pixel[AlphaByte]).IsEqualTo(Full);
    }

    /// <summary>A JPEG with an inverted /Decode array is inverted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegAppliesDecodeArray()
    {
        var stream = Image(JpegSize, JpegSize, ByteBits, Name(KnownName.DeviceRGB), EncodeJpeg());
        stream.Dictionary.Add(KnownName.Filter, Name(KnownName.DCTDecode));
        stream.Dictionary.Add(KnownName.Decode, Numbers(1, 0, 1, 0, 1, 0));
        var image = PdfImageDecoder.Decode(stream)!;

        await Assert.That(Math.Abs(image.Pixels[RedByte] - (Full - JpegRed))).IsLessThanOrEqualTo(JpegTolerance);
    }

    /// <summary>A CCITT Group 4 image decodes with black as 0 bits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesCcittImage()
    {
        var parameters = new PdfDictionary(null);
        parameters.Add(KnownName.K, PdfValue.FromInteger(Group4));
        parameters.Add(KnownName.Columns, PdfValue.FromInteger(FaxWidth));
        var stream = Image(FaxWidth, FaxHeight, 1, Name(KnownName.DeviceGray), Group4Pattern);
        stream.Dictionary.Add(KnownName.Filter, Name(KnownName.CCITTFaxDecode));
        stream.Dictionary.Add(KnownName.DecodeParms, PdfValue.FromDictionary(parameters));
        var image = PdfImageDecoder.Decode(stream)!;
        const int secondRow = FaxWidth * BytesPerPixel;

        await Assert.That(image.Pixels[0]).IsEqualTo(Full);
        await Assert.That(image.Pixels[secondRow + (FaxBlackStart * BytesPerPixel)]).IsEqualTo((byte)0);
        await Assert.That(image.Pixels[secondRow]).IsEqualTo(Full);
    }

    /// <summary>An inline image with abbreviated keys and values decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesInlineImage()
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.W, PdfValue.FromInteger(Square));
        dictionary.Add(KnownName.H, PdfValue.FromInteger(1));
        dictionary.Add(KnownName.BPC, PdfValue.FromInteger(ByteBits));
        dictionary.Add(KnownName.CS, Name(KnownName.G));
        dictionary.Add(KnownName.F, Name(KnownName.AHx));
        var image = PdfImageDecoder.DecodeInline(dictionary, "00FF>"u8, null)!;

        await Assert.That(image.Pixels).IsEquivalentTo(InlinePixels);
    }

    /// <summary>Oversized images are refused, and a JBIG2 stream too short to hold a page decodes to white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesHugeAndToleratesShortJbig2()
    {
        var huge = PdfImageDecoder.Decode(Image(HugeSide, HugeSide, ByteBits, Name(KnownName.DeviceGray), []));
        var jbig2 = Image(Square, Square, ByteBits, Name(KnownName.DeviceRGB), [0x00]);
        jbig2.Dictionary.Add(KnownName.Filter, Name(KnownName.JBIG2Decode));
        var white = PdfImageDecoder.Decode(jbig2);

        await Assert.That(huge).IsNull();
        await Assert.That(white is not null && !white.Pixels.AsSpan().ContainsAnyExcept(Full)).IsTrue();
    }

    /// <summary>Short data reads as zero samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortDataIsPadded()
    {
        var image = PdfImageDecoder.Decode(Image(Square, Square, ByteBits, Name(KnownName.DeviceGray), [Full]))!;

        await Assert.That(image.Pixels[0]).IsEqualTo(Full);
        await Assert.That(image.Pixels[BytesPerPixel]).IsEqualTo((byte)0);
    }

    /// <summary>Creates a name value.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value.</returns>
    private static PdfValue Name(KnownName name) => PdfValue.FromName(name);

    /// <summary>Creates a number array value.</summary>
    /// <param name="values">The numbers.</param>
    /// <returns>The value.</returns>
    private static PdfValue Numbers(params float[] values) => PdfValue.FromArray(PdfArray.FromNumbers(null, values));

    /// <summary>Creates an image dictionary with a size.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Dictionary(int width, int height)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(height));
        return dictionary;
    }

    /// <summary>Creates an image XObject stream.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="bits">The bits per component.</param>
    /// <param name="colorSpace">The colour space.</param>
    /// <param name="data">The data.</param>
    /// <returns>The stream.</returns>
    private static PdfStream Image(int width, int height, int bits, PdfValue colorSpace, byte[] data)
    {
        var dictionary = Dictionary(width, height);
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        dictionary.Add(KnownName.ColorSpace, colorSpace);
        return new(dictionary, data);
    }

    /// <summary>Encodes a solid colour JPEG with SkiaSharp.</summary>
    /// <returns>The JPEG bytes.</returns>
    private static byte[] EncodeJpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(JpegSize, JpegSize, SKColorType.Bgra8888, SKAlphaType.Opaque));
        bitmap.Erase(new(JpegRed, JpegGreen, JpegBlue));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }
}
