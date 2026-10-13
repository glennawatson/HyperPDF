// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Measures a page of 10,000 one-pixel hairlines on a page with a user unit, replayed warm with and without the user unit applied.</summary>
public class HyperPdfHairlineBenchmarks
{
    /// <summary>The hairlines on the page.</summary>
    private const int Lines = 10_000;

    /// <summary>The page edge in user space units.</summary>
    private const int PageEdge = 400;

    /// <summary>The rows the hairlines are spread over.</summary>
    private const int Rows = 100;

    /// <summary>The distance between hairlines and their length, in user space units.</summary>
    private const int Pitch = 4;

    /// <summary>The tile side in pixels.</summary>
    private const int TileSize = 512;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The device pixels per point.</summary>
    private const float Scale = 1;

    /// <summary>The page object shared by the sample: catalog, page tree and page.</summary>
    private const string Catalog = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>The caller-owned target reused by every operation.</summary>
    private readonly byte[] _pixels = new byte[TileSize * TileSize * BytesPerPixel];

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The document retained for picture replay.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer with the recorded page.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Creates a page with a user unit of 2 and 10,000 zero-width strokes.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateHairlinePage()
    {
        var content = new StringBuilder("0 w 0 G\n");
        for (var i = 0; i < Lines; i++)
        {
            var y = i % Rows;
            var x = i / Rows;
            _ = content.Append(CultureInfo.InvariantCulture, $"{x * Pitch} {y * Pitch} m {(x * Pitch) + Pitch} {(y * Pitch) + Pitch} l S\n");
        }

        return MiniPdf.Build(
            Catalog,
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageEdge} {PageEdge}] /UserUnit 2 /Resources << >> /Contents 4 0 R >>",
            MiniPdf.Stream(string.Empty, content.ToString()));
    }

    /// <summary>Opens the page and records it before measuring replay.</summary>
    /// <exception cref="InvalidOperationException">The warm-up tile could not be rendered.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _document = BenchmarkDrawing.Open(CreateHairlinePage(), null, _drawingServices);
        _renderer = new(_document);
        if (!Warm())
        {
            throw new InvalidOperationException("The hairline tile could not be rendered.");
        }
    }

    /// <summary>Disposes the recorded page and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Replays the hairlines at the page's physical scale (user unit 2).</summary>
    /// <returns>True when the tile rendered.</returns>
    [Benchmark]
    public bool Warm() => Render(PdfRenderFlags.None);

    /// <summary>Renders the first tile.</summary>
    /// <param name="flags">The render flags.</param>
    /// <returns>True when the tile rendered.</returns>
    private bool Render(PdfRenderFlags flags) =>
        _renderer.Render(new(0, Scale, 0, 0, 0, flags), new(_pixels, TileSize, TileSize, TileSize * BytesPerPixel));
}
