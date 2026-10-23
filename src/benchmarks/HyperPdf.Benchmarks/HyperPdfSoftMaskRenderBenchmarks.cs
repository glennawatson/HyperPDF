// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Renders a page of 200 shapes that each draw under the same luminosity soft mask at a different position, as repeated shadows do, against the same page without the mask.</summary>
public class HyperPdfSoftMaskRenderBenchmarks
{
    /// <summary>The shapes on the page.</summary>
    private const int Shapes = 200;

    /// <summary>The shapes in each row.</summary>
    private const int PerRow = 10;

    /// <summary>The distance between shapes across the page.</summary>
    private const int ColumnSpacing = 40;

    /// <summary>The distance between rows of shapes.</summary>
    private const int RowSpacing = 8;

    /// <summary>The bottom of the lowest shape, so every shape falls inside the top-left tile.</summary>
    private const int Floor = 300;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes of the page whose shapes are masked.</summary>
    private byte[] _pdf = [];

    /// <summary>The PDF bytes of the same page without the mask.</summary>
    private byte[] _plainPdf = [];

    /// <summary>Builds the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdf = Build(true);
        _plainPdf = Build(false);
    }

    /// <summary>Opens the document and records and draws the page of shapes without a mask.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark(Baseline = true)]
    public bool RenderShapesWithoutAMask() => Render(_plainPdf);

    /// <summary>Opens the document and records and draws the page of shapes under the soft mask.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderShapesUnderASoftMask() => Render(_pdf);

    /// <summary>Builds a document.</summary>
    /// <param name="masked">Whether each shape is drawn under the soft mask.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(bool masked)
    {
        var content = new StringBuilder();
        var state = masked ? "/GS gs " : string.Empty;
        for (var i = 0; i < Shapes; i++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"q 1 0 0 1 {(i % PerRow) * ColumnSpacing} {Floor + ((i / PerRow) * RowSpacing)} cm {state}0 0 1 rg 0 0 30 6 re f Q\n");
        }

        return MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /ExtGState << /GS << /SMask << /Type /Mask /S /Luminosity /G 5 0 R >> >> >> >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /CS /DeviceGray >>", "0.5 g 0 0 100 100 re f"));
    }

    /// <summary>Opens a document and records and draws its page.</summary>
    /// <param name="pdf">The file.</param>
    /// <returns>Whether the tile rendered.</returns>
    private bool Render(byte[] pdf)
    {
        using var document = BenchmarkDrawing.Open(pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }
}
