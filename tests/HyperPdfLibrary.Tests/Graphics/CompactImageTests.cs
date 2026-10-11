// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks that greyscale and bilevel images decode to one gray byte per pixel with the same pixels as BGRA, and that Skia owns the decoder's memory.</summary>
public sealed class CompactImageTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The index of the green byte in a BGRA pixel.</summary>
    private const int GreenByte = 1;

    /// <summary>The index of the red byte in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The components of an RGB pixel.</summary>
    private const int RgbComponents = 3;

    /// <summary>The index of the alpha byte in a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The bits of a bilevel sample.</summary>
    private const int OneBit = 1;

    /// <summary>The bits of a 4-bit sample.</summary>
    private const int FourBits = 4;

    /// <summary>The bits of a byte sample.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a wide sample.</summary>
    private const int WideBits = 16;

    /// <summary>A width that is a multiple of eight, so bilevel rows have no padding bits.</summary>
    private const int AlignedWidth = 304;

    /// <summary>A width with padding bits in every bilevel row.</summary>
    private const int RaggedWidth = 301;

    /// <summary>The height of the test images; large enough that the pixel array is placed on the pinned heap.</summary>
    private const int Height = 300;

    /// <summary>The multiplier that scatters the test samples.</summary>
    private const int Scatter = 37;

    /// <summary>The offset added to the scattered samples.</summary>
    private const int ScatterOffset = 11;

    /// <summary>The side of the image the sampling test draws.</summary>
    private const int Side = 64;

    /// <summary>The size of the bitmap the sampling test draws into.</summary>
    private const int Target = 24;

    /// <summary>The largest per-channel difference accepted between the BGRA and the gray drawings.</summary>
    private const int Tolerance = 1;

    /// <summary>The white sample.</summary>
    private const byte White = 0xFF;

    /// <summary>The number of bytes in a row of 16-bit samples per pixel.</summary>
    private const int WideBytes = 2;

    /// <summary>Gray images of every depth decode to gray bytes that equal the BGRA decode.</summary>
    /// <param name="bits">The bits per sample.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="inverted">Whether /Decode reverses the samples.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(OneBit, AlignedWidth, false)]
    [Arguments(OneBit, RaggedWidth, false)]
    [Arguments(OneBit, RaggedWidth, true)]
    [Arguments(FourBits, RaggedWidth, false)]
    [Arguments(ByteBits, AlignedWidth, false)]
    [Arguments(ByteBits, AlignedWidth, true)]
    [Arguments(WideBits, RaggedWidth, false)]
    public async Task GrayImagesMatchTheBgraDecode(int bits, int width, bool inverted)
    {
        var stream = GrayImage(bits, width, inverted);

        var bgra = PdfImageDecoder.Decode(stream)!;
        var gray = PdfImageDecoder.DecodeCompact(stream)!;

        await Assert.That(gray.IsGray).IsTrue();
        await Assert.That(bgra.IsGray).IsFalse();
        await Assert.That(gray.Pixels.Length).IsEqualTo(width * Height);
        var mismatches = 0;
        for (var i = 0; i < width * Height; i++)
        {
            var pixel = bgra.Pixels.AsSpan(i * BytesPerPixel, BytesPerPixel);
            if (pixel[0] != gray.Pixels[i] || pixel[GreenByte] != gray.Pixels[i] || pixel[AlphaByte] != White)
            {
                mismatches++;
            }
        }

        await Assert.That(mismatches).IsEqualTo(0);
    }

    /// <summary>Colour images stay BGRA, because a gray byte cannot hold them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorImagesStayBgra()
    {
        var dictionary = Dictionary(AlignedWidth);
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(ByteBits));
        dictionary.Add(KnownName.ColorSpace, PdfValue.FromName(KnownName.DeviceRGB));
        var stream = new PdfStream(dictionary, Samples(AlignedWidth * Height * RgbComponents));

        var image = PdfImageDecoder.DecodeCompact(stream)!;

        await Assert.That(image.IsGray).IsFalse();
        await Assert.That(image.Pixels.Length).IsEqualTo(AlignedWidth * Height * BytesPerPixel);
    }

    /// <summary>A colour-keyed gray image needs alpha, so it stays BGRA.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorKeyedImagesStayBgra()
    {
        var dictionary = Dictionary(AlignedWidth);
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(ByteBits));
        dictionary.Add(KnownName.ColorSpace, PdfValue.FromName(KnownName.DeviceGray));
        dictionary.Add(KnownName.Mask, PdfValue.FromArray(PdfArray.FromNumbers(null, [0F, 0F])));
        var stream = new PdfStream(dictionary, Samples(AlignedWidth * Height));

        var image = PdfImageDecoder.DecodeCompact(stream)!;

        await Assert.That(image.IsGray).IsFalse();
    }

    /// <summary>The public decode keeps returning BGRA for gray images.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PublicDecodeStaysBgra()
    {
        var image = PdfImageDecoder.Decode(GrayImage(ByteBits, AlignedWidth, false))!;

        await Assert.That(image.IsGray).IsFalse();
        await Assert.That(image.Pixels.Length).IsEqualTo(AlignedWidth * Height * BytesPerPixel);
    }

    /// <summary>A large gray image becomes a Skia gray image that uses the decoder's own memory.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkiaOwnsThePinnedPixels()
    {
        var gray = PdfImageDecoder.DecodeCompact(GrayImage(ByteBits, AlignedWidth, false))!;

        using var wrapper = SkiaTestRegistration.CreateServices().Backend.CreateImage(gray);
        await Assert.That(wrapper).IsNotNull();
        var image = SkiaResources.Image(wrapper!);

        await Assert.That(gray.IsPinned).IsTrue();
        await Assert.That(image.ColorType).IsEqualTo(SKColorType.Gray8);
        await Assert.That(image.Info.BytesPerPixel).IsEqualTo(1);
        using var pixmap = image.PeekPixels();
        await Assert.That(SharesPixelMemory(pixmap, gray.Pixels)).IsTrue();
        await Assert.That(pixmap.GetPixelSpan<byte>()[..AlignedWidth].SequenceEqual(gray.Pixels.AsSpan(0, AlignedWidth))).IsTrue();
    }

    /// <summary>Skia draws a gray image the same way as the equal BGRA pixels, including when it is scaled down.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayAndBgraImagesDrawAlike()
    {
        var gray = new byte[Side * Side];
        var bgra = new byte[Side * Side * BytesPerPixel];
        for (var i = 0; i < gray.Length; i++)
        {
            gray[i] = (byte)((i * Scatter) + ScatterOffset);
            bgra[i * BytesPerPixel] = gray[i];
            bgra[(i * BytesPerPixel) + GreenByte] = gray[i];
            bgra[(i * BytesPerPixel) + RedByte] = gray[i];
            bgra[(i * BytesPerPixel) + AlphaByte] = White;
        }

        using var grayImage = SKImage.FromPixelCopy(new(Side, Side, SKColorType.Gray8, SKAlphaType.Opaque), gray, Side);
        using var bgraImage = SKImage.FromPixelCopy(new(Side, Side, SKColorType.Bgra8888, SKAlphaType.Premul), bgra, Side * BytesPerPixel);

        var fromGray = Draw(grayImage);
        var fromBgra = Draw(bgraImage);

        var worst = 0;
        for (var i = 0; i < fromGray.Length; i++)
        {
            worst = Math.Max(worst, Math.Abs(fromGray[i] - fromBgra[i]));
        }

        await Assert.That(worst).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>Checks that the native pixmap uses the decoder's pixel array without copying.</summary>
    /// <param name="pixmap">The borrowed native pixels.</param>
    /// <param name="pixels">The decoder's managed pixels.</param>
    /// <returns>True when both buffers have the same address.</returns>
    private static unsafe bool SharesPixelMemory(SKPixmap pixmap, byte[] pixels)
    {
        fixed (byte* pointer = pixels)
        {
            return pixmap.GetPixels() == (nint)pointer;
        }
    }

    /// <summary>Draws an image scaled down with mipmapped linear sampling, as the renderer draws scans.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The BGRA pixels.</returns>
    private static byte[] Draw(SKImage image)
    {
        using var bitmap = new SKBitmap(Target, Target, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { IsAntialias = true };
        SKRect source = new(0, 0, Side, Side);
        SKRect destination = new(0, 0, Target, Target);
        SKSamplingOptions sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);
        canvas.DrawImage(image, source, destination, sampling, paint);
        return bitmap.GetPixelSpan().ToArray();
    }

    /// <summary>Creates a gray image stream of scattered samples.</summary>
    /// <param name="bits">The bits per sample.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="inverted">Whether /Decode reverses the samples.</param>
    /// <returns>The stream.</returns>
    private static PdfStream GrayImage(int bits, int width, bool inverted)
    {
        var dictionary = Dictionary(width);
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        dictionary.Add(KnownName.ColorSpace, PdfValue.FromName(KnownName.DeviceGray));
        if (inverted)
        {
            dictionary.Add(KnownName.Decode, PdfValue.FromArray(PdfArray.FromNumbers(null, [1F, 0F])));
        }

        var rowBytes = ((width * bits) + ByteBits - 1) / ByteBits;
        return new(dictionary, Samples(rowBytes * Height));
    }

    /// <summary>Creates an image dictionary of the test height.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Dictionary(int width)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(Height));
        return dictionary;
    }

    /// <summary>Creates scattered sample bytes.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Samples(int length)
    {
        var samples = new byte[length];
        for (var i = 0; i < length; i++)
        {
            samples[i] = (byte)((i * Scatter) + ScatterOffset + (i / WideBytes));
        }

        return samples;
    }
}
