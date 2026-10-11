// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures recorded transparency-page replay and document opening with the first render.</summary>
public class HyperPdfReplayBenchmarks
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSize = 512;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The device pixels per page point.</summary>
    private const float Scale = 1;

    /// <summary>The request shared by the warm and cold paths.</summary>
    private readonly PdfTileRequest _request = new(0, Scale, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The caller-owned target reused by every operation.</summary>
    private readonly byte[] _pixels = new byte[TileSize * TileSize * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The controlled transparency fixture.</summary>
    private byte[] _source = [];

    /// <summary>The document retained for picture replay.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer with the recorded page.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Opens the shared fixture and records its page before measuring replay.</summary>
    /// <exception cref="InvalidOperationException">The warm-up tile could not be rendered.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _source = RenderSamplePages.CreateTransparencyPage();
        _document = BenchmarkDrawing.Open(_source, null, _drawingServices);
        _renderer = new(_document);
        if (!WarmReplay())
        {
            throw new InvalidOperationException("The transparency tile could not be rendered.");
        }
    }

    /// <summary>Disposes the recorded page and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Replays the recorded page into the reused tile buffer.</summary>
    /// <returns>True when the tile rendered.</returns>
    [Benchmark]
    public bool WarmReplay() => _renderer.Render(_request, new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));

    /// <summary>Opens the fixture, records and renders its page, then disposes both document and renderer.</summary>
    /// <returns>True when the tile rendered.</returns>
    [Benchmark]
    public bool ColdOpenAndRender()
    {
        using var document = BenchmarkDrawing.Open(_source, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return renderer.Render(_request, new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));
    }
}
