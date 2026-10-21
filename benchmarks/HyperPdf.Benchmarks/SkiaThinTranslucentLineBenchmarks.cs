// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures a page of translucent lines thinner than a pixel, drawn with the page matrix unknown (body plus hairline in a layer) and
/// known (one pass).
/// </summary>
public class SkiaThinTranslucentLineBenchmarks
{
    /// <summary>The thin lines on the page.</summary>
    private const int Lines = 2000;

    /// <summary>The distance between lines in points.</summary>
    private const int Spacing = 2;

    /// <summary>The length of a line in points.</summary>
    private const int LineLength = 500;

    /// <summary>The bottom of the lowest line, so every line falls inside the top-left tile.</summary>
    private const int Floor = 300;

    /// <summary>The lines in each column of lines.</summary>
    private const int Rows = 240;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The document kept for the warm draw.</summary>
    private PdfDocument? _document;

    /// <summary>The renderer kept for the warm draw.</summary>
    private PdfPageRenderer? _renderer;

    /// <summary>Gets or sets whether the page matrix is known when the page is recorded: Unknown or Known.</summary>
    [Params("Unknown", "Known")]
    public string PageMatrix { get; set; } = "Unknown";

    /// <summary>Builds the document and records the page for the warm draw.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder("/Half gs 0 G 0.4 w ");
        for (var line = 0; line < Lines; line++)
        {
            var y = Floor + ((line % Rows) * Spacing);
            var x = line / Rows;
            _ = content.Append(CultureInfo.InvariantCulture, $"{x} {y} m {x + LineLength} {y} l S ");
        }

        _pdf = SkiaRecordingPage.Create(content.ToString(), "/ExtGState << /Half << /CA 0.5 >> >>", false);
        _document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        _renderer = new(_document, Options());
        _ = SkiaRecordingPage.Draw(_renderer, _pixels);
    }

    /// <summary>Releases the warm renderer and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer?.Dispose();
        _document?.Dispose();
    }

    /// <summary>Opens the document and records and draws the page.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RecordPageOfThinTranslucentLines()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document, Options());
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }

    /// <summary>Draws the page again from its recorded picture.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool ReplayPageOfThinTranslucentLines() => SkiaRecordingPage.Draw(_renderer!, _pixels);

    /// <summary>Gets the render options for the current parameter. A stroke adjustment that follows an absent /SA makes the matrix known without fitting.</summary>
    /// <returns>The options.</returns>
    private PdfRenderOptions Options() => PdfRenderOptions.Default with
    {
        Quality = PageMatrix == "Known" ? PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Document } : PdfRenderQuality.Default,
    };
}
