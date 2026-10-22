// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Renders a page of 200 small inline images, as scanned and exported pages with many tiny pictures have, against 200 plain rectangles in the same places.</summary>
public class HyperPdfInlineImageRenderBenchmarks
{
    /// <summary>The shapes on the page.</summary>
    private const int Shapes = 200;

    /// <summary>The shapes in each row.</summary>
    private const int PerRow = 10;

    /// <summary>The distance between shapes across the page.</summary>
    private const int ColumnSpacing = 40;

    /// <summary>The distance between rows of shapes.</summary>
    private const int RowSpacing = 10;

    /// <summary>The bottom of the lowest shape, so every shape falls inside the top-left tile.</summary>
    private const int Floor = 300;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes of the page of inline images.</summary>
    private byte[] _images = [];

    /// <summary>The PDF bytes of the page of rectangles.</summary>
    private byte[] _rectangles = [];

    /// <summary>Builds the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _images = Build("BI /W 2 /H 2 /CS /G /BPC 8 ID abcd EI");
        _rectangles = Build("0 g 0 0 1 1 re f");
    }

    /// <summary>Opens the document and records and draws the page of rectangles.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark(Baseline = true)]
    public bool RenderRectangles() => Render(_rectangles);

    /// <summary>Opens the document and records and draws the page of inline images.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderInlineImages() => Render(_images);

    /// <summary>Builds a page that draws a shape 200 times.</summary>
    /// <param name="shape">The content that draws the shape in the unit square.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string shape)
    {
        var content = new StringBuilder();
        for (var i = 0; i < Shapes; i++)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"q 20 0 0 8 {(i % PerRow) * ColumnSpacing} {Floor + ((i / PerRow) * RowSpacing)} cm {shape} Q\n");
        }

        return SkiaRecordingPage.Create(content.ToString(), string.Empty, false);
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
