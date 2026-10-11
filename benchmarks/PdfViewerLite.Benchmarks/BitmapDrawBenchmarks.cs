// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using BenchmarkDotNet.Attributes;
using PdfViewerLite.Skia;
using PdfViewerLite.Skia.Bitmaps;
using PdfViewerLite.Skia.Rendering;
using SkiaSharp;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures drawing a page tile the way the canvas does: at its own size, stretched by a zoom step, and after new pixels.</summary>
public class BitmapDrawBenchmarks
{
    /// <summary>The tile edge in pixels, matching the canvas tile grid.</summary>
    private const int TileEdge = 512;

    /// <summary>The target surface edge in pixels.</summary>
    private const int SurfaceEdge = 1024;

    /// <summary>The zoom of one wheel notch, for a stretched tile.</summary>
    private const double WheelStep = 1.1;

    /// <summary>The page grey written into the tile.</summary>
    private const byte Grey = 0xE0;

    /// <summary>The whole tile.</summary>
    private static readonly Rect TileRect = new(0, 0, TileEdge, TileEdge);

    /// <summary>The tile stretched by one wheel notch.</summary>
    private static readonly Rect StretchedRect = new(0, 0, TileEdge * WheelStep, TileEdge * WheelStep);

    /// <summary>The target surface.</summary>
    private SKSurface _surface = null!;

    /// <summary>The retained drawing session.</summary>
    private DrawingContextImpl _context = null!;

    /// <summary>The tile bitmap.</summary>
    private WriteableBitmapImpl _tile = null!;

    /// <summary>Creates the surface and a filled tile.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _surface = SKSurface.Create(new SKImageInfo(SurfaceEdge, SurfaceEdge));
        _context = new(new DrawingContextCreateInfo { Surface = _surface, Dpi = SkiaPlatform.DefaultDpi });
        _context.RenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality };
        _tile = new(new PixelSize(TileEdge, TileEdge), SkiaPlatform.DefaultDpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
        Fill();
        _context.DrawBitmap(_tile, 1, TileRect, TileRect);
    }

    /// <summary>Releases the session and native resources.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
        _tile.Dispose();
        _surface.Dispose();
    }

    /// <summary>Draws a cached tile at its own size, as on every frame.</summary>
    [Benchmark(Baseline = true)]
    public void DrawOneToOne() => _context.DrawBitmap(_tile, 1, TileRect, TileRect);

    /// <summary>Draws a cached tile stretched by one wheel notch, as while a zoom settles.</summary>
    [Benchmark]
    public void DrawStretched() => _context.DrawBitmap(_tile, 1, TileRect, StretchedRect);

    /// <summary>Draws a cached tile stretched by one wheel notch with bilinear sampling.</summary>
    [Benchmark]
    public void DrawStretchedBilinear()
    {
        var options = _context.RenderOptions;
        _context.RenderOptions = options with { BitmapInterpolationMode = BitmapInterpolationMode.LowQuality };
        _context.DrawBitmap(_tile, 1, TileRect, StretchedRect);
        _context.RenderOptions = options;
    }

    /// <summary>Writes new pixels and draws them, as when a freshly rendered tile is first shown.</summary>
    [Benchmark]
    public void WriteThenDraw()
    {
        Fill();
        _context.DrawBitmap(_tile, 1, TileRect, TileRect);
    }

    /// <summary>Writes the tile's pixels through its framebuffer lock.</summary>
    private unsafe void Fill()
    {
        using var buffer = _tile.Lock();
        new Span<byte>((void*)buffer.Address, buffer.RowBytes * buffer.Size.Height).Fill(Grey);
    }
}
