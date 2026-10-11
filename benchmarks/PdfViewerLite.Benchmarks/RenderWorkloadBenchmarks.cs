// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures CPU page recording and tile rasterization for text, scanned image and transparency workloads.</summary>
public class RenderWorkloadBenchmarks
{
    /// <summary>The tile edge in pixels.</summary>
    private const int TileEdge = 512;

    /// <summary>The number of bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The next zoom level used for an uncached raster size.</summary>
    private const float ZoomScale = 1.25F;

    /// <summary>The scroll distance between two visible tiles.</summary>
    private const int ScrollOffset = 128;

    /// <summary>The tile at the page origin.</summary>
    private readonly PdfTileRequest _origin = new(0, 1, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The tile at the next zoom level.</summary>
    private readonly PdfTileRequest _zoom = new(0, ZoomScale, 0, 0, 0, PdfRenderFlags.None);

    /// <summary>The tile after a viewport scroll.</summary>
    private readonly PdfTileRequest _scroll = new(0, 1, 0, ScrollOffset, ScrollOffset, PdfRenderFlags.None);

    /// <summary>The reused caller-owned CPU pixel buffer.</summary>
    private readonly byte[] _pixels = new byte[TileEdge * TileEdge * BytesPerPixel];

    /// <summary>The source PDF.</summary>
    private byte[] _source = [];

    /// <summary>The open document retained for warm replay.</summary>
    private PdfDocument _document = null!;

    /// <summary>The renderer retaining recorded page pictures.</summary>
    private PdfPageRenderer _renderer = null!;

    /// <summary>Gets the synthetic workloads and explicitly configured external PDFs.</summary>
    public static IEnumerable<string> Workloads
    {
        get
        {
            if (Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_EXTERNAL_ONLY") != "1")
            {
                yield return "Text";
                yield return "Scan";
                yield return "Transparency";
            }

            foreach (var workload in ExternalWorkloads())
            {
                yield return workload;
            }
        }
    }

    /// <summary>Gets or sets the synthetic workload or an external PDF workload.</summary>
    [ParamsSource(nameof(Workloads))]
    public string Workload { get; set; } = string.Empty;

    /// <summary>Opens and records the fixture before warm operations are measured.</summary>
    /// <returns>A task that completes when the local file, if any, has been read.</returns>
    /// <exception cref="InvalidOperationException">The workload is unknown or its first tile cannot be rendered.</exception>
    [GlobalSetup]
    public async Task Setup()
    {
        _source = Workload switch
        {
            "Text" => RenderSamplePages.CreateTextPage(),
            "Scan" => RenderSamplePages.CreateScanPage(),
            "Transparency" => RenderSamplePages.CreateTransparencyPage(),
            "LocalChart" => await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_CHART")!),
            "USGSRaster" => await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_USGS_RASTER")!),
            "USGSLayered" => await File.ReadAllBytesAsync(Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_USGS_LAYERED")!),
            _ => throw new InvalidOperationException($"Unknown render workload: {Workload}"),
        };
        _document = PdfDocumentReader.Open(_source, null);
        _renderer = new(_document);
        if (!WarmReplay())
        {
            throw new InvalidOperationException($"The {Workload} page did not render.");
        }

        if (!WarmZoomTile())
        {
            throw new InvalidOperationException($"The {Workload} page did not render at the zoom scale.");
        }
    }

    /// <summary>Disposes the retained page recording and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer.Dispose();
        _document.Dispose();
    }

    /// <summary>Measures opening, first-page recording, image decoding and rasterization together.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool ColdOpenAndRender()
    {
        using var document = PdfDocumentReader.Open(_source, null);
        using var renderer = new PdfPageRenderer(document);
        return renderer.Render(_origin, Target());
    }

    /// <summary>Measures page recording and first rasterization with an already open document.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool FirstRecordOnOpenDocument()
    {
        using var renderer = new PdfPageRenderer(_document);
        return renderer.Render(_origin, Target());
    }

    /// <summary>Measures rasterization of a retained page recording.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool WarmReplay() => _renderer.Render(_origin, Target());

    /// <summary>Measures a new raster size using the retained page recording.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool WarmZoomTile() => _renderer.Render(_zoom, Target());

    /// <summary>Measures a shifted visible tile using the retained page recording.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool WarmScrollTile() => _renderer.Render(_scroll, Target());

    /// <summary>Gets external PDF workloads whose files are present.</summary>
    /// <returns>The configured workloads.</returns>
    private static IEnumerable<string> ExternalWorkloads()
    {
        if (File.Exists(Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_CHART")))
        {
            yield return "LocalChart";
        }

        if (File.Exists(Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_USGS_RASTER")))
        {
            yield return "USGSRaster";
        }

        if (File.Exists(Environment.GetEnvironmentVariable("PDFVIEWERLITE_BENCHMARK_USGS_LAYERED")))
        {
            yield return "USGSLayered";
        }
    }

    /// <summary>Gets the caller-owned tile target without allocating.</summary>
    /// <returns>The target.</returns>
    private PdfTileTarget Target() => new(_pixels, TileEdge, TileEdge, TileEdge * BytesPerPixel);
}
