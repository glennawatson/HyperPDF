// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.ImageFiles;

namespace HyperPdfLibrary.Tests.ImageFiles;

/// <summary>Checks reading PNG, GIF, BMP and TIFF files, using small pictures made by another program.</summary>
public sealed class ImageFileTests
{
    /// <summary>The width of the standard test picture.</summary>
    private const int Width = 4;

    /// <summary>The height of the standard test picture.</summary>
    private const int Height = 2;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The width of the one-bit test picture.</summary>
    private const int BilevelWidth = 9;

    /// <summary>The last column of the one-bit test picture.</summary>
    private const int LastColumn = BilevelWidth - 1;

    /// <summary>The alpha of a half transparent pixel, and the most it may be off.</summary>
    private const int HalfAlpha = 128;

    /// <summary>The most an alpha channel may differ from what the program wrote.</summary>
    private const int AlphaTolerance = 2;

    /// <summary>The offset of the alpha channel in a BGRA pixel.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The flag of the leftmost pixel in a packed byte.</summary>
    private const int TopBit = 0x80;

    /// <summary>The bytes of a PNG file kept to cut it short.</summary>
    private const int TruncatedLength = 40;

    /// <summary>The prefix of the test pictures' resource names.</summary>
    private const string ResourcePrefix = "HyperPdfLibrary.Tests.ImageFiles.";

    /// <summary>Gets bytes that are not a picture.</summary>
    private static byte[] NotAPicture { get; } = "hello"u8.ToArray();

    /// <summary>Gets the colours of the standard test picture: red, lime, blue, white; black, grey, yellow, cyan.</summary>
    private static ReadOnlySpan<byte> Expected =>
    [
        0xFF, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF,
        0x00, 0x00, 0x00, 0x80, 0x80, 0x80, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF,
    ];

    /// <summary>Each file decodes to the pixels the other program wrote.</summary>
    /// <param name="name">The picture's file name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("rgb.png")]
    [Arguments("pal.png")]
    [Arguments("inter.png")]
    [Arguments("wide.png")]
    [Arguments("img.gif")]
    [Arguments("rgb24.bmp")]
    [Arguments("rle8.bmp")]
    [Arguments("tiff_None.tif")]
    [Arguments("tiff_LZW.tif")]
    [Arguments("tiff_Zip.tif")]
    [Arguments("tiff_RLE.tif")]
    [Arguments("tiff_pred.tif")]
    [Arguments("pal.tif")]
    [Arguments("wide.tif")]
    public async Task DecodesToTheSamePixels(string name)
    {
        var image = PdfImageFiles.Decode(Load(name), CancellationToken.None);

        await Assert.That(image.Width).IsEqualTo(Width);
        await Assert.That(image.Height).IsEqualTo(Height);
        await Assert.That(ToRgb(image).AsSpan().SequenceEqual(Expected)).IsTrue();
    }

    /// <summary>A grey TIFF decodes to eight-bit grey with black and white at the ends of the range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GreyTiffIsEightBitGrey()
    {
        var image = PdfImageFiles.Decode(Load("gray.tif"), CancellationToken.None);

        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Gray8);
        await Assert.That(image.Pixels.Span[Width]).IsEqualTo((byte)0);
        await Assert.That(image.Pixels.Span[Width - 1]).IsEqualTo(byte.MaxValue);
    }

    /// <summary>A PNG with alpha keeps it straight.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngKeepsItsAlpha()
    {
        var image = PdfImageFiles.Decode(Load("alpha.png"), CancellationToken.None);

        var pixels = image.Pixels.ToArray();
        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Bgra32);
        await Assert.That(Math.Abs(pixels[AlphaIndex] - HalfAlpha)).IsLessThanOrEqualTo(AlphaTolerance);
        await Assert.That(pixels[RedIndex]).IsEqualTo(byte.MaxValue);
        await Assert.That(pixels[BgraBytes + AlphaIndex]).IsEqualTo((byte)0);
    }

    /// <summary>A palette PNG with a transparency chunk makes the transparent entry transparent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PalettePngKeepsItsTransparentEntry()
    {
        var image = PdfImageFiles.Decode(Load("alphapal.png"), CancellationToken.None);

        var pixels = image.Pixels.ToArray();
        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Bgra32);
        await Assert.That(pixels[RedIndex]).IsEqualTo(byte.MaxValue);
        await Assert.That(pixels[AlphaIndex]).IsGreaterThan((byte)0);
        await Assert.That(pixels[BgraBytes + AlphaIndex]).IsEqualTo((byte)0);
    }

    /// <summary>A GIF with a transparent colour has transparent pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GifKeepsItsTransparentColour()
    {
        var image = PdfImageFiles.Decode(Load("trans.gif"), CancellationToken.None);

        var pixels = image.Pixels.ToArray();
        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Bgra32);
        await Assert.That(pixels[AlphaIndex]).IsEqualTo(byte.MaxValue);
        await Assert.That(pixels[BgraBytes + AlphaIndex]).IsEqualTo((byte)0);
    }

    /// <summary>One-bit pictures stay one-bit, with black at the two corners the program drew it.</summary>
    /// <param name="name">The picture's file name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("bilevel.png")]
    [Arguments("bilevel.bmp")]
    [Arguments("g4big.tif")]
    public async Task OneBitPicturesStayOneBit(string name)
    {
        var image = PdfImageFiles.Decode(Load(name), CancellationToken.None);

        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Bilevel1);
        await Assert.That(image.Width).IsEqualTo(BilevelWidth);
        await Assert.That(IsWhite(image, 0, 0)).IsFalse();
        await Assert.That(IsWhite(image, 1, 0)).IsTrue();
        await Assert.That(IsWhite(image, LastColumn, 0)).IsTrue();
        await Assert.That(IsWhite(image, 0, 1)).IsTrue();
        await Assert.That(IsWhite(image, LastColumn, 1)).IsFalse();
    }

    /// <summary>A fax coded TIFF of the standard picture reads as a one-bit picture of the right size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FaxTiffIsOneBit()
    {
        var image = PdfImageFiles.Decode(Load("g4.tif"), CancellationToken.None);

        await Assert.That(image.Format).IsEqualTo(PdfRasterFormat.Bilevel1);
        await Assert.That(image.Width).IsEqualTo(Width);
        await Assert.That(image.Height).IsEqualTo(Height);
    }

    /// <summary>The format is found from the first bytes, not the name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormatIsDetectedFromTheBytes()
    {
        await Assert.That(PdfImageFiles.Detect(Load("rgb.png"))).IsEqualTo(PdfImageFileFormat.Png);
        await Assert.That(PdfImageFiles.Detect(Load("img.gif"))).IsEqualTo(PdfImageFileFormat.Gif);
        await Assert.That(PdfImageFiles.Detect(Load("rgb24.bmp"))).IsEqualTo(PdfImageFileFormat.Bmp);
        await Assert.That(PdfImageFiles.Detect(Load("tiff_None.tif"))).IsEqualTo(PdfImageFileFormat.Tiff);
        await Assert.That(PdfImageFiles.Detect("hello"u8)).IsEqualTo(PdfImageFileFormat.Unknown);
    }

    /// <summary>Bytes that are no picture, or a damaged one, fail with clear errors.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadInputIsRefused()
    {
        await Assert.That(static () => DecodeBytes(NotAPicture)).Throws<NotSupportedException>();
        await Assert.That(static () => DecodeBytes(Load("rgb.png")[..TruncatedLength])).Throws<InvalidDataException>();
    }

    /// <summary>Decodes some bytes as a picture.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The picture.</returns>
    private static PdfRasterImage DecodeBytes(byte[] bytes) => PdfImageFiles.Decode(bytes, CancellationToken.None);

    /// <summary>Opens a picture file as a resource.</summary>
    /// <param name="name">The file's name.</param>
    /// <returns>The file's bytes.</returns>
    /// <exception cref="InvalidOperationException">The picture is not in the test assembly.</exception>
    private static byte[] Load(string name)
    {
        using var stream = typeof(ImageFileTests).Assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"The test picture {name} is missing.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    /// <summary>Converts a decoded picture to red, green and blue bytes.</summary>
    /// <param name="image">The picture.</param>
    /// <returns>Three bytes a pixel.</returns>
    private static byte[] ToRgb(PdfRasterImage image)
    {
        var pixels = image.Pixels.Span;
        var count = image.Width * image.Height;
        var rgb = new byte[count * RgbBytes];
        for (var i = 0; i < count; i++)
        {
            for (var channel = 0; channel < RgbBytes; channel++)
            {
                rgb[(i * RgbBytes) + channel] = image.Format switch
                {
                    PdfRasterFormat.Rgb24 => pixels[(i * RgbBytes) + channel],
                    PdfRasterFormat.Gray8 => pixels[i],
                    _ => pixels[(i * BgraBytes) + RedIndex - channel],
                };
            }
        }

        return rgb;
    }

    /// <summary>Determines whether a pixel of a one-bit picture is white.</summary>
    /// <param name="image">The picture.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns><see langword="true"/> when the bit is set.</returns>
    private static bool IsWhite(PdfRasterImage image, int x, int y) =>
        (image.Pixels.Span[(y * image.RowBytes) + (x / ByteBits)] & (TopBit >> (x % ByteBits))) != 0;
}
