// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests for the managed JPEG decoder and the header rules.</summary>
public sealed class JpegDecoderTests
{
    /// <summary>The width of the generated images.</summary>
    private const int Width = 48;

    /// <summary>The height of the generated images.</summary>
    private const int Height = 40;

    /// <summary>The JPEG quality.</summary>
    private const int Quality = 90;

    /// <summary>The largest difference from Skia when chroma is not subsampled.</summary>
    private const int FullChromaTolerance = 3;

    /// <summary>The largest difference from Skia when chroma is subsampled; Skia smooths chroma and the managed decoder repeats it.</summary>
    private const int SubsampledTolerance = 14;

    /// <summary>The components of the RGB images.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of the CMYK images.</summary>
    private const int CmykComponents = 4;

    /// <summary>The blocks across the hand-built images.</summary>
    private const int BlocksAcross = 4;

    /// <summary>The blocks down the hand-built images.</summary>
    private const int BlocksDown = 2;

    /// <summary>The restart interval of the restart test, in blocks.</summary>
    private const int RestartInterval = 3;

    /// <summary>The value a gradient channel is held at.</summary>
    private const byte FlatChannel = 128;

    /// <summary>The number of bytes of the progressive CMYK file kept by the truncation test.</summary>
    private const int TruncatedLength = 760;

    /// <summary>The Adobe transform of a YCCK file.</summary>
    private const int YcckTransform = 2;

    /// <summary>The black value of the YCCK test.</summary>
    private const byte Black = 10;

    /// <summary>The CMY value that YCbCr (128, 128, 128) converts to: 255 minus 128.</summary>
    private const byte InvertedGray = 127;

    /// <summary>The Cr value that saturates red.</summary>
    private const byte HighChroma = 255;

    /// <summary>The multiplier that spreads the hand-built values.</summary>
    private const int ValueStride = 29;

    /// <summary>The offset that spreads the hand-built values by component.</summary>
    private const int ComponentStride = 53;

    /// <summary>The offset of the hand-built values.</summary>
    private const int ValueOffset = 7;

    /// <summary>The Adobe transform byte for no conversion.</summary>
    private const int AdobeNone = 0;

    /// <summary>The sample precision written in headers.</summary>
    private const int Precision = 8;

    /// <summary>The offset of the blue sample in an RGB pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>The CMYK pixel the gray YCCK test expects.</summary>
    private static readonly byte[] ExpectedGray = [InvertedGray, InvertedGray, InvertedGray, Black];

    /// <summary>The managed decoder matches Skia on a gray JPEG.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayMatchesSkia()
    {
        var jpeg = EncodeGray();
        var output = new byte[Width * Height];
        using var reference = SKBitmap.Decode(jpeg);

        await Assert.That(JpegDecoder.TryDecode(jpeg, false, output)).IsTrue();
        await Assert.That(MaxDifference(output, reference, 1)).IsLessThanOrEqualTo(FullChromaTolerance);
    }

    /// <summary>The managed decoder matches Skia on an RGB JPEG without chroma subsampling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbWithFullChromaMatchesSkia()
    {
        var jpeg = EncodeRgb(SKJpegEncoderDownsample.Downsample444);
        var output = new byte[Width * Height * RgbComponents];
        using var reference = SKBitmap.Decode(jpeg);

        await Assert.That(JpegDecoder.TryDecode(jpeg, true, output)).IsTrue();
        await Assert.That(MaxDifference(output, reference, RgbComponents)).IsLessThanOrEqualTo(FullChromaTolerance);
    }

    /// <summary>The managed decoder matches Skia closely on an RGB JPEG with 4:2:0 chroma.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbWithSubsampledChromaMatchesSkia()
    {
        var jpeg = EncodeRgb(SKJpegEncoderDownsample.Downsample420);
        var output = new byte[Width * Height * RgbComponents];
        using var reference = SKBitmap.Decode(jpeg);

        await Assert.That(JpegDecoder.TryDecode(jpeg, true, output)).IsTrue();
        await Assert.That(MaxDifference(output, reference, RgbComponents)).IsLessThanOrEqualTo(SubsampledTolerance);
    }

    /// <summary>The managed decoder reads a progressive JPEG like Skia does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressiveMatchesSkia()
    {
        var jpeg = JpegFixtures.ProgressiveRgb;
        var output = new byte[JpegFixtures.ProgressiveRgbWidth * JpegFixtures.ProgressiveRgbHeight * RgbComponents];
        using var reference = SKBitmap.Decode(jpeg);

        await Assert.That(JpegDecoder.TryDecode(jpeg, true, output)).IsTrue();
        await Assert.That(MaxDifference(output, reference, RgbComponents)).IsLessThanOrEqualTo(SubsampledTolerance);
    }

    /// <summary>Without conversion the samples are the stored values, with or without restart markers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RawSamplesSurviveRestartIntervals()
    {
        var values = BlockValues(CmykComponents);
        var plain = JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, CmykComponents, AdobeNone, 0), values);
        var restarts = JpegTestEncoder.Encode(new(BlocksAcross, BlocksDown, CmykComponents, AdobeNone, RestartInterval), values);
        var first = new byte[BlocksAcross * BlocksDown * JpegTestEncoder.BlockSide * JpegTestEncoder.BlockSide * CmykComponents];
        var second = new byte[first.Length];

        await Assert.That(JpegDecoder.TryDecode(plain, false, first)).IsTrue();
        await Assert.That(JpegDecoder.TryDecode(restarts, false, second)).IsTrue();
        await Assert.That(second).IsEquivalentTo(first);
        await Assert.That(Matches(first, values, CmykComponents)).IsTrue();
    }

    /// <summary>YCCK samples convert to CMYK with K unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task YcckConvertsToCmyk()
    {
        var gray = JpegTestEncoder.Encode(new(1, 1, CmykComponents, YcckTransform, 0), [FlatChannel, FlatChannel, FlatChannel, Black]);
        var bright = JpegTestEncoder.Encode(new(1, 1, CmykComponents, YcckTransform, 0), [FlatChannel, FlatChannel, HighChroma, Black]);
        var output = new byte[JpegTestEncoder.BlockSide * JpegTestEncoder.BlockSide * CmykComponents];

        await Assert.That(JpegDecoder.TryDecode(gray, true, output)).IsTrue();
        await Assert.That(output.AsSpan(0, CmykComponents).ToArray()).IsEquivalentTo(ExpectedGray);
        await Assert.That(JpegDecoder.TryDecode(bright, true, output)).IsTrue();
        await Assert.That(output[0]).IsEqualTo((byte)0);
    }

    /// <summary>A truncated progressive JPEG keeps what was decoded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncatedDataStillDecodes()
    {
        var jpeg = JpegFixtures.CmykProgressive.AsSpan(0, TruncatedLength).ToArray();
        var output = new byte[JpegFixtures.CmykProgressiveWidth * JpegFixtures.CmykProgressiveHeight * CmykComponents];

        await Assert.That(JpegDecoder.TryDecode(jpeg, false, output)).IsTrue();
    }

    /// <summary>Data that is not a JPEG fails.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GarbageFails()
    {
        var output = new byte[Width * Height];

        await Assert.That(JpegDecoder.TryDecode("not a jpeg"u8, false, output)).IsFalse();
        await Assert.That(JpegDecoder.TryDecode([], false, output)).IsFalse();
    }

    /// <summary>The header scan reads the size, process, component count and Adobe transform.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadersAreRead()
    {
        var found = JpegMarkers.TryReadInfo(JpegFixtures.CmykProgressive, out var cmyk);
        var rgbFound = JpegMarkers.TryReadInfo(JpegFixtures.ProgressiveRgb, out var rgb);

        await Assert.That(found).IsTrue();
        await Assert.That(cmyk.Components).IsEqualTo(CmykComponents);
        await Assert.That(cmyk.AdobeTransform).IsEqualTo(YcckTransform);
        await Assert.That(cmyk.Process).IsEqualTo(JpegProcess.Progressive);
        await Assert.That(cmyk.Width).IsEqualTo(JpegFixtures.CmykProgressiveWidth);
        await Assert.That(cmyk.Height).IsEqualTo(JpegFixtures.CmykProgressiveHeight);
        await Assert.That(rgbFound).IsTrue();
        await Assert.That(rgb.Components).IsEqualTo(RgbComponents);
        await Assert.That(rgb.HasAdobe).IsFalse();
    }

    /// <summary>Three-component files convert unless the Adobe marker says 0 or, without it, /ColorTransform is 0.</summary>
    /// <param name="adobe">The Adobe transform, or -1.</param>
    /// <param name="colorTransform">The /ColorTransform value, or -1.</param>
    /// <param name="expected">Whether YCbCr converts to RGB.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1, -1, true)]
    [Arguments(-1, 1, true)]
    [Arguments(-1, 0, false)]
    [Arguments(0, 1, false)]
    [Arguments(1, 0, true)]
    [Arguments(0, -1, false)]
    public async Task ThreeComponentRule(int adobe, int colorTransform, bool expected)
    {
        var info = new JpegInfo(1, 1, RgbComponents, Precision, JpegProcess.Sequential, adobe, false);

        await Assert.That(info.UsesColorTransform(colorTransform)).IsEqualTo(expected);
    }

    /// <summary>Three components labelled R, G and B are never converted unless Adobe says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbIdsAreNotConverted()
    {
        var info = new JpegInfo(1, 1, RgbComponents, Precision, JpegProcess.Sequential, -1, true);

        await Assert.That(info.UsesColorTransform(-1)).IsFalse();
    }

    /// <summary>Four-component files convert when the Adobe marker is not 0 or, without it, /ColorTransform is 1.</summary>
    /// <param name="adobe">The Adobe transform, or -1.</param>
    /// <param name="colorTransform">The /ColorTransform value, or -1.</param>
    /// <param name="expected">Whether YCCK converts to CMYK.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1, -1, false)]
    [Arguments(-1, 0, false)]
    [Arguments(-1, 1, true)]
    [Arguments(0, 1, false)]
    [Arguments(2, 0, true)]
    [Arguments(2, -1, true)]
    public async Task FourComponentRule(int adobe, int colorTransform, bool expected)
    {
        var info = new JpegInfo(1, 1, CmykComponents, Precision, JpegProcess.Sequential, adobe, false);

        await Assert.That(info.UsesColorTransform(colorTransform)).IsEqualTo(expected);
    }

    /// <summary>Makes the values of the hand-built blocks.</summary>
    /// <param name="components">The components per block.</param>
    /// <returns>One value per block and component.</returns>
    internal static byte[] BlockValues(int components)
    {
        var values = new byte[BlocksAcross * BlocksDown * components];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = (byte)(((i * ValueStride) + ((i % components) * ComponentStride) + ValueOffset) % (byte.MaxValue + 1));
        }

        return values;
    }

    /// <summary>Checks that decoded samples equal the per-block values.</summary>
    /// <param name="samples">The decoded interleaved samples.</param>
    /// <param name="values">The block values.</param>
    /// <param name="components">The components per pixel.</param>
    /// <returns><see langword="true"/> when every sample matches.</returns>
    private static bool Matches(byte[] samples, byte[] values, int components)
    {
        const int width = BlocksAcross * JpegTestEncoder.BlockSide;
        for (var i = 0; i < samples.Length; i++)
        {
            var pixel = i / components;
            var block = (((pixel / width) / JpegTestEncoder.BlockSide) * BlocksAcross) + ((pixel % width) / JpegTestEncoder.BlockSide);
            if (samples[i] != values[(block * components) + (i % components)])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the largest per-channel difference between decoded samples and a Skia bitmap.</summary>
    /// <param name="samples">The interleaved samples: gray or RGB.</param>
    /// <param name="reference">The Skia decode.</param>
    /// <param name="components">The components per pixel.</param>
    /// <returns>The largest absolute difference.</returns>
    private static int MaxDifference(byte[] samples, SKBitmap reference, int components)
    {
        var largest = 0;
        for (var y = 0; y < reference.Height; y++)
        {
            for (var x = 0; x < reference.Width; x++)
            {
                largest = Math.Max(largest, PixelDifference(samples.AsSpan(((y * reference.Width) + x) * components, components), reference.GetPixel(x, y)));
            }
        }

        return largest;
    }

    /// <summary>Finds the largest per-channel difference of one pixel; one sample means gray.</summary>
    /// <param name="sample">The decoded samples of the pixel.</param>
    /// <param name="color">The reference colour.</param>
    /// <returns>The largest absolute difference.</returns>
    private static int PixelDifference(ReadOnlySpan<byte> sample, SKColor color)
    {
        var largest = Math.Abs(sample[0] - color.Red);
        if (sample.Length != RgbComponents)
        {
            return largest;
        }

        largest = Math.Max(largest, Math.Abs(sample[1] - color.Green));
        return Math.Max(largest, Math.Abs(sample[BlueOffset] - color.Blue));
    }

    /// <summary>Encodes a bitmap as a JPEG.</summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="downsample">The chroma subsampling.</param>
    /// <returns>The JPEG bytes.</returns>
    /// <exception cref="InvalidOperationException">SkiaSharp could not encode the bitmap.</exception>
    private static byte[] Encode(SKBitmap bitmap, SKJpegEncoderDownsample downsample)
    {
        using var pixmap = bitmap.PeekPixels() ?? throw new InvalidOperationException("The bitmap has no pixels.");
        using var data = pixmap.Encode(new SKJpegEncoderOptions(Quality, downsample, SKJpegEncoderAlphaOption.Ignore)) ?? throw new InvalidOperationException("The bitmap did not encode.");
        return data.ToArray();
    }

    /// <summary>Encodes a smooth RGB gradient.</summary>
    /// <param name="downsample">The chroma subsampling.</param>
    /// <returns>The JPEG bytes.</returns>
    private static byte[] EncodeRgb(SKJpegEncoderDownsample downsample)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                bitmap.SetPixel(x, y, new((byte)(x * byte.MaxValue / (Width - 1)), (byte)(y * byte.MaxValue / (Height - 1)), FlatChannel));
            }
        }

        return Encode(bitmap, downsample);
    }

    /// <summary>Encodes a smooth gray gradient.</summary>
    /// <returns>The JPEG bytes.</returns>
    private static byte[] EncodeGray()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Gray8, SKAlphaType.Opaque));
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                pixels[(y * Width) + x] = (byte)(((x * byte.MaxValue / (Width - 1)) + (y * byte.MaxValue / (Height - 1))) / BlueOffset);
            }
        }

        return Encode(bitmap, SKJpegEncoderDownsample.Downsample444);
    }
}
