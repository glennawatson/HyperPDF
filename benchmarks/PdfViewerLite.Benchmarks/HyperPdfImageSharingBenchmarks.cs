// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures pages that share one image, such as a logo or a watermark. The concurrent benchmark has two threads draw two
/// cold pages that use the same image at once, which shows whether the image is decoded once or twice. The picture
/// benchmark draws every page twice with a picture limit that holds the pages only if the shared image is counted once.
/// </summary>
public class HyperPdfImageSharingBenchmarks
{
    /// <summary>The side of the shared image in pixels.</summary>
    private const int ImageSide = 1536;

    /// <summary>The pages that share the image.</summary>
    private const int Pages = 16;

    /// <summary>The times every page is drawn.</summary>
    private const int Passes = 2;

    /// <summary>The tile size in pixels.</summary>
    private const int Tile = 128;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The page size in points.</summary>
    private const int PageSize = 200;

    /// <summary>The multiplier that scrambles the image samples so they do not compress away.</summary>
    private const int Scramble = 31;

    /// <summary>The scale of the tiles.</summary>
    private const float Scale = 1;

    /// <summary>The picture limit: the pages' operations plus one shared image fit, but not the image once per page.</summary>
    private const long PictureLimit = (Passes * ImageSide * ImageSide) + (1024 * 1024);

    /// <summary>The cache limit that keeps the one image.</summary>
    private const long ImageLimit = 64L * 1024 * 1024;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>Builds the document once.</summary>
    [GlobalSetup]
    public void Setup() => _pdf = Create();

    /// <summary>Two threads draw two cold pages that use the same image at once.</summary>
    /// <returns>Whether both tiles rendered.</returns>
    [Benchmark]
    public async Task<bool> ConcurrentColdImage()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return await RenderBoth(renderer);
    }

    /// <summary>Draws every page twice with a picture limit that fits the pages only when the shared image counts once.</summary>
    /// <returns>Whether every tile rendered.</returns>
    [Benchmark]
    public bool TwoPassesOverSharedImage()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document, PdfRenderOptions.Default with { ImageCacheBytes = ImageLimit, PictureCacheBytes = PictureLimit });
        var all = true;
        var pixels = new byte[Tile * Tile * BytesPerPixel];
        for (var pass = 0; pass < Passes; pass++)
        {
            for (var page = 0; page < Pages; page++)
            {
                all &= renderer.Render(new(page, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, Tile, Tile, Tile * BytesPerPixel));
            }
        }

        return all;
    }

    /// <summary>Starts two threads that draw pages 0 and 1 together.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>Whether both tiles rendered.</returns>
    private static async Task<bool> RenderBoth(PdfPageRenderer renderer)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var results = Task.WhenAll(Task.Run(() => RenderAfter(gate, renderer, 0)), Task.Run(() => RenderAfter(gate, renderer, 1)));
        gate.SetResult();
        var rendered = await results;
        return rendered[0] && rendered[1];
    }

    /// <summary>Waits for the gate and draws one tile of a page.</summary>
    /// <param name="gate">Opens when both threads are ready.</param>
    /// <param name="renderer">The renderer.</param>
    /// <param name="page">The page index.</param>
    /// <returns>Whether the tile rendered.</returns>
    private static async Task<bool> RenderAfter(TaskCompletionSource gate, PdfPageRenderer renderer, int page)
    {
        await gate.Task.ConfigureAwait(false);
        var pixels = new byte[Tile * Tile * BytesPerPixel];
        return renderer.Render(new(page, Scale, 0, 0, 0, PdfRenderFlags.None), new(pixels, Tile, Tile, Tile * BytesPerPixel));
    }

    /// <summary>Builds a document whose pages all draw one image.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create()
    {
        var samples = new byte[ImageSide * ImageSide];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)((i * Scramble) ^ (i >> 7));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            zlib.Write(samples);
        }

        var data = Encoding.Latin1.GetString(compressed.GetBuffer(), 0, (int)compressed.Length);
        var imageEntries = string.Create(
            CultureInfo.InvariantCulture,
            $"/Type /XObject /Subtype /Image /Width {ImageSide} /Height {ImageSide} /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /FlateDecode");
        var pageBody = string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageSize} {PageSize}] /Resources << /XObject << /Im 3 0 R >> >> /Contents 4 0 R >>");
        var kids = new StringBuilder();
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            MiniPdf.Stream(imageEntries, data),
            MiniPdf.Stream(string.Empty, $"q {PageSize} 0 0 {PageSize} 0 0 cm /Im Do Q"),
        };
        for (var i = 0; i < Pages; i++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{objects.Count + 1} 0 R ");
            objects.Add(pageBody);
        }

        objects[1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids}] /Count {Pages} >>");
        return MiniPdf.Build([.. objects]);
    }
}
