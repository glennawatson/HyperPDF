// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures recording eight text pages at once from glyph masks that are already cached, so every glyph is a mask lookup on a shared cache.</summary>
public class GlyphMaskParallelRecordBenchmarks
{
    /// <summary>The pages recorded at once.</summary>
    private const int ParallelPages = 8;

    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The tile each page renders.</summary>
    private readonly PdfTileRequest _origin = new(0, 1, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The pixel buffers, one for each parallel page.</summary>
    private readonly byte[][] _pixels = new byte[ParallelPages][];

    /// <summary>The drawing services, and so the glyph masks, shared by every page.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The open documents, one for each parallel page.</summary>
    private readonly PdfDocument[] _documents = new PdfDocument[ParallelPages];

    /// <summary>Opens the documents and records one of them, so the glyph masks are made before measuring.</summary>
    /// <exception cref="InvalidOperationException">The page did not render.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = RenderSamplePages.CreateTextPage();
        for (var i = 0; i < ParallelPages; i++)
        {
            _pixels[i] = new byte[TileEdge * TileEdge * BytesPerPixel];
            _documents[i] = BenchmarkDrawing.Open(bytes, null, _drawingServices);
        }

        if (!RecordPage(0))
        {
            throw new InvalidOperationException("The text page did not render.");
        }
    }

    /// <summary>Disposes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var document in _documents)
        {
            document.Dispose();
        }
    }

    /// <summary>Records eight pages at once on the pool, each into a new picture, drawing every glyph from the shared masks.</summary>
    /// <returns>The pages drawn.</returns>
    [Benchmark]
    public int RecordEightTextPagesInParallelFromCachedMasks()
    {
        var drawn = 0;
        _ = Parallel.For(0, ParallelPages, i =>
        {
            if (RecordPage(i))
            {
                _ = Interlocked.Increment(ref drawn);
            }
        });
        return drawn;
    }

    /// <summary>Records and draws one page with a renderer that has no pictures yet.</summary>
    /// <param name="index">Which document.</param>
    /// <returns>Whether the page rendered.</returns>
    private bool RecordPage(int index)
    {
        using var renderer = new PdfPageRenderer(_documents[index], PdfRenderOptions.Default with { Quality = PdfRenderQuality.Default with { GlyphMasks = true } });
        return renderer.Render(_origin, new(_pixels[index], TileEdge, TileEdge, TileEdge * BytesPerPixel));
    }
}
