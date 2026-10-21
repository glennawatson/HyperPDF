// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures recording and drawing a tiling pattern page from scratch at low and normal zoom, which includes composing each pattern period.</summary>
public class TilingPatternPageRenderBenchmarks
{
    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The caller-owned pixel buffer.</summary>
    private readonly byte[] _pixels = new byte[TileEdge * TileEdge * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The pattern page's bytes.</summary>
    private byte[] _bytes = [];

    /// <summary>Gets or sets the display scale: 0.25 for 25% zoom and 1 for 100%.</summary>
    [Params(0.25F, 1F)]
    public float Scale { get; set; }

    /// <summary>Makes the pattern page.</summary>
    [GlobalSetup]
    public void Setup() => _bytes = RenderSamplePages.CreatePatternPage();

    /// <summary>Opens the pattern page, so no pattern period is cached, and draws a tile of it.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool OpenAndRenderPatternPageTile()
    {
        using var document = BenchmarkDrawing.Open(_bytes, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return renderer.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(_pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel));
    }
}
