// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures overlaying a page and watermarking text and an image on every page of a document, each followed by its undo.</summary>
public class HyperPdfStampBenchmarks
{
    /// <summary>The pages in the target.</summary>
    private const int Pages = 100;

    /// <summary>The edge of the watermark image in pixels.</summary>
    private const int ImageEdge = 16;

    /// <summary>The width the image is drawn at, in points.</summary>
    private const float ImageWidth = 120;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The size of the watermark text in points.</summary>
    private const float TextSize = 60;

    /// <summary>The turn of the watermark in degrees.</summary>
    private const float Turn = 45;

    /// <summary>The pages every operation draws on.</summary>
    private readonly int[] _pages = CreatePages();

    /// <summary>The image watermark's pixels.</summary>
    private readonly byte[] _pixels = new byte[ImageEdge * ImageEdge * BytesPerPixel];

    /// <summary>The overlay mappings, one per page.</summary>
    private readonly PdfOverlayMapping[] _mappings = PdfDocumentOverlays.MapEveryPage(Pages, 0);

    /// <summary>The document edited and restored by each operation.</summary>
    private PdfDocument _target = null!;

    /// <summary>The document whose first page is overlaid.</summary>
    private PdfDocument _source = null!;

    /// <summary>Opens the fixtures and warms each operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _target = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        _source = PdfDocumentReader.Open(TestPdf.Create(1), null);
        _pixels.AsSpan().Fill(byte.MaxValue);
        _ = OverlayAndUndo();
        _ = TextWatermarkAndUndo();
        _ = ImageWatermarkAndUndo();
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _target.Dispose();
        _source.Dispose();
    }

    /// <summary>Overlays a page on every page, then undoes it.</summary>
    /// <returns>Whether the undo restored the document.</returns>
    [Benchmark]
    public bool OverlayAndUndo()
    {
        PdfDocumentOverlays.OverlayPages(_target, _source, _mappings, PdfOverlayOptions.Default);
        return PdfDocumentEditing.Undo(_target);
    }

    /// <summary>Draws a text watermark in a layer on every page, then undoes it.</summary>
    /// <returns>Whether the undo restored the document.</returns>
    [Benchmark]
    public bool TextWatermarkAndUndo()
    {
        PdfDocumentWatermarks.AddText(_target, _pages, "CONFIDENTIAL", new() { FontSize = TextSize }, new() { RotationDegrees = Turn, LayerName = "Watermark" });
        return PdfDocumentEditing.Undo(_target);
    }

    /// <summary>Draws an image watermark on every page, then undoes it.</summary>
    /// <returns>Whether the undo restored the document.</returns>
    [Benchmark]
    public bool ImageWatermarkAndUndo()
    {
        PdfDocumentWatermarks.AddImage(_target, _pages, new(_pixels, ImageEdge, ImageEdge, ImageWidth), PdfWatermarkOptions.Default);
        return PdfDocumentEditing.Undo(_target);
    }

    /// <summary>Lists every page of the fixture.</summary>
    /// <returns>The page indexes.</returns>
    private static int[] CreatePages()
    {
        var pages = new int[Pages];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = i;
        }

        return pages;
    }
}
