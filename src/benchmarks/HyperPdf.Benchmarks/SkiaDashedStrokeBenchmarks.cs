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

/// <summary>Measures recording a page of dashed lines that share one dash array, the case a kept dash effect serves.</summary>
public class SkiaDashedStrokeBenchmarks
{
    /// <summary>The dashed lines on the page.</summary>
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

    /// <summary>Builds the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder("0 G 0.5 w [3 2] 0 d ");
        for (var line = 0; line < Lines; line++)
        {
            var y = Floor + ((line % Rows) * Spacing);
            var x = line / Rows;
            _ = content.Append(CultureInfo.InvariantCulture, $"{x} {y} m {x + LineLength} {y} l S ");
        }

        _pdf = SkiaRecordingPage.Create(content.ToString(), string.Empty, false);
    }

    /// <summary>Opens the document and records and draws the page, which sets one dash array and strokes many lines.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RecordPageOfDashedLines()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }
}
