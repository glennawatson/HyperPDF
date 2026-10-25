// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia.Images;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Checks codec output and caller-owned buffer bounds.</summary>
public sealed class SkiaImageCodecTests
{
    /// <summary>The source side in pixels.</summary>
    private const int Side = 4;

    /// <summary>The resampled side in pixels.</summary>
    private const int TargetSide = 2;

    /// <summary>A neutral gray sample.</summary>
    private const byte Gray = 128;

    /// <summary>A sentinel that reveals writes to a refused destination.</summary>
    private const byte Sentinel = 59;

    /// <summary>The highest JPEG quality.</summary>
    private const int Quality = 100;

    /// <summary>The stored red channel of a half-transparent red pixel.</summary>
    private const byte PremultipliedRed = 64;

    /// <summary>The alpha of a half-transparent pixel.</summary>
    private const byte Alpha = 128;

    /// <summary>The red channel index in BGRA pixels.</summary>
    private const int RedIndex = 2;

    /// <summary>The alpha channel index in BGRA pixels.</summary>
    private const int AlphaIndex = 3;

    /// <summary>The bytes of one BGRA pixel.</summary>
    private const int ColorBytes = 4;

    /// <summary>The allowed channel difference from JPEG encoding.</summary>
    private const int JpegTolerance = 2;

    /// <summary>A valid gray JPEG round trip writes into the caller's destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayJpegRoundTripUsesCallerBuffer()
    {
        var codec = new SkiaPdfImageCodec();
        var layout = new PdfImagePixelLayout(Side, Side, PdfImagePixelFormat.Gray8);
        var pixels = new byte[Side * Side];
        Array.Fill(pixels, Gray);
        var jpeg = codec.EncodeJpeg(pixels, layout, Quality);
        await Assert.That(jpeg).IsNotNull();
        var decoded = new byte[pixels.Length];

        await Assert.That(codec.TryDecodeJpeg(jpeg!, layout, decoded)).IsTrue();
        await Assert.That(decoded).IsEquivalentTo(pixels);
    }

    /// <summary>The Mitchell resample retains a constant gray image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResamplePreservesConstantGray()
    {
        var codec = new SkiaPdfImageCodec();
        var pixels = new byte[Side * Side];
        Array.Fill(pixels, Gray);
        var output = new byte[TargetSide * TargetSide];

        await Assert.That(codec.Resample(pixels, new(Side, Side, PdfImagePixelFormat.Gray8), TargetSide, TargetSide, output)).IsTrue();
        await Assert.That(Array.TrueForAll(output, static pixel => pixel == Gray)).IsTrue();
    }

    /// <summary>BGRA resampling retains the alpha and premultiplied color of a translucent pixel.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResamplePreservesPremultipliedAlpha()
    {
        var codec = new SkiaPdfImageCodec();
        byte[] source = [0, 0, PremultipliedRed, Alpha];
        var output = new byte[TargetSide * TargetSide * ColorBytes];

        await Assert.That(codec.Resample(source, new(1, 1, PdfImagePixelFormat.Bgra8888), TargetSide, TargetSide, output)).IsTrue();
        await Assert.That(output[AlphaIndex]).IsEqualTo(Alpha);
        await Assert.That(output[RedIndex]).IsEqualTo(PremultipliedRed);
    }

    /// <summary>JPEG encodes premultiplied BGRA over black and decodes opaque alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegCompositesPremultipliedPixelsOverBlack()
    {
        var codec = new SkiaPdfImageCodec();
        var layout = new PdfImagePixelLayout(1, 1, PdfImagePixelFormat.Bgra8888);
        byte[] source = [0, 0, PremultipliedRed, Alpha];
        var jpeg = codec.EncodeJpeg(source, layout, Quality);
        await Assert.That(jpeg).IsNotNull();
        var decoded = new byte[ColorBytes];

        await Assert.That(codec.TryDecodeJpeg(jpeg!, layout, decoded)).IsTrue();
        await Assert.That(decoded[AlphaIndex]).IsEqualTo(byte.MaxValue);
        await Assert.That(Math.Abs(decoded[RedIndex] - PremultipliedRed)).IsLessThanOrEqualTo(JpegTolerance);
    }

    /// <summary>Invalid layouts are refused before a native codec sees any buffer.</summary>
    /// <param name="width">The requested width.</param>
    /// <param name="height">The requested height.</param>
    /// <param name="length">The available bytes.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, 1, 1)]
    [Arguments(1, 0, 1)]
    [Arguments(1, 1, 0)]
    [Arguments(int.MaxValue, int.MaxValue, 1)]
    public async Task InvalidLayoutsAreRefused(int width, int height, int length)
    {
        var codec = new SkiaPdfImageCodec();
        var layout = new PdfImagePixelLayout(width, height, PdfImagePixelFormat.Bgra8888);
        var pixels = new byte[length];
        Array.Fill(pixels, Sentinel);

        await Assert.That(codec.TryDecodeJpeg([], layout, pixels)).IsFalse();
        await Assert.That(codec.EncodeJpeg(pixels, layout, Quality)).IsNull();
        await Assert.That(codec.Resample(pixels, layout, 1, 1, pixels)).IsFalse();
        await Assert.That(Array.TrueForAll(pixels, static pixel => pixel == Sentinel)).IsTrue();
    }

    /// <summary>Unknown pixel formats are refused before any codec accesses the buffers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownFormatsAreRefused()
    {
        var codec = new SkiaPdfImageCodec();
        var layout = new PdfImagePixelLayout(1, 1, (PdfImagePixelFormat)(-1));
        var pixels = new byte[ColorBytes];
        Array.Fill(pixels, Sentinel);

        await Assert.That(codec.TryDecodeJpeg([], layout, pixels)).IsFalse();
        await Assert.That(codec.EncodeJpeg(pixels, layout, Quality)).IsNull();
        await Assert.That(codec.Resample(pixels, layout, 1, 1, pixels)).IsFalse();
        await Assert.That(Array.TrueForAll(pixels, static pixel => pixel == Sentinel)).IsTrue();
    }

    /// <summary>A JPEG with different dimensions cannot write into the requested destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpegDimensionMismatchRetainsDestination()
    {
        var codec = new SkiaPdfImageCodec();
        var source = new byte[Side * Side];
        Array.Fill(source, Gray);
        var jpeg = codec.EncodeJpeg(source, new(Side, Side, PdfImagePixelFormat.Gray8), Quality);
        await Assert.That(jpeg).IsNotNull();
        var pixels = new byte[(Side + 1) * Side];
        Array.Fill(pixels, Sentinel);

        await Assert.That(codec.TryDecodeJpeg(jpeg!, new(Side + 1, Side, PdfImagePixelFormat.Gray8), pixels)).IsFalse();
        await Assert.That(Array.TrueForAll(pixels, static pixel => pixel == Sentinel)).IsTrue();
    }
}
