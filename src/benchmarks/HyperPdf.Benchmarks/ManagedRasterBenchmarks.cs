// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Render.Managed;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures drawing one recorded page of text into a tile with the managed rasteriser, against Skia drawing the same page.</summary>
public class ManagedRasterBenchmarks
{
    /// <summary>The pixels of a point: a page at twice its size, as on a high resolution display.</summary>
    private const int Scale = 2;

    /// <summary>The width of the tile in pixels: a US letter page at the scale.</summary>
    private const int Width = 612 * Scale;

    /// <summary>The height of the tile in pixels.</summary>
    private const int Height = 792 * Scale;

    /// <summary>The bytes of one pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The lines of text on the page.</summary>
    private const int Lines = 40;

    /// <summary>The tile's pixels.</summary>
    private readonly byte[] _pixels = new byte[Width * Height * BytesPerPixel];

    /// <summary>The document opened with Skia.</summary>
    private PdfDocument? _skiaDocument;

    /// <summary>The document opened with the managed rasteriser.</summary>
    private PdfDocument? _managedDocument;

    /// <summary>The renderer that draws with Skia.</summary>
    private PdfPageRenderer? _skia;

    /// <summary>The renderer that draws with the managed rasteriser.</summary>
    private PdfPageRenderer? _managed;

    /// <summary>Opens the page with both backends and draws it once, so each run draws from the recorded picture.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var pdf = TestPdf.CreateTextFilled(1, Lines);
        var skia = new PdfRenderBackendRegistration().UseSkia();
        _skiaDocument = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = PdfFontLoader.Load, DrawingServices = new(skia) });
        _managedDocument = PdfDocumentReader.OpenWith(pdf, new PdfOpenOptions { FontFactory = PdfFontLoader.Load, DrawingServices = new(skia.UseManaged()) });
        _skia = new(_skiaDocument);
        _managed = new(_managedDocument);
        _ = SkiaTile();
        _ = ManagedTile();
    }

    /// <summary>Releases the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _skia?.Dispose();
        _managed?.Dispose();
        _skiaDocument?.Dispose();
        _managedDocument?.Dispose();
    }

    /// <summary>Draws the page with Skia.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark(Baseline = true)]
    public bool SkiaTile() => _skia!.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(_pixels, Width, Height, Width * BytesPerPixel));

    /// <summary>Draws the page with the managed rasteriser.</summary>
    /// <returns>Whether the page was drawn.</returns>
    [Benchmark]
    public bool ManagedTile() => _managed!.Render(new(0, Scale, 0, 0, 0, PdfRenderFlags.None), new(_pixels, Width, Height, Width * BytesPerPixel));
}
