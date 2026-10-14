// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>Compares the managed JPEG encoder with SkiaSharp (libjpeg-turbo) on a 1000x1000 RGB picture at quality 90.</summary>
public class HyperPdfJpegEncodeBenchmarks
{
    /// <summary>The image side in pixels.</summary>
    private const int Side = 1000;

    /// <summary>The largest value of x plus y.</summary>
    private const int DiagonalSpan = Side + Side;

    /// <summary>The JPEG quality.</summary>
    private const int Quality = 90;

    /// <summary>The bytes in an RGB pixel.</summary>
    private const int RgbBytes = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the third channel in a pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>The multiplier that scrambles pixel positions into texture.</summary>
    private const uint NoiseMultiplier = 2_654_435_761;

    /// <summary>The bits of noise added to each channel.</summary>
    private const int NoiseMask = 0x0F;

    /// <summary>The shift that takes the high bits of the scrambled position.</summary>
    private const int NoiseShift = 24;

    /// <summary>The encodes of each variant made before measuring.</summary>
    private const int WarmRounds = 3;

    /// <summary>The starting capacity of the output buffer.</summary>
    private const int OutputCapacity = 1 << 20;

    /// <summary>The options for gray and CMYK pictures, with the automatic subsampling.</summary>
    private static readonly PdfJpegOptions DefaultOptions = new() { Quality = Quality };

    /// <summary>The options for 4:2:0 colour with optimised tables.</summary>
    private static readonly PdfJpegOptions Chroma420Options = new() { Quality = Quality, Subsampling = PdfJpegSubsampling.Chroma420 };

    /// <summary>The options for 4:4:4 colour with optimised tables.</summary>
    private static readonly PdfJpegOptions Chroma444Options = new() { Quality = Quality, Subsampling = PdfJpegSubsampling.Chroma444 };

    /// <summary>The options for 4:2:0 colour with the example tables.</summary>
    private static readonly PdfJpegOptions ExampleTableOptions = new() { Quality = Quality, Subsampling = PdfJpegSubsampling.Chroma420, OptimizeHuffman = false };

    /// <summary>The options for 4:2:0 progressive colour.</summary>
    private static readonly PdfJpegOptions ProgressiveOptions = new() { Quality = Quality, Subsampling = PdfJpegSubsampling.Chroma420, Progressive = true };

    /// <summary>The scratch cache a document owns, shared by every encode as the optimiser shares it.</summary>
    private readonly ScratchPools _scratch = new();

    /// <summary>The picture as BGRA, for Skia.</summary>
    private SKBitmap _bitmap = null!;

    /// <summary>The picture as Skia pixels.</summary>
    private SKPixmap _pixmap = null!;

    /// <summary>The picture as RGB.</summary>
    private byte[] _rgb = [];

    /// <summary>The picture as gray.</summary>
    private byte[] _gray = [];

    /// <summary>The picture as CMYK.</summary>
    private byte[] _cmyk = [];

    /// <summary>Builds the test picture in each layout.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _bitmap = new(new SKImageInfo(Side, Side, SKColorType.Bgra8888, SKAlphaType.Opaque));
        _rgb = new byte[Side * Side * RgbBytes];
        _gray = new byte[Side * Side];
        _cmyk = new byte[Side * Side * BgraBytes];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var noise = (int)(((uint)((y * Side) + x) * NoiseMultiplier) >> NoiseShift) & NoiseMask;
                var red = (byte)((x * byte.MaxValue / Side) + noise);
                var green = (byte)((y * byte.MaxValue / Side) + noise);
                var blue = (byte)(((x + y) * byte.MaxValue / DiagonalSpan) + noise);
                _bitmap.SetPixel(x, y, new(red, green, blue));
                var index = (y * Side) + x;
                _rgb[index * RgbBytes] = red;
                _rgb[(index * RgbBytes) + 1] = green;
                _rgb[(index * RgbBytes) + BlueOffset] = blue;
                _gray[index] = green;
                _cmyk[index * BgraBytes] = (byte)(byte.MaxValue - red);
                _cmyk[(index * BgraBytes) + 1] = (byte)(byte.MaxValue - green);
                _cmyk[(index * BgraBytes) + BlueOffset] = (byte)(byte.MaxValue - blue);
            }
        }

        _pixmap = _bitmap.PeekPixels();

        // Fill the scratch cache and the shared array pool for every variant, so a measured encode starts warm.
        for (var round = 0; round < WarmRounds; round++)
        {
            _ = Managed420() + Managed444() + ManagedExampleTables() + ManagedProgressive() + ManagedGray() + ManagedCmyk();
        }
    }

    /// <summary>Releases the Skia objects.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pixmap.Dispose();
        _bitmap.Dispose();
    }

    /// <summary>Encodes with SkiaSharp, 4:2:0.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark(Baseline = true)]
    public int Skia()
    {
        using var data = _pixmap.Encode(new SKJpegEncoderOptions(Quality, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore));
        return data is null ? 0 : (int)data.Size;
    }

    /// <summary>Encodes RGB with the managed encoder, 4:2:0, optimised tables.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark]
    public int Managed420() => Encode(_rgb, PdfJpegPixelFormat.Rgb24, Chroma420Options);

    /// <summary>Encodes RGB with the managed encoder, 4:4:4, optimised tables.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark]
    public int Managed444() => Encode(_rgb, PdfJpegPixelFormat.Rgb24, Chroma444Options);

    /// <summary>Encodes RGB with the managed encoder, 4:2:0, the example tables of Annex K.3 in a single pass.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark]
    public int ManagedExampleTables() => Encode(_rgb, PdfJpegPixelFormat.Rgb24, ExampleTableOptions);

    /// <summary>Encodes RGB with the managed encoder as a progressive JPEG.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark]
    public int ManagedProgressive() => Encode(_rgb, PdfJpegPixelFormat.Rgb24, ProgressiveOptions);

    /// <summary>Encodes gray samples with the managed encoder.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark]
    public int ManagedGray() => Encode(_gray, PdfJpegPixelFormat.Gray8, DefaultOptions);

    /// <summary>Encodes CMYK samples with the managed encoder as YCCK.</summary>
    /// <returns>The encoded size.</returns>
    [Benchmark]
    public int ManagedCmyk() => Encode(_cmyk, PdfJpegPixelFormat.Cmyk32, DefaultOptions);

    /// <summary>Encodes into a pooled buffer, so the measurement does not include the copy into a result array.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="format">Their layout.</param>
    /// <param name="options">The coding options.</param>
    /// <returns>The encoded size.</returns>
    private int Encode(byte[] pixels, PdfJpegPixelFormat format, PdfJpegOptions options)
    {
        var output = new PooledBuffer(OutputCapacity);
        try
        {
            JpegEncoder.Encode(pixels, new(Side, Side, format, options), ref output, _scratch, default);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
