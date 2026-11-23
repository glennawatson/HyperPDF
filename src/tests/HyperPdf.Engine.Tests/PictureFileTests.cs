// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Export;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that picture files the user brings in, such as a picture of a signature, are read into BGRA pixels.</summary>
public sealed class PictureFileTests
{
    /// <summary>The width of the test picture in pixels.</summary>
    private const int Width = 9;

    /// <summary>The height of the test picture in pixels.</summary>
    private const int Height = 5;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The components of an RGB sample.</summary>
    private const int RgbBytes = 3;

    /// <summary>The alpha of the partly clear pixels of the test picture.</summary>
    private const byte HalfAlpha = 128;

    /// <summary>The multiplier that scatters sample values.</summary>
    private const int Scatter = 37;

    /// <summary>How far a JPEG sample may be from the sample that was written.</summary>
    private const int JpegTolerance = 12;

    /// <summary>The sample every pixel of the JPEG test picture has.</summary>
    private const byte FlatSample = 90;

    /// <summary>A PNG with transparency comes back with the same colours and the same alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PngWithTransparencyKeepsItsPixels()
    {
        var pixels = new byte[Width * Height * BgraBytes];
        for (var i = 0; i < Width * Height; i++)
        {
            pixels[i * BgraBytes] = (byte)(i * Scatter);
            pixels[(i * BgraBytes) + 1] = (byte)(i + Scatter);
            pixels[(i * BgraBytes) + RgbBytes - 1] = (byte)(i * RgbBytes);
            pixels[(i * BgraBytes) + BgraBytes - 1] = i % RgbBytes == 0 ? HalfAlpha : byte.MaxValue;
        }

        var file = PdfRasterFiles.Encode(new(PdfRasterFormat.Bgra32, Width, Height, pixels), PdfRasterFileFormat.Png, CancellationToken.None);

        var picture = HyperPdfPictureFiles.Decode(file, CancellationToken.None);

        await Assert.That(picture).IsNotNull();
        await Assert.That(picture!.Width).IsEqualTo(Width);
        await Assert.That(picture.Height).IsEqualTo(Height);
        await Assert.That(picture.Pixels.AsSpan().SequenceEqual(pixels)).IsTrue();
    }

    /// <summary>An opaque RGB picture written as PNG, BMP or TIFF comes back as opaque BGRA with red and blue in their places.</summary>
    /// <param name="format">The file format.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRasterFileFormat.Png)]
    [Arguments(PdfRasterFileFormat.Bmp)]
    [Arguments(PdfRasterFileFormat.Tiff)]
    public async Task OpaquePicturesBecomeOpaqueBgra(PdfRasterFileFormat format)
    {
        var rgb = new byte[Width * Height * RgbBytes];
        for (var i = 0; i < rgb.Length; i++)
        {
            rgb[i] = (byte)(i * Scatter);
        }

        var file = PdfRasterFiles.Encode(new(PdfRasterFormat.Rgb24, Width, Height, rgb), format, CancellationToken.None);

        var picture = HyperPdfPictureFiles.Decode(file, CancellationToken.None);

        await Assert.That(picture).IsNotNull();
        for (var i = 0; i < Width * Height; i++)
        {
            await Assert.That(picture!.Pixels[i * BgraBytes]).IsEqualTo(rgb[(i * RgbBytes) + RgbBytes - 1]);
            await Assert.That(picture.Pixels[(i * BgraBytes) + 1]).IsEqualTo(rgb[(i * RgbBytes) + 1]);
            await Assert.That(picture.Pixels[(i * BgraBytes) + RgbBytes - 1]).IsEqualTo(rgb[i * RgbBytes]);
            await Assert.That(picture.Pixels[(i * BgraBytes) + BgraBytes - 1]).IsEqualTo(byte.MaxValue);
        }
    }

    /// <summary>A JPEG file, which the picture file decoders do not read, is read by the JPEG decoder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegIsRead()
    {
        var rgb = new byte[Width * Height * RgbBytes];
        rgb.AsSpan().Fill(FlatSample);
        var file = PdfRasterFiles.Encode(new(PdfRasterFormat.Rgb24, Width, Height, rgb), PdfRasterFileFormat.Jpeg, CancellationToken.None);

        var picture = HyperPdfPictureFiles.Decode(file, CancellationToken.None);

        await Assert.That(picture).IsNotNull();
        await Assert.That(picture!.Width).IsEqualTo(Width);
        await Assert.That(Math.Abs(picture.Pixels[0] - FlatSample)).IsLessThanOrEqualTo(JpegTolerance);
        await Assert.That(picture.Pixels[BgraBytes - 1]).IsEqualTo(byte.MaxValue);
    }

    /// <summary>A file that is missing, or is not a picture, gives no picture and no error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilesThatAreNotPicturesGiveNothing()
    {
        var notPicture = Path.Combine(Path.GetTempPath(), $"hyperpdf-not-a-picture-{Guid.NewGuid():N}.png");
        await File.WriteAllTextAsync(notPicture, "not a picture");
        try
        {
            await Assert.That(HyperPdfPictureFiles.Read(notPicture, CancellationToken.None)).IsNull();
            await Assert.That(HyperPdfPictureFiles.Read($"{notPicture}.missing", CancellationToken.None)).IsNull();
        }
        finally
        {
            File.Delete(notPicture);
        }
    }
}
