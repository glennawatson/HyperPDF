// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures warmed adapter rendering, text and links before and after page-reader protection.</summary>
public class HyperPdfAdapterReadBenchmarks
{
    /// <summary>The pages in the existing document benchmark fixture.</summary>
    private const int Pages = 50;

    /// <summary>The warmed page.</summary>
    private const int PageIndex = Pages - 1;

    /// <summary>The rendered tile edge.</summary>
    private const int Edge = 256;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The reusable caller pixel buffer.</summary>
    private readonly byte[] _pixels = new byte[Edge * Edge * PixelBytes];

    /// <summary>The adapter opened from the same generated fixture as the document benchmarks.</summary>
    private HyperPdfDocument? _document;

    /// <summary>Opens the fixture and warms the measured paths.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(BenchmarkDrawing.Open(TestPdf.Create(Pages), null, BenchmarkDrawing.CreateServices()), "adapter-read-benchmark.pdf");
        _ = WarmRead();
        _ = WarmRenderAndRead();
    }

    /// <summary>Closes the adapter.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Reads the cached character and link counts through the adapter.</summary>
    /// <returns>The combined count.</returns>
    [Benchmark]
    public int WarmRead() => HyperPdfText.GetCharacterCount(_document!, PageIndex) + HyperPdfNavigation.GetLinks(_document!, PageIndex).Count;

    /// <summary>Renders a cached tile and reads its cached character and link counts.</summary>
    /// <returns>The combined count and render result.</returns>
    [Benchmark]
    public int WarmRenderAndRead()
    {
        var document = _document!;
        var info = new PageRenderInfo(PageIndex, 1, PageRotation.None, 0, 0, RenderFlags.Annotations);
        var rendered = HyperPdfRendering.Render(document, info, new(_pixels, Edge, Edge, Edge * PixelBytes));
        return (rendered ? 1 : 0) + HyperPdfText.GetCharacterCount(document, PageIndex) + HyperPdfNavigation.GetLinks(document, PageIndex).Count;
    }
}
