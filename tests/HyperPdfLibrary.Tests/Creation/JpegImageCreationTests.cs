// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests that images created from pixels are stored as JPEGs the PDF colour pipeline reads back.</summary>
public sealed class JpegImageCreationTests
{
    /// <summary>The width of the test pictures.</summary>
    private const int Width = 48;

    /// <summary>The height of the test pictures.</summary>
    private const int Height = 32;

    /// <summary>The bits per sample of the image header.</summary>
    private const int SampleBits = 8;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int Red = 2;

    /// <summary>The offset of the green byte in a BGRA pixel.</summary>
    private const int Green = 1;

    /// <summary>The offset of the blue byte in a BGRA pixel.</summary>
    private const int Blue = 0;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The quality of the CMYK picture.</summary>
    private const int HighQuality = 95;

    /// <summary>A quarter of a side, which lands inside the first colour patch.</summary>
    private const int Quarter = 4;

    /// <summary>The least that red must exceed green and blue by in the red patch.</summary>
    private const int RedLead = 60;

    /// <summary>The alpha of the transparent test pixels.</summary>
    private const byte HalfAlpha = 128;

    /// <summary>The components of the CMYK picture.</summary>
    private const int InkComponents = 4;

    /// <summary>Gray and RGB rasters are stored as DCTDecode images of the same size.</summary>
    /// <param name="format">The raster format.</param>
    /// <param name="space">The expected colour space.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRasterFormat.Gray8, KnownName.DeviceGray)]
    [Arguments(PdfRasterFormat.Rgb24, KnownName.DeviceRGB)]
    public async Task RastersAreStoredAsJpeg(PdfRasterFormat format, KnownName space)
    {
        var pixels = JpegEncodeSamples.Pixels(format == PdfRasterFormat.Gray8 ? PdfJpegPixelFormat.Gray8 : PdfJpegPixelFormat.Rgb24, Width, Height);
        var sink = new ListSink();

        var created = PdfImageWriter.AddRasterAsJpeg(sink, new(format, Width, Height, pixels), PdfJpegOptions.Default, PdfImageOptions.Default, CancellationToken.None);

        var stream = sink.GetStream(created.Id);
        await Assert.That(stream.Dictionary.IsName(KnownName.Filter, KnownName.DCTDecode)).IsTrue();
        await Assert.That(stream.Dictionary.IsName(KnownName.ColorSpace, space)).IsTrue();
        await Assert.That(created.Width).IsEqualTo(Width);
        await Assert.That(created.Height).IsEqualTo(Height);
        await Assert.That(JpegMarkers.TryReadInfo(stream.CopyRawData(), out var info)).IsTrue();
        await Assert.That(info.Width).IsEqualTo(Width);
    }

    /// <summary>A BGRA raster with transparency gets a soft mask; an opaque one does not.</summary>
    /// <param name="transparent">Whether some pixels are not opaque.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task BgraRastersGetASoftMaskOnlyWhenTransparent(bool transparent)
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Bgra32, Width, Height);
        if (transparent)
        {
            pixels[BgraBytes - 1] = HalfAlpha;
        }

        var sink = new ListSink();

        var created = PdfImageWriter.AddRasterAsJpeg(sink, new(PdfRasterFormat.Bgra32, Width, Height, pixels), PdfJpegOptions.Default, PdfImageOptions.Default, CancellationToken.None);

        var stream = sink.GetStream(created.Id);
        await Assert.That(stream.Dictionary.IsName(KnownName.Filter, KnownName.DCTDecode)).IsTrue();
        await Assert.That(stream.Dictionary.ContainsKey(KnownName.SMask)).IsEqualTo(transparent);
    }

    /// <summary>A bilevel raster cannot be a JPEG.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BilevelRastersAreRefused()
    {
        var image = new PdfRasterImage(PdfRasterFormat.Bilevel1, Width, Height, new byte[(Width / ByteBits) * Height]);

        await Assert.That(() => PdfImageWriter.AddRasterAsJpeg(new ListSink(), image, PdfJpegOptions.Default, PdfImageOptions.Default, CancellationToken.None)).Throws<ArgumentException>();
    }

    /// <summary>A CMYK picture written with the encoder and added with the writer comes back as the colours it was made from.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykJpegRoundTripsThroughThePdfColourPipeline()
    {
        var ink = JpegEncodeSamples.Patches(PdfJpegPixelFormat.Cmyk32, Width, Height);
        var jpeg = PdfJpegEncoder.Encode(ink, PdfJpegPixelFormat.Cmyk32, Width, Height, new() { Quality = HighQuality }, CancellationToken.None);
        var sink = new ListSink();

        var created = PdfImageWriter.AddJpeg(sink, jpeg);

        var stream = sink.GetStream(created.Id);
        var decode = stream.Dictionary.GetArray(KnownName.Decode)!;
        var inverted = new float[decode.Count];
        for (var i = 0; i < inverted.Length; i++)
        {
            inverted[i] = decode.GetInt32(i);
        }

        var header = new ImageHeader(Width, Height, SampleBits, false, false, PdfColorSpace.DeviceCmyk, inverted, null);

        var image = JpegImageDecoder.Decode(header, jpeg, JpegInfo.NoColorTransform);

        await Assert.That(image).IsNotNull();
        const int offset = ((Height / Quarter) * Width * BgraBytes) + ((Width / Quarter) * BgraBytes);
        var overGreen = image!.Pixels[offset + Red] - image.Pixels[offset + Green];
        var overBlue = image.Pixels[offset + Red] - image.Pixels[offset + Blue];
        await Assert.That(overGreen).IsGreaterThan(RedLead);
        await Assert.That(overBlue).IsGreaterThan(RedLead);
        await Assert.That(JpegMarkers.ReadComponentCount(jpeg)).IsEqualTo(InkComponents);
    }
}
