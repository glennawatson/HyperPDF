// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Export;
using SkiaSharp;
using TUnit.Assertions.Enums;

namespace HyperPdfLibrary.Tests.Export;

/// <summary>Checks the picture files written from raster images.</summary>
public sealed class PdfRasterFilesTests
{
    /// <summary>The width of the test image in pixels.</summary>
    private const int Width = 3;

    /// <summary>The height of the test image in pixels.</summary>
    private const int Height = 2;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int Bgra = 4;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int Rgb = 3;

    /// <summary>The red of the first pixel.</summary>
    private const byte Red = 200;

    /// <summary>The green of the first pixel.</summary>
    private const byte Green = 100;

    /// <summary>The blue of the first pixel.</summary>
    private const byte Blue = 50;

    /// <summary>The alpha of the first pixel.</summary>
    private const byte Alpha = 128;

    /// <summary>The premultiplied red of a half transparent pixel.</summary>
    private const byte HalfRed = 100;

    /// <summary>The offset of the second sample in a pixel.</summary>
    private const int GreenAt = 1;

    /// <summary>The offset of the third sample in a pixel: blue in BGRA order, and where red sits in a BGRA pixel.</summary>
    private const int BlueAt = 2;

    /// <summary>The offset of the alpha sample in a pixel, and the number of planes before the alpha plane.</summary>
    private const int AlphaAt = 3;

    /// <summary>The divisor that halves a number.</summary>
    private const int Halve = 2;

    /// <summary>The offset of the width in a TGA header.</summary>
    private const int TgaWidthOffset = 12;

    /// <summary>The bytes of a TGA header.</summary>
    private const int TgaHeaderBytes = 18;

    /// <summary>The bytes of the colour mode, resources and layer sections and the compression field in a PSD file.</summary>
    private const int PsdTail = 14;

    /// <summary>The bytes of a PSD file header.</summary>
    private const int PsdHeaderBytes = 26;

    /// <summary>The TIFF tag for the width.</summary>
    private const int TagWidth = 256;

    /// <summary>The TIFF tag for the samples per pixel.</summary>
    private const int TagSamples = 277;

    /// <summary>The TIFF tag for the strip offset.</summary>
    private const int TagStripOffset = 273;

    /// <summary>The offset of the first directory offset in a TIFF file.</summary>
    private const int TiffDirectoryField = 4;

    /// <summary>The bytes of one TIFF directory entry.</summary>
    private const int TiffEntryBytes = 12;

    /// <summary>The offset of a value in a TIFF directory entry.</summary>
    private const int TiffValueOffset = 8;

    /// <summary>The most a JPEG sample may differ from the original.</summary>
    private const int JpegTolerance = 12;

    /// <summary>The white a transparent pixel is placed over.</summary>
    private const byte White = 255;

    /// <summary>Gets the test image: three pixels across and two down, with the first pixel half transparent.</summary>
    private static PdfRasterImage Image => new(PdfRasterFormat.Bgra32, Width, Height, CreatePixels());

    /// <summary>A PNG file decodes to the same pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngKeepsEveryPixel()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Png, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(file);
        await Assert.That(bitmap.Width).IsEqualTo(Width);
        await Assert.That(bitmap.Height).IsEqualTo(Height);
        var unpremultiplied = bitmap.GetPixel(0, 0);
        await Assert.That(Math.Abs(unpremultiplied.Red - Red)).IsLessThanOrEqualTo(1);
        await Assert.That(unpremultiplied.Alpha).IsEqualTo(Alpha);
    }

    /// <summary>A BMP file with alpha decodes to the same size and keeps the colour of an opaque pixel.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BmpKeepsTheSizeAndColour()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Bmp, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(file);
        await Assert.That(bitmap).IsNotNull();
        await Assert.That(bitmap.Width).IsEqualTo(Width);
        await Assert.That(bitmap.Height).IsEqualTo(Height);
        var opaque = bitmap.GetPixel(1, 0);
        await Assert.That(opaque.Green).IsEqualTo(Green);
        await Assert.That(opaque.Blue).IsEqualTo(Blue);
    }

    /// <summary>A grey image becomes an 8-bit BMP whose first pixel is the grey value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GreyBmpHasAPalette()
    {
        var grey = new PdfRasterImage(PdfRasterFormat.Gray8, Width, Height, new byte[Width * Height]);

        var file = PdfRasterFiles.Encode(grey, PdfRasterFileFormat.Bmp, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(file);
        await Assert.That(bitmap).IsNotNull();
        await Assert.That(bitmap.Width).IsEqualTo(Width);
    }

    /// <summary>A JPEG decodes close to the original, with the transparent pixel placed over white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegDecodesCloseToTheOriginal()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Jpeg, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(file);
        await Assert.That(bitmap.Width).IsEqualTo(Width);
        var opaque = bitmap.GetPixel(1, 1);
        await Assert.That(Math.Abs(opaque.Red - Red)).IsLessThan(JpegTolerance);
    }

    /// <summary>A PNM file has the colour header, then the opaque pixels, with the half transparent pixel placed over white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PnmPlacesTransparencyOverWhite()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Pnm, CancellationToken.None);

        var header = "P6\n3 2\n255\n"u8.ToArray();
        await Assert.That(file[..header.Length]).IsEquivalentTo(header, CollectionOrdering.Matching);
        await Assert.That(file.Length).IsEqualTo(header.Length + (Width * Height * Rgb));
        const byte expectedRed = ((Red * Alpha) + (White * (White - Alpha)) + (White / Halve)) / White;
        await Assert.That(file[header.Length]).IsEqualTo(expectedRed);
    }

    /// <summary>A PAM file keeps the alpha channel.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PamKeepsAlpha()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Pam, CancellationToken.None);

        var text = Encoding.ASCII.GetString(file);
        await Assert.That(text).StartsWith("P7\nWIDTH 3\nHEIGHT 2\nDEPTH 4\nMAXVAL 255\nTUPLTYPE RGB_ALPHA\nENDHDR\n");
        var first = file.Length - (Width * Height * Bgra);
        await Assert.That(file[first]).IsEqualTo(Red);
        await Assert.That(file[first + GreenAt]).IsEqualTo(Green);
        await Assert.That(file[first + AlphaAt]).IsEqualTo(Alpha);
    }

    /// <summary>A TGA file has a 32-bit header and writes the first pixel as blue, green, red, alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TgaWritesBgraTopRowFirst()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Tga, CancellationToken.None);

        await Assert.That(file.Length).IsEqualTo(TgaHeaderBytes + (Width * Height * Bgra));
        await Assert.That(BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(TgaWidthOffset))).IsEqualTo((ushort)Width);
        await Assert.That(file[TgaHeaderBytes]).IsEqualTo(Blue);
        await Assert.That(file[TgaHeaderBytes + BlueAt]).IsEqualTo(Red);
        await Assert.That(file[TgaHeaderBytes + AlphaAt]).IsEqualTo(Alpha);
    }

    /// <summary>A PSD file stores each channel as its own plane.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PsdStoresPlanes()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Psd, CancellationToken.None);

        await Assert.That(Encoding.ASCII.GetString(file[..Bgra])).IsEqualTo("8BPS");
        await Assert.That(file.Length).IsEqualTo(PsdHeaderBytes + PsdTail + (Width * Height * Bgra));
        const int planes = PsdHeaderBytes + PsdTail;
        await Assert.That(file[planes]).IsEqualTo(Red);
        await Assert.That(file[planes + (Width * Height)]).IsEqualTo(Green);
        await Assert.That(file[planes + (Width * Height * BlueAt)]).IsEqualTo(Blue);
        await Assert.That(file[planes + (Width * Height * AlphaAt)]).IsEqualTo(Alpha);
    }

    /// <summary>A TIFF file has the size, four samples a pixel, and the pixels where its strip offset says.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TiffPointsAtItsPixels()
    {
        var file = PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Tiff, CancellationToken.None);

        await Assert.That(ReadTag(file, TagWidth)).IsEqualTo(Width);
        await Assert.That(ReadTag(file, TagSamples)).IsEqualTo(Bgra);
        var strip = ReadTag(file, TagStripOffset);
        await Assert.That(file.Length).IsEqualTo(strip + (Width * Height * Bgra));
        await Assert.That(file[strip]).IsEqualTo(Red);
        await Assert.That(file[strip + AlphaAt]).IsEqualTo(Alpha);
    }

    /// <summary>Premultiplied renderer pixels are divided by their alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PremultipliedPixelsBecomeStraight()
    {
        byte[] premultiplied = [Blue / Halve, Green / Halve, HalfRed, Alpha];

        var image = PdfRasterFiles.FromPremultipliedBgra(premultiplied, 1, 1);

        var pixels = image.Pixels.ToArray();
        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Bgra32);
        await Assert.That(Math.Abs(pixels[BlueAt] - Red)).IsLessThanOrEqualTo(1);
        await Assert.That(pixels[AlphaAt]).IsEqualTo(Alpha);
    }

    /// <summary>The asynchronous form writes the same bytes as the synchronous one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncWritesTheSameBytes()
    {
        await using var stream = new MemoryStream();

        await PdfRasterFiles.EncodeAsync(Image, PdfRasterFileFormat.Tga, stream, CancellationToken.None);

        await Assert.That(stream.ToArray()).IsEquivalentTo(PdfRasterFiles.Encode(Image, PdfRasterFileFormat.Tga, CancellationToken.None), CollectionOrdering.Matching);
    }

    /// <summary>Every format has a file name extension.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryFormatHasAnExtension()
    {
        foreach (var format in Enum.GetValues<PdfRasterFileFormat>())
        {
            await Assert.That(PdfRasterFiles.GetExtension(format)).IsNotEmpty();
        }
    }

    /// <summary>Makes the pixels: a half transparent first pixel (straight red 200, green 100, blue 50) and opaque ones after it.</summary>
    /// <returns>The BGRA bytes.</returns>
    private static byte[] CreatePixels()
    {
        var pixels = new byte[Width * Height * Bgra];
        for (var i = 0; i < pixels.Length; i += Bgra)
        {
            pixels[i] = Blue;
            pixels[i + GreenAt] = Green;
            pixels[i + BlueAt] = Red;
            pixels[i + AlphaAt] = byte.MaxValue;
        }

        pixels[AlphaAt] = Alpha;
        return pixels;
    }

    /// <summary>Reads a short or long tag from the first directory of a little-endian TIFF.</summary>
    /// <param name="tiff">The file.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The value, or -1.</returns>
    private static int ReadTag(byte[] tiff, int tag)
    {
        var directory = BitConverter.ToInt32(tiff, TiffDirectoryField);
        var count = BitConverter.ToUInt16(tiff, directory);
        for (var i = 0; i < count; i++)
        {
            var entry = directory + sizeof(ushort) + (i * TiffEntryBytes);
            if (BitConverter.ToUInt16(tiff, entry) == tag)
            {
                return BitConverter.ToInt32(tiff, entry + TiffValueOffset);
            }
        }

        return -1;
    }
}
