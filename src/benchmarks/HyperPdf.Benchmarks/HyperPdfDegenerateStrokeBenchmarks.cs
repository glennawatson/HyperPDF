// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Strokes 1,000 lines of zero length with butt caps, as dotted leaders made of points are, against 1,000 lines of one point.</summary>
public class HyperPdfDegenerateStrokeBenchmarks
{
    /// <summary>The lines on the page.</summary>
    private const int Lines = 1000;

    /// <summary>The lines in each row.</summary>
    private const int PerRow = 50;

    /// <summary>The distance between lines across the page.</summary>
    private const int ColumnSpacing = 8;

    /// <summary>The distance between rows of lines.</summary>
    private const int RowSpacing = 6;

    /// <summary>The bottom of the lowest line, so every line falls inside the top-left tile.</summary>
    private const int Floor = 300;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes of the page of zero length lines.</summary>
    private byte[] _degenerate = [];

    /// <summary>The PDF bytes of the page of one point lines.</summary>
    private byte[] _short = [];

    /// <summary>Builds the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _degenerate = Build(0);
        _short = Build(1);
    }

    /// <summary>Opens the document and records and draws the page of one point lines.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark(Baseline = true)]
    public bool RenderShortLines() => Render(_short);

    /// <summary>Opens the document and records and draws the page of zero length lines.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderZeroLengthLines() => Render(_degenerate);

    /// <summary>Builds a page of lines.</summary>
    /// <param name="length">The length of each line.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(int length)
    {
        var content = new StringBuilder("0 G 2 w 0 J ");
        for (var i = 0; i < Lines; i++)
        {
            var x = (i % PerRow) * ColumnSpacing;
            _ = content.Append(CultureInfo.InvariantCulture, $"{x} {Floor + ((i / PerRow) * RowSpacing)} m {x + length} {Floor + ((i / PerRow) * RowSpacing)} l S ");
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
