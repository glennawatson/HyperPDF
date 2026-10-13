// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Images added to a document: JPEG bytes kept as they are, lossless pixels with predictors, soft masks, fax coding and ICC colour.</summary>
public sealed class ImageWriterTests
{
    /// <summary>The width and height of the test JPEG.</summary>
    private const int JpegSize = 16;

    /// <summary>The quality of the test JPEG.</summary>
    private const int JpegQuality = 90;

    /// <summary>The red level of the test JPEG.</summary>
    private const byte JpegRed = 200;

    /// <summary>The green level of the test JPEG.</summary>
    private const byte JpegGreen = 100;

    /// <summary>The blue level of the test JPEG.</summary>
    private const byte JpegBlue = 50;

    /// <summary>The width of the raster tests.</summary>
    private const int Width = 19;

    /// <summary>The height of the raster tests.</summary>
    private const int Height = 11;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes in a row of one-bit pixels.</summary>
    private const int Stride = (Width + ByteBits - 1) / ByteBits;

    /// <summary>The bits of the last byte of a row that hold pixels; the rest is padding.</summary>
    private const byte PaddingMask = unchecked((byte)(0xFF << (ByteBits - (Width % ByteBits))));

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The bytes of an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The offset of the green sample in a BGRA pixel.</summary>
    private const int GreenOffset = 1;

    /// <summary>The offset of the red sample in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The index of the blue sample in a decoded RGB pixel.</summary>
    private const int BlueIndex = 2;

    /// <summary>The multiplier that spreads the test pattern.</summary>
    private const int Spread = 5;

    /// <summary>The predictor that lets the encoder pick a filter for each row.</summary>
    private const int OptimumPredictor = 15;

    /// <summary>The length of an ICC profile header.</summary>
    private const int IccHeaderLength = 128;

    /// <summary>The offset of the data colour space in an ICC profile header.</summary>
    private const int IccSpaceOffset = 16;

    /// <summary>The offset of the file signature in an ICC profile header.</summary>
    private const int IccSignatureOffset = 36;

    /// <summary>The components of a CMYK image.</summary>
    private const int CmykComponents = 4;

    /// <summary>The length field of the Adobe marker segment.</summary>
    private const byte AdobeSegmentLength = 14;

    /// <summary>The length field of the frame header of a four component JPEG.</summary>
    private const byte FrameLength = 20;

    /// <summary>The 8-bit sample precision.</summary>
    private const byte Precision = 8;

    /// <summary>The size of the synthetic CMYK frame.</summary>
    private const byte FrameSize = 16;

    /// <summary>The blue level of the translucent test pixels.</summary>
    private const byte BlueLevel = 10;

    /// <summary>The green level of the translucent test pixels.</summary>
    private const byte GreenLevel = 20;

    /// <summary>The red level of the translucent test pixels.</summary>
    private const byte RedLevel = 30;

    /// <summary>The alpha of the faint test pixels.</summary>
    private const byte FaintAlpha = 64;

    /// <summary>The width in pixels of the runs in the bilevel pattern.</summary>
    private const int RunWidth = 3;

    /// <summary>Gets the bytes of the start of image marker that begins a JPEG.</summary>
    private static ReadOnlySpan<byte> StartOfImage => [0xFF, 0xD8];

    /// <summary>A JPEG is stored as it is, under DCTDecode, with its size.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegBytesPassThrough()
    {
        var jpeg = EncodeJpeg();
        var sink = new ListSink();

        var image = PdfImageWriter.AddJpeg(sink, jpeg);

        var stream = sink.GetStream(image.Id);
        await Assert.That(stream.CopyRawData()).IsEquivalentTo(jpeg);
        await Assert.That(stream.Dictionary.IsName(KnownName.Filter, KnownName.DCTDecode)).IsTrue();
        await Assert.That(stream.Dictionary.IsName(KnownName.ColorSpace, KnownName.DeviceRGB)).IsTrue();
        await Assert.That(image.Width).IsEqualTo(JpegSize);
        await Assert.That(image.Height).IsEqualTo(JpegSize);
        await Assert.That(stream.Dictionary.GetInt32(KnownName.BitsPerComponent)).IsEqualTo(Precision);
    }

    /// <summary>A JPEG from a stream is read asynchronously and stored as it is.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegFromStreamPassesThrough(CancellationToken cancellationToken)
    {
        var jpeg = EncodeJpeg();
        var sink = new ListSink();
        await using var source = new MemoryStream(jpeg);

        var image = await PdfImageWriter.AddJpegAsync(sink, source, PdfImageOptions.Default, cancellationToken);

        await Assert.That(sink.GetStream(image.Id).CopyRawData()).IsEquivalentTo(jpeg);
    }

    /// <summary>A CMYK JPEG with an Adobe marker gets the inverting Decode array that restores its samples.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AdobeCmykJpegIsInverted()
    {
        var sink = new ListSink();

        var image = PdfImageWriter.AddJpeg(sink, CreateAdobeCmykHeader());

        var stream = sink.GetStream(image.Id);
        var decode = stream.Dictionary.GetArray(KnownName.Decode)!;
        await Assert.That(stream.Dictionary.IsName(KnownName.ColorSpace, KnownName.DeviceCMYK)).IsTrue();
        await Assert.That(decode.Count).IsEqualTo(CmykComponents + CmykComponents);
        await Assert.That(decode.GetInt32(0)).IsEqualTo(1);
        await Assert.That(decode.GetInt32(1)).IsEqualTo(0);
    }

    /// <summary>Bytes that are not a JPEG are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonJpegBytesAreRefused()
    {
        var threw = false;
        try
        {
            _ = PdfImageWriter.AddJpeg(new ListSink(), new byte[Width]);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>Gray and RGB pixels are stored with PNG predictors and decode to the same bytes.</summary>
    /// <param name="format">The pixel format.</param>
    /// <param name="bytesPerPixel">The bytes of a pixel.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRasterFormat.Gray8, 1)]
    [Arguments(PdfRasterFormat.Rgb24, RgbBytes)]
    public async Task LosslessPixelsRoundTrip(PdfRasterFormat format, int bytesPerPixel)
    {
        var pixels = CreatePixels(Width * Height * bytesPerPixel);
        var sink = new ListSink();

        var image = PdfImageWriter.AddRaster(sink, new(format, Width, Height, pixels));

        var stream = sink.GetStream(image.Id);
        var parameters = stream.Dictionary.GetDictionary(KnownName.DecodeParms)!;
        await Assert.That(parameters.GetInt32(KnownName.Predictor)).IsEqualTo(OptimumPredictor);
        await Assert.That(parameters.GetInt32(KnownName.Colors)).IsEqualTo(bytesPerPixel);
        await Assert.That(stream.Dictionary.IsName(KnownName.Filter, KnownName.FlateDecode)).IsTrue();
        await Assert.That(stream.DecodeToArray()).IsEquivalentTo(pixels);
    }

    /// <summary>Transparency goes to a gray soft mask and the colour to RGB; opaque pixels need no mask.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlphaBecomesASoftMask()
    {
        var translucent = CreateTranslucent(false);
        var opaque = CreateTranslucent(true);
        var transparentSink = new ListSink();
        var opaqueSink = new ListSink();

        var withMask = PdfImageWriter.AddRaster(transparentSink, new(PdfRasterFormat.Bgra32, Width, Height, translucent));
        var withoutMask = PdfImageWriter.AddRaster(opaqueSink, new(PdfRasterFormat.Bgra32, Width, Height, opaque));

        var image = transparentSink.GetStream(withMask.Id);
        var mask = transparentSink.Resolve(image.Dictionary.GetRaw(KnownName.SMask)).AsStream()!;
        var colors = image.DecodeToArray();
        var alpha = mask.DecodeToArray();
        await Assert.That(mask.Dictionary.IsName(KnownName.ColorSpace, KnownName.DeviceGray)).IsTrue();
        await Assert.That(colors[0]).IsEqualTo(RedLevel);
        await Assert.That(colors[1]).IsEqualTo(GreenLevel);
        await Assert.That(colors[BlueIndex]).IsEqualTo(BlueLevel);
        await Assert.That(alpha[1]).IsEqualTo(FaintAlpha);
        await Assert.That(opaqueSink.GetStream(withoutMask.Id).Dictionary.ContainsKey(KnownName.SMask)).IsFalse();
    }

    /// <summary>One-bit pixels are coded as CCITT Group 4 and decode to the same rows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BilevelPixelsUseGroup4()
    {
        var rows = CreateStripes();
        var sink = new ListSink();

        var image = PdfImageWriter.AddRaster(sink, new(PdfRasterFormat.Bilevel1, Width, Height, rows));

        var stream = sink.GetStream(image.Id);
        var parameters = stream.Dictionary.GetDictionary(KnownName.DecodeParms)!;
        await Assert.That(stream.Dictionary.IsName(KnownName.Filter, KnownName.CCITTFaxDecode)).IsTrue();
        await Assert.That(parameters.GetInt32(KnownName.K)).IsEqualTo(-1);
        await Assert.That(parameters.GetInt32(KnownName.Columns)).IsEqualTo(Width);
        await Assert.That(stream.Dictionary.GetInt32(KnownName.BitsPerComponent)).IsEqualTo(1);
        var decoded = new byte[rows.Length];
        CcittFaxDecoder.Decode(stream.CopyRawData(), new(-1, Width, Height, false, false, false, true), Width, Height, decoded);
        await Assert.That(MaskPadding(decoded)).IsEquivalentTo(MaskPadding(rows));
    }

    /// <summary>A stencil mask has no colour space and is marked as an image mask.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StencilIsAnImageMask()
    {
        var sink = new ListSink();
        var options = new PdfImageOptions { AsStencil = true };

        var image = PdfImageWriter.AddRaster(sink, new(PdfRasterFormat.Bilevel1, Width, Height, new byte[Stride * Height]), options, CancellationToken.None);

        var dictionary = sink.GetStream(image.Id).Dictionary;
        await Assert.That(dictionary.GetBoolean(KnownName.ImageMask)).IsTrue();
        await Assert.That(dictionary.ContainsKey(KnownName.ColorSpace)).IsFalse();
    }

    /// <summary>An ICC profile becomes an ICCBased colour space that images use.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IccProfileBecomesTheColorSpace()
    {
        var sink = new ListSink();
        var profile = CreateProfile("RGB "u8);
        var space = PdfImageWriter.AddIccColorSpace(sink, profile);
        var options = new PdfImageOptions { IccColorSpace = space };

        var image = PdfImageWriter.AddRaster(sink, new(PdfRasterFormat.Rgb24, Width, Height, CreatePixels(Width * Height * RgbBytes)), options, CancellationToken.None);

        var array = sink.Get(space.Id).AsArray()!;
        var stored = sink.Resolve(array.GetRaw(1)).AsStream()!;
        var colorSpace = sink.GetStream(image.Id).Dictionary.GetRaw(KnownName.ColorSpace);
        await Assert.That(space.Components).IsEqualTo(RgbBytes);
        await Assert.That(array.GetName(0)).IsEqualTo((PdfName)KnownName.ICCBased);
        await Assert.That(stored.Dictionary.GetInt32(KnownName.N)).IsEqualTo(RgbBytes);
        await Assert.That(stored.DecodeToArray()).IsEquivalentTo(profile);
        await Assert.That(colorSpace.AsReference()).IsEqualTo(space.Id);
    }

    /// <summary>A profile for another number of components, or bytes that are not a profile, are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadProfilesAreRefused()
    {
        var sink = new ListSink();
        var notProfile = false;
        var wrongSpace = false;
        try
        {
            _ = PdfImageWriter.AddIccColorSpace(sink, new byte[IccHeaderLength]);
        }
        catch (ArgumentException)
        {
            notProfile = true;
        }

        var options = new PdfImageOptions { IccColorSpace = PdfImageWriter.AddIccColorSpace(sink, CreateProfile("GRAY"u8)) };
        try
        {
            _ = PdfImageWriter.AddRaster(sink, new(PdfRasterFormat.Rgb24, Width, Height, CreatePixels(Width * Height * RgbBytes)), options, CancellationToken.None);
        }
        catch (ArgumentException)
        {
            wrongSpace = true;
        }

        await Assert.That(notProfile).IsTrue();
        await Assert.That(wrongSpace).IsTrue();
    }

    /// <summary>Pixels that do not fill the image are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortPixelsAreRefused()
    {
        var threw = false;
        try
        {
            _ = PdfImageWriter.AddRaster(new ListSink(), new(PdfRasterFormat.Rgb24, Width, Height, new byte[Width]));
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>Clears the padding bits at the end of each row, which the fax decoder may set either way.</summary>
    /// <param name="rows">The packed rows.</param>
    /// <returns>The rows with the padding bits cleared.</returns>
    private static byte[] MaskPadding(byte[] rows)
    {
        var copy = (byte[])rows.Clone();
        for (var at = Stride - 1; at < copy.Length; at += Stride)
        {
            copy[at] &= PaddingMask;
        }

        return copy;
    }

    /// <summary>Makes one-bit rows of vertical stripes that shift from row to row.</summary>
    /// <returns>The packed rows.</returns>
    private static byte[] CreateStripes()
    {
        var rows = new byte[Stride * Height];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (((x / RunWidth) + y) % RunWidth == 0)
                {
                    rows[(y * Stride) + (x / ByteBits)] |= (byte)(0x80 >> (x % ByteBits));
                }
            }
        }

        return rows;
    }

    /// <summary>Makes BGRA pixels with a fixed colour and alternating alpha.</summary>
    /// <param name="opaque">Whether every pixel is opaque.</param>
    /// <returns>The pixels.</returns>
    private static byte[] CreateTranslucent(bool opaque)
    {
        var pixels = new byte[Width * Height * BgraBytes];
        for (var i = 0; i < Width * Height; i++)
        {
            var at = i * BgraBytes;
            pixels[at] = BlueLevel;
            pixels[at + GreenOffset] = GreenLevel;
            pixels[at + RedOffset] = RedLevel;
            pixels[at + AlphaOffset] = opaque || i % RunWidth == 0 ? byte.MaxValue : FaintAlpha;
        }

        return pixels;
    }

    /// <summary>Makes a deterministic pixel pattern.</summary>
    /// <param name="count">The bytes.</param>
    /// <returns>The pixels.</returns>
    private static byte[] CreatePixels(int count)
    {
        var pixels = new byte[count];
        for (var i = 0; i < count; i++)
        {
            pixels[i] = (byte)(i * Spread);
        }

        return pixels;
    }

    /// <summary>Makes the header of an ICC profile, which is all the writer reads.</summary>
    /// <param name="space">The four character data colour space.</param>
    /// <returns>The profile bytes.</returns>
    private static byte[] CreateProfile(ReadOnlySpan<byte> space)
    {
        var profile = new byte[IccHeaderLength];
        space.CopyTo(profile.AsSpan(IccSpaceOffset));
        "acsp"u8.CopyTo(profile.AsSpan(IccSignatureOffset));
        return profile;
    }

    /// <summary>Makes the headers of a four component JPEG with an Adobe marker.</summary>
    /// <returns>The bytes, which hold only the markers the writer reads.</returns>
    private static byte[] CreateAdobeCmykHeader()
    {
        List<byte> bytes = [.. StartOfImage, 0xFF, 0xEE, 0x00, AdobeSegmentLength, .. "Adobe"u8, 0x00, 0x64, 0x00, 0x00, 0x00, 0x00, 0x00];
        bytes.AddRange([0xFF, 0xC0, 0x00, FrameLength, Precision, 0x00, FrameSize, 0x00, FrameSize, CmykComponents]);
        for (byte component = 1; component <= CmykComponents; component++)
        {
            bytes.AddRange([component, 0x11, 0x00]);
        }

        bytes.AddRange([0xFF, 0xD9]);
        return [.. bytes];
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
