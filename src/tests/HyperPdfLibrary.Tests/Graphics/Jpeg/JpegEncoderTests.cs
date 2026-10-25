// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>Tests that the managed JPEG encoder writes files the library's decoder and an independent decoder (Skia) both read.</summary>
public sealed class JpegEncoderTests
{
    /// <summary>The width of the test pictures; not a multiple of any MCU size.</summary>
    private const int Width = 61;

    /// <summary>The height of the test pictures; not a multiple of any MCU size.</summary>
    private const int Height = 45;

    /// <summary>The quality of the round-trip tests.</summary>
    private const int Quality = 90;

    /// <summary>The highest quality.</summary>
    private const int BestQuality = 100;

    /// <summary>The components of an RGB picture.</summary>
    private const int RgbComponents = 3;

    /// <summary>The Adobe transform of a YCCK file.</summary>
    private const int YcckTransform = 2;

    /// <summary>The highest sample that is not white enough to count as white.</summary>
    private const byte NearWhite = 253;

    /// <summary>The bytes of a four-byte pixel.</summary>
    private const int QuadBytes = 4;

    /// <summary>The components of an ink picture.</summary>
    private const int InkComponents = 4;

    /// <summary>The largest mean error of a full-quality round trip without subsampling.</summary>
    private const double BestMeanError = 2.0;

    /// <summary>The largest mean error of a round trip at quality 90.</summary>
    private const double GoodMeanError = 6.0;

    /// <summary>The largest difference from Skia when chroma is not subsampled.</summary>
    private const int FullChromaTolerance = 3;

    /// <summary>The largest difference from Skia when chroma is subsampled; Skia smooths chroma and the managed decoder repeats it.</summary>
    private const int SubsampledTolerance = 16;

    /// <summary>The restart interval of the restart tests.</summary>
    private const int RestartInterval = 5;

    /// <summary>The largest mean error of the patch pictures.</summary>
    private const double PatchMeanError = 3.0;

    /// <summary>The sampling arrangements tried: each component's sampling byte (high nibble horizontal, low nibble vertical) and whether one scan holds them all.</summary>
    private static readonly SamplingCase[] Arrangements =
    [
        new([0x11, 0x11, 0x11], true),
        new([0x21, 0x11, 0x11], true),
        new([0x12, 0x11, 0x11], true),
        new([0x22, 0x11, 0x11], true),
        new([0x41, 0x11, 0x11], true),
        new([0x42, 0x21, 0x11], false),
        new([0x22, 0x22, 0x11], true),
        new([0x44, 0x22, 0x42], false),
        new([0x22, 0x11, 0x11], false),
        new([0x41, 0x21, 0x11], false),
    ];

    /// <summary>A picture of gray samples round-trips through the library's decoder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayRoundTrips()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Gray8, Width, Height);

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Gray8, Width, Height, new() { Quality = BestQuality }, CancellationToken.None);

        await Assert.That(MeanError(Decode(jpeg, 1, false), pixels)).IsLessThan(BestMeanError);
        await Assert.That(MaxSkiaDifference(jpeg, 1)).IsLessThanOrEqualTo(FullChromaTolerance);
    }

    /// <summary>An RGB picture round-trips through the library's decoder, whatever the subsampling.</summary>
    /// <param name="subsampling">The chroma subsampling.</param>
    /// <param name="skiaTolerance">The largest allowed difference from Skia's decode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpegSubsampling.Chroma444, FullChromaTolerance)]
    [Arguments(PdfJpegSubsampling.Chroma422, SubsampledTolerance)]
    [Arguments(PdfJpegSubsampling.Chroma420, SubsampledTolerance)]
    [Arguments(PdfJpegSubsampling.Chroma440, SubsampledTolerance)]
    [Arguments(PdfJpegSubsampling.Chroma411, SubsampledTolerance)]
    public async Task RgbRoundTripsWithEverySubsampling(PdfJpegSubsampling subsampling, int skiaTolerance)
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, new() { Quality = Quality, Subsampling = subsampling }, CancellationToken.None);

        await Assert.That(MeanError(Decode(jpeg, RgbComponents, true), pixels)).IsLessThan(GoodMeanError);
        await Assert.That(MaxSkiaDifference(jpeg, RgbComponents)).IsLessThanOrEqualTo(skiaTolerance);
    }

    /// <summary>Without subsampling and at full quality an RGB picture comes back almost exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbAtFullQualityIsNearlyLossless()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, new() { Quality = BestQuality, Subsampling = PdfJpegSubsampling.Chroma444 }, CancellationToken.None);

        await Assert.That(MeanError(Decode(jpeg, RgbComponents, true), pixels)).IsLessThan(BestMeanError);
    }

    /// <summary>Flat colour patches survive 4:2:0 subsampling.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PatchesSurviveChromaSubsampling()
    {
        var pixels = JpegEncodeSamples.Patches(PdfJpegPixelFormat.Rgb24, Width, Height);

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, new() { Quality = Quality, Subsampling = PdfJpegSubsampling.Chroma420 }, CancellationToken.None);

        await Assert.That(MeanError(Decode(jpeg, RgbComponents, true), pixels)).IsLessThan(PatchMeanError);
    }

    /// <summary>The RGBX and BGRA layouts give the same JPEG as RGB when nothing is transparent.</summary>
    /// <param name="format">The layout.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpegPixelFormat.Rgbx32)]
    [Arguments(PdfJpegPixelFormat.Bgra32)]
    public async Task FourByteLayoutsMatchRgb(PdfJpegPixelFormat format)
    {
        var rgb = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);
        var quad = JpegEncodeSamples.Pixels(format, Width, Height);

        var expected = PdfJpegEncoder.Encode(rgb, PdfJpegPixelFormat.Rgb24, Width, Height, PdfJpegOptions.Default, CancellationToken.None);
        var actual = PdfJpegEncoder.Encode(quad, format, Width, Height, PdfJpegOptions.Default, CancellationToken.None);

        await Assert.That(actual).IsEquivalentTo(expected);
    }

    /// <summary>Transparent BGRA pixels are composited over white.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TransparentPixelsBecomeWhite()
    {
        var pixels = new byte[Width * Height * QuadBytes];

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Bgra32, Width, Height, new() { Quality = BestQuality }, CancellationToken.None);

        var decoded = Decode(jpeg, RgbComponents, true);
        await Assert.That(decoded.AsSpan().IndexOfAnyInRange((byte)0, NearWhite)).IsEqualTo(-1);
    }

    /// <summary>A progressive file holds the same coefficients as the sequential file, so it decodes to identical samples.</summary>
    /// <param name="format">The pixel format.</param>
    /// <param name="subsampling">The chroma subsampling.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpegPixelFormat.Gray8, PdfJpegSubsampling.Automatic)]
    [Arguments(PdfJpegPixelFormat.Rgb24, PdfJpegSubsampling.Chroma444)]
    [Arguments(PdfJpegPixelFormat.Rgb24, PdfJpegSubsampling.Chroma420)]
    [Arguments(PdfJpegPixelFormat.Rgb24, PdfJpegSubsampling.Chroma411)]
    [Arguments(PdfJpegPixelFormat.Cmyk32, PdfJpegSubsampling.Automatic)]
    public async Task ProgressiveDecodesLikeSequential(PdfJpegPixelFormat format, PdfJpegSubsampling subsampling)
    {
        var pixels = JpegEncodeSamples.Pixels(format, Width, Height);
        var components = ComponentsOf(format);

        var sequential = PdfJpegEncoder.Encode(pixels, format, Width, Height, new() { Quality = Quality, Subsampling = subsampling }, CancellationToken.None);
        var progressive = PdfJpegEncoder.Encode(pixels, format, Width, Height, new() { Quality = Quality, Subsampling = subsampling, Progressive = true }, CancellationToken.None);

        await Assert.That(JpegMarkers.TryReadInfo(progressive, out var info)).IsTrue();
        await Assert.That(info.Process).IsEqualTo(JpegProcess.Progressive);
        await Assert.That(Decode(progressive, components, true)).IsEquivalentTo(Decode(sequential, components, true));
        if (components != InkComponents)
        {
            await Assert.That(MaxSkiaDifference(progressive, components)).IsLessThanOrEqualTo(SubsampledTolerance);
        }
    }

    /// <summary>The example Huffman tables decode to the same samples as the optimised ones, and the optimised file is smaller.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExampleTablesDecodeLikeOptimisedTables()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);

        var optimised = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, new() { Quality = Quality }, CancellationToken.None);
        var example = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, new() { Quality = Quality, OptimizeHuffman = false }, CancellationToken.None);

        await Assert.That(Decode(example, RgbComponents, true)).IsEquivalentTo(Decode(optimised, RgbComponents, true));
        await Assert.That(MaxSkiaDifference(example, RgbComponents)).IsLessThanOrEqualTo(SubsampledTolerance);
        await Assert.That(optimised.Length).IsLessThan(example.Length);
    }

    /// <summary>Restart markers do not change the decoded samples, in sequential and progressive files.</summary>
    /// <param name="progressive">Whether the file is progressive.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RestartIntervalsDoNotChangeTheSamples(bool progressive)
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);

        PdfJpegOptions options = new() { Quality = Quality, Progressive = progressive };
        var plain = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, options, CancellationToken.None);
        var restarts = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, options with { RestartInterval = RestartInterval }, CancellationToken.None);

        await Assert.That(MarkerOffset(restarts, 0xD0)).IsGreaterThan(0);
        await Assert.That(Decode(restarts, RgbComponents, true)).IsEquivalentTo(Decode(plain, RgbComponents, true));
        await Assert.That(MaxSkiaDifference(restarts, RgbComponents)).IsLessThanOrEqualTo(SubsampledTolerance);
    }

    /// <summary>Four-component pictures carry the Adobe marker and decode to the inverted ink samples.</summary>
    /// <param name="transform">Whether the file is YCCK instead of plain CMYK.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task CmykRoundTripsWithAdobeMarker(bool transform)
    {
        var pixels = JpegEncodeSamples.Patches(PdfJpegPixelFormat.Cmyk32, Width, Height);

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Cmyk32, Width, Height, new() { Quality = Quality, TransformCmyk = transform }, CancellationToken.None);

        await Assert.That(JpegMarkers.TryReadInfo(jpeg, out var info)).IsTrue();
        await Assert.That(info.Components).IsEqualTo(InkComponents);
        await Assert.That(info.AdobeTransform).IsEqualTo(transform ? YcckTransform : 0);
        var decoded = Decode(jpeg, InkComponents, info.UsesColorTransform(JpegInfo.NoColorTransform));
        var inverted = new byte[pixels.Length];
        for (var i = 0; i < inverted.Length; i++)
        {
            inverted[i] = (byte)(byte.MaxValue - pixels[i]);
        }

        await Assert.That(MeanError(decoded, inverted)).IsLessThan(PatchMeanError);
    }

    /// <summary>Skia reads the Adobe-style CMYK files as the matching RGB colours.</summary>
    /// <param name="transform">Whether the file is YCCK instead of plain CMYK.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task SkiaReadsCmykFiles(bool transform)
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Cmyk32, Width, Height);

        PdfJpegOptions options = new() { Quality = Quality, TransformCmyk = transform, Subsampling = PdfJpegSubsampling.Chroma444 };
        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Cmyk32, Width, Height, options, CancellationToken.None);

        using var bitmap = SKBitmap.Decode(jpeg);
        await Assert.That(bitmap).IsNotNull();
        await Assert.That(WorstInkDifference(bitmap, pixels)).IsLessThanOrEqualTo(SubsampledTolerance);
    }

    /// <summary>Every sampling arrangement the standard allows with whole-number ratios decodes, interleaved or not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnySamplingArrangementDecodes()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);
        foreach (var arrangement in Arrangements)
        {
            var jpeg = Encode(pixels, Request(arrangement));

            await Assert.That(MeanError(Decode(jpeg, RgbComponents, true), pixels)).IsLessThan(GoodMeanError);

            // Skia's libjpeg build refuses some of the unusual sampling factors; it only judges the files it reads.
            await Assert.That(MaxSkiaDifference(jpeg, RgbComponents)).IsLessThanOrEqualTo(SubsampledTolerance);
        }
    }

    /// <summary>The restart-interval option is clamped to what the DRI marker can hold, and a negative value means none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NegativeRestartIntervalMeansNone()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Gray8, Width, Height);

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Gray8, Width, Height, new() { RestartInterval = -RestartInterval }, CancellationToken.None);

        await Assert.That(MarkerOffset(jpeg, JpegMarkers.RestartInterval)).IsEqualTo(-1);
    }

    /// <summary>The async overloads give the same bytes as the synchronous ones, and the stream overload writes them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormsMatchTheSyncForm()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);
        var image = new PdfRasterImage(PdfRasterFormat.Rgb24, Width, Height, pixels);
        var expected = PdfJpegEncoder.Encode(image, PdfJpegOptions.Default, CancellationToken.None);

        var memory = await PdfJpegEncoder.EncodeAsync(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, PdfJpegOptions.Default, CancellationToken.None);
        await using var stream = new MemoryStream();
        await PdfJpegEncoder.EncodeAsync(image, PdfJpegOptions.Default, stream, CancellationToken.None);

        await Assert.That(memory).IsEquivalentTo(expected);
        await Assert.That(stream.ToArray()).IsEquivalentTo(expected);
    }

    /// <summary>A cancelled token stops the encode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsTheEncode()
    {
        var pixels = JpegEncodeSamples.Pixels(PdfJpegPixelFormat.Rgb24, Width, Height);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, Width, Height, PdfJpegOptions.Default, source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Bad arguments are refused with the matching exception.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadArgumentsAreRefused()
    {
        var pixels = new byte[Width * Height];

        await Assert.That(() => PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Gray8, 0, Height, PdfJpegOptions.Default, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Gray8, Width, Height + 1, PdfJpegOptions.Default, CancellationToken.None)).Throws<ArgumentException>();
        await Assert.That(() => PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.None, Width, Height, PdfJpegOptions.Default, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Gray8, Width, Height, null!, CancellationToken.None)).Throws<ArgumentNullException>();
    }

    /// <summary>A one-pixel picture encodes and decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnePixelPictureRoundTrips()
    {
        var pixels = new byte[] { 200, 100, 50 };

        var jpeg = PdfJpegEncoder.Encode(pixels, PdfJpegPixelFormat.Rgb24, 1, 1, new() { Quality = BestQuality, Subsampling = PdfJpegSubsampling.Chroma444 }, CancellationToken.None);

        await Assert.That(OneSample(jpeg, pixels)).IsLessThan(BestMeanError);
    }

    /// <summary>Decodes a one-pixel RGB file and finds its mean error.</summary>
    /// <param name="jpeg">The file.</param>
    /// <param name="expected">The source pixel.</param>
    /// <returns>The mean error.</returns>
    private static double OneSample(byte[] jpeg, byte[] expected)
    {
        var decoded = new byte[RgbComponents];
        return JpegDecoder.TryDecode(jpeg, true, decoded) ? MeanError(decoded, expected) : double.MaxValue;
    }

    /// <summary>Builds the request for a sampling arrangement of an RGB picture.</summary>
    /// <param name="arrangement">The arrangement.</param>
    /// <returns>The request.</returns>
    private static JpegEncodeRequest Request(SamplingCase arrangement) =>
        new(Width, Height, PdfJpegPixelFormat.Rgb24, new PdfJpegOptions { Quality = Quality }) { Sampling = arrangement.Sampling, NonInterleaved = !arrangement.Interleaved };

    /// <summary>Encodes a request through the internal entry point.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="request">The request.</param>
    /// <returns>The JPEG bytes.</returns>
    private static byte[] Encode(byte[] pixels, in JpegEncodeRequest request)
    {
        var output = new HyperPdfLibrary.Filters.PooledBuffer(0);
        try
        {
            JpegEncoder.Encode(pixels, request, ref output, new(), CancellationToken.None);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Finds the largest difference between a Skia decode of a CMYK file and the RGB that its ink amounts mean.</summary>
    /// <param name="bitmap">Skia's decode.</param>
    /// <param name="ink">The source ink amounts, four bytes a pixel.</param>
    /// <returns>The largest absolute channel difference.</returns>
    private static int WorstInkDifference(SKBitmap bitmap, byte[] ink)
    {
        var worst = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var pixel = ink.AsSpan(((y * Width) + x) * InkComponents, InkComponents);
                var color = bitmap.GetPixel(x, y);
                var key = byte.MaxValue - pixel[3];
                var expected = new[]
                {
                    (byte)((byte.MaxValue - pixel[0]) * key / byte.MaxValue),
                    (byte)((byte.MaxValue - pixel[1]) * key / byte.MaxValue),
                    (byte)((byte.MaxValue - pixel[2]) * key / byte.MaxValue),
                };
                worst = Math.Max(worst, Math.Abs(expected[0] - color.Red));
                worst = Math.Max(worst, Math.Abs(expected[1] - color.Green));
                worst = Math.Max(worst, Math.Abs(expected[2] - color.Blue));
            }
        }

        return worst;
    }

    /// <summary>Finds a marker in a file, ignoring the entropy-coded bytes where only restart markers can appear.</summary>
    /// <param name="jpeg">The file.</param>
    /// <param name="marker">The marker byte.</param>
    /// <returns>The offset of the 0xFF that starts the marker, or -1.</returns>
    private static int MarkerOffset(byte[] jpeg, byte marker)
    {
        for (var i = 0; i + 1 < jpeg.Length; i++)
        {
            if (jpeg[i] == JpegMarkers.MarkerPrefix && jpeg[i + 1] == marker)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Gets the components of a pixel format's JPEG.</summary>
    /// <param name="format">The format.</param>
    /// <returns>1, 3 or 4.</returns>
    private static int ComponentsOf(PdfJpegPixelFormat format) => format switch
    {
        PdfJpegPixelFormat.Gray8 => 1,
        PdfJpegPixelFormat.Cmyk32 => InkComponents,
        _ => RgbComponents,
    };

    /// <summary>Decodes with the library's decoder.</summary>
    /// <param name="jpeg">The JPEG bytes.</param>
    /// <param name="components">The components per pixel.</param>
    /// <param name="convert">Whether to convert YCbCr or YCCK.</param>
    /// <returns>The interleaved samples.</returns>
    private static byte[] Decode(byte[] jpeg, int components, bool convert)
    {
        var output = new byte[Width * Height * components];
        if (!JpegDecoder.TryDecode(jpeg, convert, output))
        {
            Assert.Fail("The library's decoder could not read the file.");
        }

        return output;
    }

    /// <summary>Finds the mean absolute difference of two sample arrays.</summary>
    /// <param name="decoded">The decoded samples.</param>
    /// <param name="expected">The expected samples, as many as the decoded ones or more.</param>
    /// <returns>The mean difference.</returns>
    private static double MeanError(byte[] decoded, byte[] expected)
    {
        long total = 0;
        for (var i = 0; i < decoded.Length; i++)
        {
            total += Math.Abs(decoded[i] - expected[i]);
        }

        return (double)total / decoded.Length;
    }

    /// <summary>Finds the largest per-channel difference between the library's decode and Skia's.</summary>
    /// <param name="jpeg">The JPEG bytes.</param>
    /// <param name="components">The components per pixel: 1 for gray, 3 for colour.</param>
    /// <returns>The largest absolute difference.</returns>
    private static int MaxSkiaDifference(byte[] jpeg, int components)
    {
        var decoded = Decode(jpeg, components, true);
        using var bitmap = SKBitmap.Decode(jpeg);
        var worst = 0;
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var sample = decoded.AsSpan(((y * Width) + x) * components, components);
                worst = Math.Max(worst, Math.Abs(sample[0] - color.Red));
                worst = Math.Max(worst, components == RgbComponents ? Math.Abs(sample[1] - color.Green) : 0);
                worst = Math.Max(worst, components == RgbComponents ? Math.Abs(sample[2] - color.Blue) : 0);
            }
        }

        return worst;
    }

    /// <summary>One sampling arrangement.</summary>
    /// <param name="Sampling">The sampling byte of each component.</param>
    /// <param name="Interleaved">Whether one scan holds every component.</param>
    private sealed record SamplingCase(byte[] Sampling, bool Interleaved);
}
