// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures recording a text page into a picture, and drawing it, with each small text choice, once the glyph masks are made. Each call records the page again.</summary>
public class GlyphMaskRecordBenchmarks
{
    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The tile at the page origin.</summary>
    private readonly PdfTileRequest _origin = new(0, 1, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The reused caller-owned CPU pixel buffer.</summary>
    private readonly byte[] _pixels = new byte[TileEdge * TileEdge * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations; they hold the glyph masks.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The open document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The render options of the choice.</summary>
    private PdfRenderOptions _options = PdfRenderOptions.Default;

    /// <summary>Gets or sets which small text choice is measured.</summary>
    [Params("Outlines", "Masks", "Lcd")]
    public string Choice { get; set; } = string.Empty;

    /// <summary>Opens the text page and records it once with the choice, so the glyph masks are made before measuring.</summary>
    /// <exception cref="InvalidOperationException">The page did not render.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDrawing.Open(RenderSamplePages.CreateTextPage(), null, _drawingServices);
        _options = PdfRenderOptions.Default with { Quality = QualityOf(Choice) };
        if (!RecordAndDrawTextPage())
        {
            throw new InvalidOperationException($"The text page did not render with {Choice}.");
        }
    }

    /// <summary>Disposes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Measures recording the text page into a new picture and drawing one tile from it.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool RecordAndDrawTextPage()
    {
        using var renderer = new PdfPageRenderer(_document, _options);
        return renderer.Render(_origin, new(_pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel));
    }

    /// <summary>Gets the quality settings of a choice.</summary>
    /// <param name="choice">The choice.</param>
    /// <returns>The settings.</returns>
    private static PdfRenderQuality QualityOf(string choice) => choice switch
    {
        "Masks" => PdfRenderQuality.Default with { GlyphMasks = true },
        "Lcd" => PdfRenderQuality.Default with { GlyphMasks = true, TextEdges = PdfTextEdges.Lcd },
        _ => PdfRenderQuality.Default,
    };
}
