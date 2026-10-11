// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace PdfViewerLite.Benchmarks;

/// <summary>Compares the caller-buffer and owned-target replay APIs on the same software Skia device.</summary>
public class OwnedRenderTargetBenchmarks
{
    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The retained page request.</summary>
    private readonly PdfTileRequest _request = new(0, 1, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The reused caller-owned pixels.</summary>
    private readonly byte[] _pixels = new byte[TileEdge * TileEdge * BytesPerPixel];

    /// <summary>The open fixture.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer retaining page recording.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>The reused backend-owned raster target.</summary>
    private SkiaSurfaceRenderTarget _target = null!;

    /// <summary>Opens and records the transparency page for both replay paths.</summary>
    /// <exception cref="InvalidOperationException">A target could not be created or rendered.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(RenderSamplePages.CreateTransparencyPage(), null);
        _renderer = new(_document);
        var info = new SKImageInfo(TileEdge, TileEdge, SKColorType.Bgra8888, SKAlphaType.Premul);
        _target = new(SKSurface.Create(info), info);
        if (!RenderCallerBuffer() || !RenderOwnedRasterSurface())
        {
            throw new InvalidOperationException("The transparency tile could not be rendered.");
        }
    }

    /// <summary>Releases the document, pictures and software target.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _target.Dispose();
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Replays the retained picture into caller-owned CPU pixels.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark(Baseline = true)]
    public bool RenderCallerBuffer() => _renderer.Render(_request, new(_pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel));

    /// <summary>Replays the retained picture into a backend-owned CPU raster surface.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool RenderOwnedRasterSurface() => _renderer.RenderToTarget(_request, _target);
}
