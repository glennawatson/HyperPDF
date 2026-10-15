// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Creation;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures drawing stroked lines and rectangle outlines on a page canvas. The baseline is the way a caller had to draw a rule
/// before the canvas had stroke methods: a thin filled rectangle. The page is abandoned rather than finished, so the
/// measurement is the content the drawing writes.
/// </summary>
public class HyperPdfStrokeBenchmarks
{
    /// <summary>The lines or outlines drawn on each page.</summary>
    private const int Shapes = 500;

    /// <summary>The page size in points.</summary>
    private const float PageSize = 600F;

    /// <summary>The left edge of every shape.</summary>
    private const float Left = 20F;

    /// <summary>The length of a line and the width of an outline.</summary>
    private const float Length = 400F;

    /// <summary>The height of an outline.</summary>
    private const float OutlineHeight = 8F;

    /// <summary>The pitch between shapes.</summary>
    private const float Pitch = 1.1F;

    /// <summary>The stroke width of a line.</summary>
    private const float StrokeWidth = 0.5F;

    /// <summary>The document the pages belong to.</summary>
    private readonly PdfDocumentCreator _creator = new(PdfDocumentCreatorOptions.Default);

    /// <summary>Draws horizontal rules as thin filled rectangles, the way it was done before the canvas could stroke.</summary>
    /// <returns>The size of the page's content.</returns>
    [Benchmark(Baseline = true)]
    public int FilledRectangleRules()
    {
        var canvas = _creator.BeginPage(PageSize, PageSize);
        try
        {
            for (var i = 0; i < Shapes; i++)
            {
                canvas.FillRectangle(0F, 0F, 0F, Left, i * Pitch, Length, StrokeWidth);
            }

            return canvas.Content.Length;
        }
        finally
        {
            canvas.Dispose();
        }
    }

    /// <summary>Draws horizontal lines with <see cref="PdfPageCanvas.DrawLine"/>.</summary>
    /// <returns>The size of the page's content.</returns>
    [Benchmark]
    public int StrokedLines()
    {
        var canvas = _creator.BeginPage(PageSize, PageSize);
        try
        {
            var stroke = PdfStroke.Black(StrokeWidth);
            for (var i = 0; i < Shapes; i++)
            {
                canvas.DrawLine(stroke, Left, i * Pitch, Left + Length, i * Pitch);
            }

            return canvas.Content.Length;
        }
        finally
        {
            canvas.Dispose();
        }
    }

    /// <summary>Draws rectangle outlines with <see cref="PdfPageCanvas.StrokeRectangle"/>.</summary>
    /// <returns>The size of the page's content.</returns>
    [Benchmark]
    public int StrokedRectangles()
    {
        var canvas = _creator.BeginPage(PageSize, PageSize);
        try
        {
            var stroke = PdfStroke.Black(StrokeWidth);
            for (var i = 0; i < Shapes; i++)
            {
                canvas.StrokeRectangle(stroke, Left, i * Pitch, Length, OutlineHeight);
            }

            return canvas.Content.Length;
        }
        finally
        {
            canvas.Dispose();
        }
    }
}
