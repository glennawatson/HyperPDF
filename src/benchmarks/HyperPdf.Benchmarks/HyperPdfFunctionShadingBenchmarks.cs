// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Renders a page whose only content is a function-based (type 1) shading, which is evaluated for every point of a 256 by 256 grid when the page is first drawn.</summary>
public class HyperPdfFunctionShadingBenchmarks
{
    /// <summary>The drawing services reused across benchmark invocations.</summary>
    private readonly PdfDrawingServices _drawingServices = BenchmarkDrawing.CreateServices();

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[SkiaRecordingPage.Tile * SkiaRecordingPage.Tile * SkiaRecordingPage.BytesPerPixel];

    /// <summary>The PDF bytes.</summary>
    private byte[] _pdf = [];

    /// <summary>Builds the document.</summary>
    [GlobalSetup]
    public void Setup() => _pdf = MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Shading << /Sh 5 0 R >> >> /Contents 4 0 R >>",
        MiniPdf.Stream(string.Empty, "/Sh sh"),
        "<< /ShadingType 1 /ColorSpace /DeviceRGB /Domain [0 1 0 1] /Matrix [400 0 0 400 100 300] /Function 6 0 R >>",
        MiniPdf.Stream("/FunctionType 4 /Domain [0 1 0 1] /Range [0 1 0 1 0 1]", "{ 2 copy mul 3 1 roll }"));

    /// <summary>Opens the document and records and draws the page.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderFunctionShading()
    {
        using var document = BenchmarkDrawing.Open(_pdf, null, _drawingServices);
        using var renderer = new PdfPageRenderer(document);
        return SkiaRecordingPage.Draw(renderer, _pixels);
    }
}
