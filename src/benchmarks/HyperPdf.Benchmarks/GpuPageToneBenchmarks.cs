// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Rendering;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>Measures applying a non-identity page tone to one rendered tile through the shader tone pass, on a raster surface.</summary>
public class GpuPageToneBenchmarks
{
    /// <summary>The tile edge in pixels, matching the canvas tile grid.</summary>
    private const int TileEdge = 512;

    /// <summary>The bytes per premultiplied BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The number of tiles toned at once to measure render thread contention.</summary>
    private const int ParallelTiles = 8;

    /// <summary>The page grey written into the tile.</summary>
    private const byte Grey = 0xE0;

    /// <summary>The packed paper colour of the tone.</summary>
    private const uint PaperColor = 0x2A2826U;

    /// <summary>The packed ink colour of the tone.</summary>
    private const uint InkColor = 0xD2CDC5U;

    /// <summary>The tone pass under test.</summary>
    private GpuPageTone _tones = null!;

    /// <summary>The page tone, which is not the identity.</summary>
    private PageTone _tone = null!;

    /// <summary>The untoned tile.</summary>
    private SKImage _tile = null!;

    /// <summary>The tile layout.</summary>
    private SKImageInfo _info;

    /// <summary>Creates a tile and warms the tone filter cache.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _tones = new();
        _tone = new(PaperColor, InkColor);
        _info = new(TileEdge, TileEdge, SKColorType.Bgra8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
        var pixels = new byte[TileEdge * TileEdge * BytesPerPixel];
        Array.Fill(pixels, Grey);
        for (var alpha = BytesPerPixel - 1; alpha < pixels.Length; alpha += BytesPerPixel)
        {
            pixels[alpha] = byte.MaxValue;
        }

        _tile = SKImage.FromPixelCopy(_info, pixels, TileEdge * BytesPerPixel);
        using var warm = _tones.TryApply(_tile, _info, _tone, null);
    }

    /// <summary>Releases the native resources.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _tile.Dispose();
        _tones.Dispose();
    }

    /// <summary>Tones one tile on the calling thread.</summary>
    /// <returns>The toned image size.</returns>
    [Benchmark(Baseline = true)]
    public int ApplyToneToOneTile()
    {
        using var image = _tones.TryApply(_tile, _info, _tone, null);
        return image?.Width ?? 0;
    }

    /// <summary>Tones several tiles at once, as render threads do while scrolling.</summary>
    /// <returns>The number of tiles toned.</returns>
    [Benchmark]
    public int ApplyToneToTilesInParallel()
    {
        var toned = 0;
        _ = Parallel.For(0, ParallelTiles, _ =>
        {
            using var image = _tones.TryApply(_tile, _info, _tone, null);
            if (image is not null)
            {
                _ = Interlocked.Increment(ref toned);
            }
        });

        return toned;
    }
}
