// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures how long a newly scrolled-in page waits for its first sharp tile while the view keeps moving. Each frame
/// advances the canvas generation and asks for the tile again, as the page canvas does on every scrolled frame. Every
/// iteration opens the page afresh, so the tile is a cold render, as for a page of a large document seen for the
/// first time.
/// </summary>
public class ScrollTileDeliveryBenchmarks
{
    /// <summary>The longest scroll measured, in frames, after which the tile counts as never arriving while scrolling.</summary>
    private const int MaxFrames = 120;

    /// <summary>The tile scale in device pixels per point: 150% zoom on a 4/3 display scale.</summary>
    private const float Scale = 2F;

    /// <summary>The document identifier used in the tile key.</summary>
    private const int DocumentId = 1;

    /// <summary>The time between frames, for a 60 Hz display.</summary>
    private static readonly TimeSpan FrameInterval = TimeSpan.FromSeconds(1.0 / 60);

    /// <summary>The PDF of the workload.</summary>
    private byte[] _source = [];

    /// <summary>The document opened for the current iteration.</summary>
    private BenchmarkPdfDocument? _document;

    /// <summary>The scheduler for the current iteration.</summary>
    private RenderScheduler? _scheduler;

    /// <summary>The first tile's width in pixels.</summary>
    private int _tileWidth;

    /// <summary>The first tile's height in pixels.</summary>
    private int _tileHeight;

    /// <summary>Gets the synthetic pages and the external PDFs configured for <see cref="RenderWorkloadBenchmarks"/>.</summary>
    public static IEnumerable<string> Workloads => ["Text", "Scan", "Transparency", .. RenderWorkloadBenchmarks.ExternalWorkloads()];

    /// <summary>Gets or sets the page workload.</summary>
    [ParamsSource(nameof(Workloads))]
    public string Workload { get; set; } = string.Empty;

    /// <summary>Builds or reads the workload page.</summary>
    /// <returns>A task that completes when an external file has been read.</returns>
    /// <exception cref="InvalidOperationException">The workload is unknown.</exception>
    [GlobalSetup]
    public async Task Setup() =>
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

    /// <summary>Opens the page cold and starts an idle scheduler.</summary>
    [IterationSetup]
    public void OpenPage()
    {
        _document = new(_source);
        _scheduler = new(new PixelSurfaceFactory());
        TileGrid.GetPagePixelSize(_document.Document.GetPageSizes()[0], PageRotation.None, Scale, out var pageWidth, out var pageHeight);
        TileGrid.GetTileSize(pageWidth, pageHeight, 0, 0, out _tileWidth, out _tileHeight);
    }

    /// <summary>Stops the scheduler and closes the page.</summary>
    [IterationCleanup]
    public void ClosePage()
    {
        _scheduler?.Dispose();
        _document?.Dispose();
    }

    /// <summary>Scrolls frame by frame until the page's first tile is delivered, or the scroll ends.</summary>
    /// <returns>The frames scrolled before the tile arrived.</returns>
    [Benchmark]
    public async Task<int> FramesUntilTileArrives()
    {
        var scheduler = _scheduler!;
        var document = _document!.Document;
        var client = new RenderClient();
        var key = new TileKey(DocumentId, 0, TileGrid.ToScaleKey(Scale), PageRotation.None, 0, 0, 0);
        var info = new PageRenderInfo(0, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
        using var timer = new PeriodicTimer(FrameInterval);
        for (var frame = 1; frame <= MaxFrames; frame++)
        {
            var generation = client.Advance();
            _ = scheduler.Request(new(key, document, info, _tileWidth, _tileHeight, RenderPriority.Visible, client, generation, PageTone.None));
            _ = await timer.WaitForNextTickAsync();
            if (!scheduler.TryTakeCompleted(out var tile))
            {
                continue;
            }

            tile.Surface.Dispose();
            return frame;
        }

        return MaxFrames;
    }

    /// <summary>Creates CPU pixel surfaces.</summary>
    private sealed class PixelSurfaceFactory : IRenderSurfaceFactory
    {
        /// <inheritdoc/>
        public IRenderSurface Create(int width, int height) => new PixelSurface(width, height);
    }

    /// <summary>A CPU pixel surface.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    private sealed class PixelSurface(int width, int height) : IRenderSurface
    {
        /// <summary>The bytes in one BGRA pixel.</summary>
        private const int BytesPerPixel = 4;

        /// <summary>The BGRA pixels.</summary>
        private readonly byte[] _pixels = new byte[width * height * BytesPerPixel];

        /// <inheritdoc/>
        public int Width => width;

        /// <inheritdoc/>
        public int Height => height;

        /// <inheritdoc/>
        public long ByteSize => _pixels.Length;

        /// <inheritdoc/>
        public bool Write<TState>(in TState state, SurfaceWriter<TState> writer) =>
            writer(new(_pixels, width, height, width * BytesPerPixel), state);

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
