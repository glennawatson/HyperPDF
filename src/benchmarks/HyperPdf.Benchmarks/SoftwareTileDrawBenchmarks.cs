// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if HYPERPDF_VIEWMODELS_PROJECT
using BenchmarkDotNet.Attributes;
using HyperPdf.Rendering.Skia;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures drawing a software tile the way the Avalonia head's tile batch does: the cached image over the tile's pixels drawn
/// into a Skia canvas. The compositor's own cost is not part of this measurement.
/// </summary>
public class SoftwareTileDrawBenchmarks
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 512;

    /// <summary>The destination side in pixels, as when a tile is scaled for a preview.</summary>
    private const int ScaledSide = 384;

    /// <summary>Sampling for scaled previews.</summary>
    private static readonly SKSamplingOptions Smooth = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>Sampling for scaled software previews, which skips the mipmaps a raster canvas would rebuild on every draw.</summary>
    private static readonly SKSamplingOptions Linear = new(SKFilterMode.Linear, SKMipmapMode.None);

    /// <summary>Sampling for pixel-aligned tiles.</summary>
    private static readonly SKSamplingOptions Sharp = new(SKFilterMode.Nearest, SKMipmapMode.None);

    /// <summary>The whole tile.</summary>
    private static readonly SKRect Whole = SKRect.Create(TileSide, TileSide);

    /// <summary>The scaled destination.</summary>
    private static readonly SKRect Scaled = SKRect.Create(ScaledSide, ScaledSide);

    /// <summary>The software tile.</summary>
    private SkiaBitmapRenderSurface _surface = null!;

    /// <summary>The bitmap the canvas draws into.</summary>
    private SKBitmap _target = null!;

    /// <summary>The canvas.</summary>
    private SKCanvas _canvas = null!;

    /// <summary>Creates the tile and the canvas.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _surface = new(TileSide, TileSide);
        _ = _surface.Image;
        _target = new(TileSide, TileSide);
        _canvas = new(_target);
    }

    /// <summary>Frees the canvas and the tile.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _canvas.Dispose();
        _target.Dispose();
        _surface.Dispose();
    }

    /// <summary>Draws the tile at its own size with nearest sampling.</summary>
    /// <returns>The tile's width.</returns>
    [Benchmark]
    public int DrawSharp()
    {
        _canvas.DrawImage(_surface.Image, Whole, Whole, Sharp);
        return TileSide;
    }

    /// <summary>Draws the tile scaled down with linear sampling and no mipmaps.</summary>
    /// <returns>The destination width.</returns>
    [Benchmark]
    public int DrawScaledLinear()
    {
        _canvas.DrawImage(_surface.Image, Whole, Scaled, Linear);
        return ScaledSide;
    }

    /// <summary>Draws the tile scaled down with smooth sampling.</summary>
    /// <returns>The destination width.</returns>
    [Benchmark]
    public int DrawScaledSmooth()
    {
        _canvas.DrawImage(_surface.Image, Whole, Scaled, Smooth);
        return ScaledSide;
    }
}
#endif
