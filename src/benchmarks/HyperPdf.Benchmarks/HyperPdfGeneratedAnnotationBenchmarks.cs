// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures annotations without appearance streams: replaying a page whose appearances are already generated and
/// recorded, and recording the page's annotations from scratch, which generates every appearance.
/// </summary>
public class HyperPdfGeneratedAnnotationBenchmarks
{
    /// <summary>The page size in points.</summary>
    private const int PageSize = 200;

    /// <summary>The tile side in pixels.</summary>
    private const int TileSize = 256;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The device pixels per page point.</summary>
    private const float Scale = 1;

    /// <summary>The annotations of the page, none with an appearance stream.</summary>
    private static readonly string[] Annotations =
    [
        "/Subtype /Square /Rect [10 10 60 60] /C [0 0 1] /IC [1 0 0] /BS << /W 4 >> /F 4",
        "/Subtype /Circle /Rect [70 10 120 60] /IC [0 1 0] /F 4",
        "/Subtype /Highlight /Rect [10 70 120 90] /QuadPoints [10 90 120 90 10 70 120 70] /F 4",
        "/Subtype /Underline /Rect [10 100 120 120] /QuadPoints [10 120 120 120 10 100 120 100] /F 4",
        "/Subtype /Ink /Rect [130 10 190 60] /InkList [[130 10 160 60 190 10]] /C [1 0 1] /F 4",
        "/Subtype /Text /Rect [130 70 160 100] /F 4",
        "/Subtype /Link /Rect [10 130 60 180] /C [1 0 0] /Border [0 0 2] /F 4",
        "/Subtype /Caret /Rect [70 130 120 180] /C [0 0 1] /F 4",
        "/Subtype /Redact /Rect [130 130 190 180] /OverlayText (X) /F 4",
    ];

    /// <summary>The request for the whole page.</summary>
    private readonly PdfTileRequest _request = new(0, Scale, 0, 0, 0, PdfRenderFlags.Annotations);

    /// <summary>The caller-owned target reused by every operation.</summary>
    private readonly byte[] _pixels = new byte[TileSize * TileSize * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The page.</summary>
    private byte[] _source = [];

    /// <summary>The document retained for replay.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer holding the recorded page.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Builds the page and records it before measuring replay.</summary>
    /// <exception cref="InvalidOperationException">The warm-up tile could not be rendered.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var pdf = new RenderTestPdf(PageSize, PageSize) { Content = "0.9 g 0 0 200 200 re f" };
        var references = string.Concat(Annotations.Select(entries => $"{pdf.AddObject($"<< /Type /Annot {entries} >>")} 0 R "));
        pdf.PageEntries = $"/Annots [{references}]";
        _source = pdf.ToBytes();
        _document = BenchmarkDrawing.Open(_source, null, _drawingServices);
        _renderer = new(_document);
        if (!WarmReplay())
        {
            throw new InvalidOperationException("The annotation page could not be rendered.");
        }
    }

    /// <summary>Disposes the recorded page and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Replays the recorded page and its generated appearances into the reused tile buffer.</summary>
    /// <returns>True when the tile rendered.</returns>
    [Benchmark]
    public bool WarmReplay() => _renderer.Render(_request, new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));

    /// <summary>Records the page again with a new renderer, generating every appearance, and renders a tile.</summary>
    /// <returns>True when the tile rendered.</returns>
    [Benchmark]
    public bool RecordAndRender()
    {
        using var renderer = new PdfPageRenderer(_document);
        return renderer.Render(_request, new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));
    }
}
