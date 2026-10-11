// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Media;
using BenchmarkDotNet.Attributes;
using PdfViewerLite.Skia;
using PdfViewerLite.Skia.Geometry;
using PdfViewerLite.Skia.Rendering;
using SkiaSharp;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures equivalent path and rectangle fills in a retained drawing session.</summary>
public class RectangleBenchmarks
{
    /// <summary>The rectangle side in pixels.</summary>
    private const int RectangleSize = 32;

    /// <summary>The rounded corner radius in pixels.</summary>
    private const int CornerRadius = 4;

    /// <summary>The target surface.</summary>
    private SKSurface _surface = null!;

    /// <summary>The retained drawing session.</summary>
    private DrawingContextImpl _context = null!;

    /// <summary>The equivalent path geometry.</summary>
    private StreamGeometryImpl _path = null!;

    /// <summary>The rectangle geometry.</summary>
    private RectangleGeometryImpl _rectangle = null!;

    /// <summary>Creates the surface and geometry before measuring draw calls.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _surface = SKSurface.Create(new SKImageInfo(RectangleSize, RectangleSize));
        _context = new(new DrawingContextCreateInfo { Surface = _surface, Dpi = SkiaPlatform.DefaultDpi });
        using var builder = new SKPathBuilder();
        builder.AddRect(new(0, 0, RectangleSize, RectangleSize));
        var path = builder.Detach();
        _path = new(path, path);
        _rectangle = new(new(0, 0, RectangleSize, RectangleSize));
    }

    /// <summary>Releases the session and native resources.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _path.Dispose();
        _context.Dispose();
        _surface.Dispose();
    }

    /// <summary>Draws the rectangle through the general geometry path.</summary>
    [Benchmark(Baseline = true)]
    public void PathFill() => _context.DrawGeometry(Brushes.Blue, null, _path);

    /// <summary>Draws the rectangle using the solid fill shortcut.</summary>
    [Benchmark]
    public void SolidRectangle() => _context.DrawRectangle(Brushes.Blue, null, new(new Rect(0, 0, RectangleSize, RectangleSize)));

    /// <summary>Draws rectangle geometry using its shape shortcut.</summary>
    [Benchmark]
    public void RectangleGeometry() => _context.DrawGeometry(Brushes.Blue, null, _rectangle);

    /// <summary>Draws a rectangle with rounded corners using retained native storage.</summary>
    [Benchmark]
    public void RoundedRectangle() => _context.DrawRectangle(Brushes.Blue, null, new(new Rect(0, 0, RectangleSize, RectangleSize), CornerRadius));
}
