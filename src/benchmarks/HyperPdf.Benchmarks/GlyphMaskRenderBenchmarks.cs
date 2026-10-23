// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures warm tile rasterization of a text page drawn from cached glyph masks with each small text choice.</summary>
public class GlyphMaskRenderBenchmarks
{
    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The scroll distance between two visible tiles.</summary>
    private const int ScrollOffset = 128;

    /// <summary>The tile at the page origin.</summary>
    private readonly PdfTileRequest _origin = new(0, 1, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The tile after a viewport scroll.</summary>
    private readonly PdfTileRequest _scroll = new(0, 1, 0, ScrollOffset, ScrollOffset, PdfRenderFlags.None);

    /// <summary>The reused caller-owned CPU pixel buffer.</summary>
    private readonly byte[] _pixels = new byte[TileEdge * TileEdge * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The open document retained for warm replay.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer retaining recorded page pictures.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Gets or sets which small text choice is measured.</summary>
    [Params("Outlines", "Masks", "HintedContrast", "Lcd")]
    public string Choice { get; set; } = string.Empty;

    /// <summary>Opens and records the text page with the choice, so the glyph masks are made before measuring.</summary>
    /// <exception cref="InvalidOperationException">The page did not render.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDrawing.Open(RenderSamplePages.CreateTextPage(), null, _drawingServices);
        _renderer = new(_document, PdfRenderOptions.Default with { Quality = QualityOf(Choice) });
        if (!WarmReplay() || !WarmScrollTile())
        {
            throw new InvalidOperationException($"The text page did not render with {Choice}.");
        }
    }

    /// <summary>Disposes the retained page recording and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Measures rasterization of the retained page recording.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool WarmReplay() => _renderer.Render(_origin, Target());

    /// <summary>Measures a shifted visible tile using the retained page recording.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool WarmScrollTile() => _renderer.Render(_scroll, Target());

    /// <summary>Gets the quality settings of a choice.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns>The settings.</returns>
    private static PdfRenderQuality QualityOf(string choice) => choice switch
    {
        "Masks" => PdfRenderQuality.Default with { GlyphMasks = true },
        "HintedContrast" => PdfRenderQuality.Default with { TextHinting = PdfTextHinting.Slight, TextContrast = 1 },
        "Lcd" => PdfRenderQuality.Default with { TextEdges = PdfTextEdges.Lcd },
        _ => PdfRenderQuality.Default,
    };

    /// <summary>Gets the reused caller-owned target.</summary>
    /// <returns>The target.</returns>
    private PdfTileTarget Target() => new(_pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel);
}
