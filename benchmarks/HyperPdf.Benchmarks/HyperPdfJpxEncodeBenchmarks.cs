// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Graphics.Images.Jpx.Encoding;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Encodes the top left square of a scanned page with the managed JPEG 2000 encoder: lossless (5/3 wavelet and reversible
/// colour transform), lossy (9/7 wavelet and irreversible colour transform) at quality 80, and lossless with tiles and layers.
/// The page is decoded from the 5/3 fixture the decoder benchmarks use.
/// </summary>
public class HyperPdfJpxEncodeBenchmarks
{
    /// <summary>The side of the square encoded.</summary>
    private const int Side = 1024;

    /// <summary>The bits per sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The components of the image.</summary>
    private const int Rgb = 3;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of red in a BGRA pixel, and of blue in an RGB pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of blue in an RGB pixel.</summary>
    private const int BlueOffset = 2;

    /// <summary>The tile side of the tiled options.</summary>
    private const int TileSide = 512;

    /// <summary>The layers of the layered options.</summary>
    private const int Layers = 3;

    /// <summary>The quality of the lossy options.</summary>
    private const int Quality = 80;

    /// <summary>The caller-owned encoder scratch.</summary>
    private readonly ScratchPools _scratchPools = new();

    /// <summary>The square to encode.</summary>
    private PdfJpxImage _image = null!;

    /// <summary>Options for a lossless file.</summary>
    private PdfJpxOptions _lossless = null!;

    /// <summary>Options for a lossy file.</summary>
    private PdfJpxOptions _lossy = null!;

    /// <summary>Options for a lossless file in tiles and layers.</summary>
    private PdfJpxOptions _tiled = null!;

    /// <summary>Decodes the page and cuts out the square.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var data = Convert.FromBase64String(HyperPdfJpxPages.Reversible);
        var file = JpxFileFormat.Read(data)!;
        var geometry = JpxCodestream.Read(data.AsSpan(file.Codestream.Offset, file.Codestream.Length))!.Geometry;
        var header = new ImageHeader(geometry.Image.Width, geometry.Image.Height, SampleBits, false, false, PdfColorSpace.DeviceRgb, null, null);
        var page = JpxImageDecoder.Decode(header, data, 0)!;
        var samples = new byte[Side * Side * Rgb];
        for (var y = 0; y < Side; y++)
        {
            for (var x = 0; x < Side; x++)
            {
                var source = ((y * page.Width) + x) * BgraBytes;
                var target = ((y * Side) + x) * Rgb;
                samples[target] = page.Pixels[source + RedOffset];
                samples[target + 1] = page.Pixels[source + 1];
                samples[target + BlueOffset] = page.Pixels[source];
            }
        }

        _image = new(Side, Side, Rgb, SampleBits, samples);
        _lossless = PdfJpxOptions.Lossless;
        _lossy = new() { Mode = PdfJpxMode.Lossy, Quality = Quality };
        _tiled = new() { TileWidth = TileSide, TileHeight = TileSide, Layers = Layers };
    }

    /// <summary>Encodes losslessly.</summary>
    /// <returns>The file.</returns>
    [Benchmark(Baseline = true)]
    public byte[] Lossless() => PdfJpxEncoder.Encode(_image, _lossless, default);

    /// <summary>Encodes lossily at quality 80.</summary>
    /// <returns>The file.</returns>
    [Benchmark]
    public byte[] Lossy() => PdfJpxEncoder.Encode(_image, _lossy, default);

    /// <summary>Encodes losslessly in four tiles and three layers.</summary>
    /// <returns>The file.</returns>
    [Benchmark]
    public byte[] LosslessTiledLayered() => PdfJpxEncoder.Encode(_image, _tiled, default);

    /// <summary>Encodes into a pooled output buffer with caller-owned scratch.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int CallerOwnedLossless()
    {
        var output = default(PooledBuffer);
        try
        {
            JpxEncoder.Encode(_image, _image.Samples.Span, _lossless, ref output, _scratchPools, CancellationToken.None);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Releases the caller's encoding storage.</summary>
    [GlobalCleanup]
    public void Cleanup() => _scratchPools.Close();
}
