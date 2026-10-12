// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>Compares the managed JPEG decoder with SkiaSharp (libjpeg-turbo) on a 1000x1000 RGB JPEG.</summary>
public class HyperPdfJpegBenchmarks
{
    /// <summary>The image side in pixels.</summary>
    private const int Side = 1000;

    /// <summary>The largest value of x plus y.</summary>
    private const int DiagonalSpan = Side + Side;

    /// <summary>The JPEG quality.</summary>
    private const int Quality = 90;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The components of the managed decoder's output.</summary>
    private const int RgbComponents = 3;

    /// <summary>The multiplier that scrambles pixel positions into texture.</summary>
    private const uint NoiseMultiplier = 2_654_435_761;

    /// <summary>The bits of noise added to each channel.</summary>
    private const int NoiseMask = 0x0F;

    /// <summary>The shift that takes the high bits of the scrambled position.</summary>
    private const int NoiseShift = 24;

    /// <summary>The JPEG bytes.</summary>
    private byte[] _jpeg = [];

    /// <summary>The JPEG as Skia data.</summary>
    private SKData _data = null!;

    /// <summary>The Skia output buffer.</summary>
    private byte[] _pixels = [];

    /// <summary>The managed decoder's output buffer.</summary>
    private byte[] _samples = [];

    /// <summary>Encodes the test image and allocates the output buffers.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Side, Side, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var noise = (int)(((uint)((y * Side) + x) * NoiseMultiplier) >> NoiseShift) & NoiseMask;
                bitmap.SetPixel(x, y, new((byte)((x * byte.MaxValue / Side) + noise), (byte)((y * byte.MaxValue / Side) + noise), (byte)(((x + y) * byte.MaxValue / DiagonalSpan) + noise)));
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, Quality);
        _jpeg = encoded.ToArray();
        _data = SKData.CreateCopy(_jpeg);
        _pixels = new byte[Side * Side * BytesPerPixel];
        _samples = new byte[Side * Side * RgbComponents];
    }

    /// <summary>Releases the Skia data.</summary>
    [GlobalCleanup]
    public void Cleanup() => _data.Dispose();

    /// <summary>Decodes with SkiaSharp into a reused buffer.</summary>
    /// <returns>The codec result.</returns>
    [Benchmark(Baseline = true)]
    public SKCodecResult Skia()
    {
        using var codec = SKCodec.Create(_data);
        return codec.GetPixels(new(Side, Side, SKColorType.Bgra8888, SKAlphaType.Opaque), _pixels);
    }

    /// <summary>Checks for the end-of-image marker, which every image decode now does to report a truncated JPEG.</summary>
    /// <returns><see langword="true"/> when the JPEG ends with its marker.</returns>
    [Benchmark]
    public bool EndMarkerCheck() => JpegMarkers.EndsWithEndOfImage(_jpeg);

    /// <summary>Decodes with the managed decoder, converting YCbCr to RGB, into a reused buffer.</summary>
    /// <returns><see langword="true"/> when the JPEG decoded.</returns>
    [Benchmark]
    public bool Managed() => JpegDecoder.TryDecode(_jpeg, true, _samples);
}
