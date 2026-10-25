// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using BenchmarkDotNet.Attributes;
using HyperPdf.Skia;
using HyperPdf.Skia.Fonts;
using HyperPdf.Skia.Rendering;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>Measures text shaping and drawing of a retained glyph run.</summary>
public class GlyphRunBenchmarks
{
    /// <summary>The text size in device-independent pixels.</summary>
    private const int FontSize = 16;

    /// <summary>The surface width.</summary>
    private const int SurfaceWidth = 512;

    /// <summary>The surface height.</summary>
    private const int SurfaceHeight = 64;

    /// <summary>The text shared by all cases.</summary>
    private const string Text = "Hyper PDF Viewer: office, AV, search and annotation";

    /// <summary>The text shaper.</summary>
    private readonly HarfBuzzTextShaper _shaper = new();

    /// <summary>The platform glyph typeface.</summary>
    private GlyphTypeface _typeface = null!;

    /// <summary>The shaped and retained glyph run.</summary>
    private GlyphRunImpl _run = null!;

    /// <summary>The drawing surface.</summary>
    private SKSurface _surface = null!;

    /// <summary>The retained drawing session.</summary>
    private DrawingContextImpl _context = null!;

    /// <summary>The shaping options.</summary>
    private TextShaperOptions _options;

    /// <summary>Creates and warms the glyph run before measuring repeated draws.</summary>
    [GlobalSetup]
    public void Setup()
    {
        SkiaPlatform.InitializeTextShaping();
        _typeface = new(new SkiaTypeface(SKTypeface.FromFamilyName("DejaVu Sans"), FontSimulations.None));
        _options = new(_typeface, FontSize);
        using var shaped = _shaper.ShapeText(Text.AsMemory(), _options);
        _run = new(_typeface, FontSize, shaped, new(0, FontSize));
        _surface = SKSurface.Create(new SKImageInfo(SurfaceWidth, SurfaceHeight));
        _context = new(new DrawingContextCreateInfo { Surface = _surface, Dpi = SkiaPlatform.DefaultDpi });
        _context.DrawGlyphRun(Brushes.Black, _run);
    }

    /// <summary>Releases the native text and drawing resources.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _run.Dispose();
        _context.Dispose();
        _surface.Dispose();
        _typeface.Dispose();
        _shaper.Dispose();
    }

    /// <summary>Shapes the text using the cached HarfBuzz font and thread buffer.</summary>
    [Benchmark]
    public void ShapeText()
    {
        using var shaped = _shaper.ShapeText(Text.AsMemory(), _options);
    }

    /// <summary>Draws the retained text blob using the session paint.</summary>
    [Benchmark]
    public void DrawCachedGlyphRun() => _context.DrawGlyphRun(Brushes.Black, _run);
}
