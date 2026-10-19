// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdf.Benchmarks;

/// <summary>Measures making one glyph mask, which a page pays once for each glyph, size and position it draws.</summary>
public class GlyphMaskCreateBenchmarks
{
    /// <summary>The side of the glyph in device pixels.</summary>
    private const float GlyphSize = 10;

    /// <summary>The middle of the unit square the round glyph is drawn in.</summary>
    private const float Middle = 0.5F;

    /// <summary>The power of two that gives the glyph's scale about 15 bits.</summary>
    private const int Shift = 12;

    /// <summary>The matrix entry of the glyph's scale at that shift.</summary>
    private const int Scale = (int)GlyphSize << Shift;

    /// <summary>How far down its pixel the glyph's origin is, in quarter pixels.</summary>
    private const byte SubY = 2;

    /// <summary>The paint masks are made with, reused for every mask as the painter does.</summary>
    private readonly SKPaint _paint = new();

    /// <summary>The outline of the test glyph.</summary>
    private SKPath _path = null!;

    /// <summary>Gets or sets how the mask's edges are made: Smooth or Lcd.</summary>
    [Params("Smooth", "Lcd")]
    public string Edges { get; set; } = string.Empty;

    /// <summary>Makes the glyph outline.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var builder = new SKPathBuilder();
        builder.AddCircle(Middle, Middle, Middle);
        _path = builder.Detach();
    }

    /// <summary>Releases the outline.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _path.Dispose();
        _paint.Dispose();
    }

    /// <summary>Makes the mask of one glyph.</summary>
    /// <returns>The mask's memory in bytes.</returns>
    [Benchmark]
    public long MakeMask()
    {
        var edges = Edges == "Lcd" ? GlyphMaskEdges.Lcd : GlyphMaskEdges.Smooth;
        var key = new GlyphMaskKey(this, 1, Scale, 0, 0, Scale, Shift, 1, SubY, edges, 0, 0);
        _ = GlyphMaskRaster.TryCreate(new OutlineMaskSource(_path), key, _paint, out var mask);
        var bytes = mask?.Bytes ?? 0;
        mask?.Image.Dispose();
        mask?.Cut?.Dispose();
        return bytes;
    }
}
