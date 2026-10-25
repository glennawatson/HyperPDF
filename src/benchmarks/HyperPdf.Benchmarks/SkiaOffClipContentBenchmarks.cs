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

/// <summary>Measures recording a page whose rectangles and images mostly lie outside the page, the case a clip test before drawing serves.</summary>
public class SkiaOffClipContentBenchmarks
{
    /// <summary>The rectangles on the page.</summary>
    private const int Rectangles = 3000;

    /// <summary>The images on the page.</summary>
    private const int Images = 300;

    /// <summary>One in this many shapes lies on the page.</summary>
    private const int VisibleOneIn = 10;

    /// <summary>The distance outside the page of the shapes that are not visible, in points.</summary>
    private const int OutsideOffset = 5000;

    /// <summary>The size of each rectangle and image in points.</summary>
    private const int ShapeSize = 8;

    /// <summary>The distance between visible shapes in points.</summary>
    private const int Pitch = 10;

    /// <summary>The visible shapes in each row.</summary>
    private const int PerRow = 40;

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

    /// <summary>Builds the document and records the page for the warm draw.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = new StringBuilder("0.2 0.4 0.8 rg ");
        for (var shape = 0; shape < Rectangles; shape++)
        {
            var (x, y) = Position(shape);
            _ = content.Append(CultureInfo.InvariantCulture, $"{x} {y} {ShapeSize} {ShapeSize} re f ");
        }

        for (var shape = 0; shape < Images; shape++)
        {
            var (x, y) = Position(shape);
            _ = content.Append(CultureInfo.InvariantCulture, $"q {ShapeSize} 0 0 {ShapeSize} {x} {y} cm /Im Do Q ");
        }

        _pdf = SkiaRecordingPage.Create(content.ToString(), SkiaRecordingPage.ImageResources(), true);
        _document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        _renderer = new(_document);
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
    public bool RecordPageOfMostlyOffClipShapes()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }

    /// <summary>Draws the page again from its recorded picture.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool ReplayPageOfMostlyOffClipShapes() => SkiaRecordingPage.Draw(_renderer!, _pixels);

    /// <summary>Gets where a shape is drawn: on the page for one in ten, far outside it otherwise.</summary>
    /// <param name="index">The shape's index.</param>
    /// <returns>The position in points.</returns>
    private static ShapePosition Position(int index)
    {
        var slot = index / VisibleOneIn;
        var x = (slot % PerRow) * Pitch;
        var y = SkiaRecordingPage.PageHeight - ((slot / PerRow) * Pitch) - Pitch;
        return index % VisibleOneIn == 0 ? new(x, y) : new(x + OutsideOffset, y + OutsideOffset);
    }

    /// <summary>A position on the page, in points.</summary>
    /// <param name="X">The x position.</param>
    /// <param name="Y">The y position.</param>
    private readonly record struct ShapePosition(int X, int Y);
}
