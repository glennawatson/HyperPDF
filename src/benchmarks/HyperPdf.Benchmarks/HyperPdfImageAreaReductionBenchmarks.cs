// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Compression;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures a large scan drawn on a small page: the cold draw decodes, reduces and records the page, and the warm draw
/// replays the recorded page. With area reduction on, the picture holds an image eight times smaller on each side.
/// </summary>
public class HyperPdfImageAreaReductionBenchmarks
{
    /// <summary>The side of the image in pixels.</summary>
    private const int ImageSide = 2400;

    /// <summary>The page size in points, so the image shrinks eight times at scale one.</summary>
    private const int PageSize = 300;

    /// <summary>The tile size in pixels.</summary>
    private const int Tile = 300;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The color components of the image.</summary>
    private const int Components = 3;

    /// <summary>The multiplier that spreads the image samples so they do not compress away.</summary>
    private const int Scramble = 31;

    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[Tile * Tile * BytesPerPixel];

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>The document kept for the warm draw.</summary>
    private PdfDocument? _document;

    /// <summary>The renderer kept for the warm draw.</summary>
    private PdfPageRenderer? _renderer;

    /// <summary>Gets or sets a value indicating whether large reductions are averaged by area.</summary>
    [Params(false, true)]
    public bool Area { get; set; }

    /// <summary>Builds the document and records the page for the warm draw.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _pdf = Create();
        _document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        _renderer = new(_document, Options());
        _ = Draw(_renderer);
    }

    /// <summary>Releases the warm renderer and document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer?.Dispose();
        _document?.Dispose();
    }

    /// <summary>Opens the document and draws the page for the first time: decode, reduce, record and replay.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool ColdDraw()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document, Options());
        return Draw(renderer);
    }

    /// <summary>Draws the page again from its recorded picture.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool WarmDraw() => Draw(_renderer!);

    /// <summary>Builds a one page document drawing a large RGB image over the page.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create()
    {
        var samples = new byte[ImageSide * ImageSide * Components];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)(((i / Components % ImageSide) >> 3) + ((i * Scramble) & 0x3F));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        {
            zlib.Write(samples);
        }

        var data = Encoding.Latin1.GetString(compressed.GetBuffer(), 0, (int)compressed.Length);
        var imageEntries = string.Create(
            CultureInfo.InvariantCulture,
            $"/Type /XObject /Subtype /Image /Width {ImageSide} /Height {ImageSide} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode");
        return MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [5 0 R] /Count 1 >>",
            MiniPdf.Stream(imageEntries, data),
            MiniPdf.Stream(string.Empty, string.Create(CultureInfo.InvariantCulture, $"q {PageSize} 0 0 {PageSize} 0 0 cm /Im Do Q")),
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageSize} {PageSize}] /Resources << /XObject << /Im 3 0 R >> >> /Contents 4 0 R >>"),
        ]);
    }

    /// <summary>Gets the render options for the current parameter.</summary>
    /// <returns>The options.</returns>
    private PdfRenderOptions Options() => PdfRenderOptions.Default with { Quality = PdfRenderQuality.Default with { AreaImageReduction = Area } };

    /// <summary>Draws the whole page into the tile buffer.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <returns>Whether the tile rendered.</returns>
    private bool Draw(PdfPageRenderer renderer) =>
        renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(_pixels, Tile, Tile, Tile * BytesPerPixel));
}
