// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures a page of thin ruled lines and table boxes drawn as the page says, fitted to whole pixels (stroke adjustment,
/// ISO 32000-2 10.7.5), and widened to a thinnest line.
/// </summary>
public class HyperPdfStrokeFitBenchmarks
{
    /// <summary>The tile size in pixels.</summary>
    private const int Tile = 512;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The ruled lines and boxes on the page.</summary>
    private const int Rules = 200;

    /// <summary>The distance between rules in points.</summary>
    private const int Spacing = 3;

    /// <summary>The length of a rule in points.</summary>
    private const int RuleLength = 500;

    /// <summary>The left margin in points.</summary>
    private const int Margin = 36;

    /// <summary>The thin line width in points, under a pixel at scale one.</summary>
    private const string ThinWidth = "0.4";

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[Tile * Tile * BytesPerPixel];

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The document kept for the warm draw.</summary>
    private PdfDocument? _document;

    /// <summary>The renderer kept for the warm draw.</summary>
    private PdfPageRenderer? _renderer;

    /// <summary>Gets or sets how lines are drawn: Page, Adjusted or Thinnest.</summary>
    [Params("Page", "Adjusted", "Thinnest")]
    public string Mode { get; set; } = "Page";

    /// <summary>Builds the document and records the page for the warm draw.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdf = Create();
        _document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        _renderer = new(_document, Options());
        _ = Draw(_renderer);
    }

    /// <summary>Releases the warm renderer and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer?.Dispose();
        _document?.Dispose();
    }

    /// <summary>Opens the document and draws the page for the first time.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool ColdDraw()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document, Options());
        return Draw(renderer);
    }

    /// <summary>Draws the page again from its recorded pictures.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool WarmDraw() => Draw(_renderer!);

    /// <summary>Builds a one page document of thin lines and boxes.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create()
    {
        var content = new StringBuilder();
        _ = content.Append(CultureInfo.InvariantCulture, $"{ThinWidth} w ");
        for (var rule = 0; rule < Rules; rule++)
        {
            var y = Margin + (rule * Spacing);
            _ = content.Append(CultureInfo.InvariantCulture, $"{Margin} {y} m {Margin + RuleLength} {y} l S {Margin} {y} 20 {Spacing} re S ");
        }

        return MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
        ]);
    }

    /// <summary>Gets the render options for the current parameter.</summary>
    /// <returns>The options.</returns>
    private PdfRenderOptions Options() => PdfRenderOptions.Default with { Quality = Quality() };

    /// <summary>Gets the quality settings for the current parameter.</summary>
    /// <returns>The settings.</returns>
    private PdfRenderQuality Quality() => Mode switch
    {
        "Adjusted" => PdfRenderQuality.Default with { StrokeAdjustment = PdfStrokeAdjustment.Always },
        "Thinnest" => PdfRenderQuality.Default with { MinimumLineWidth = 1 },
        _ => PdfRenderQuality.Default,
    };

    /// <summary>Draws the bottom-left tile of the page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>Whether the tile rendered.</returns>
    private bool Draw(PdfPageRenderer renderer) =>
        renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(_pixels, Tile, Tile, Tile * BytesPerPixel));
}
