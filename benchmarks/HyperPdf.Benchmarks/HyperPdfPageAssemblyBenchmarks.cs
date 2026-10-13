// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Measures splitting a document, building one from interleaved pages of two, and making pages from images.</summary>
public class HyperPdfPageAssemblyBenchmarks
{
    /// <summary>The pages in the document that is split.</summary>
    private const int Pages = 100;

    /// <summary>The pages in each part of the split.</summary>
    private const int PagesPerPart = 10;

    /// <summary>The pages in each document that is interleaved.</summary>
    private const int HalfPages = 50;

    /// <summary>The edge of each image in pixels.</summary>
    private const int ImageEdge = 256;

    /// <summary>The images made into pages.</summary>
    private const int Images = 8;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The images that become pages.</summary>
    private readonly PdfPageImage[] _images = new PdfPageImage[Images];

    /// <summary>The document that is split.</summary>
    private PdfDocument _whole = null!;

    /// <summary>The first document of the interleave.</summary>
    private PdfDocument _first = null!;

    /// <summary>The second document of the interleave.</summary>
    private PdfDocument _second = null!;

    /// <summary>The pages of the interleave.</summary>
    private PdfPageReference[] _interleaved = [];

    /// <summary>The parts of the split.</summary>
    private PdfSplitPart[] _parts = [];

    /// <summary>Opens the fixtures and warms each operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _whole = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        _first = PdfDocumentReader.Open(TestPdf.Create(HalfPages), null);
        _second = PdfDocumentReader.Open(TestPdf.Create(HalfPages), null);
        _interleaved = PdfDocumentCollation.Interleave([HalfPages, HalfPages], 1);
        _parts = PdfDocumentSplitting.PlanByPageCount(_whole, PagesPerPart);
        var pixels = new byte[ImageEdge * ImageEdge * BytesPerPixel];
        pixels.AsSpan().Fill(byte.MaxValue);
        PdfPageImage image = new(pixels, ImageEdge, ImageEdge);
        Array.Fill(_images, image);
        _ = SplitByCount();
        _ = AssembleInterleaved();
        _ = ImagePages();
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _whole.Dispose();
        _first.Dispose();
        _second.Dispose();
    }

    /// <summary>Splits the document into parts of ten pages.</summary>
    /// <returns>The bytes made.</returns>
    [Benchmark]
    public long SplitByCount()
    {
        long bytes = 0;
        foreach (var output in PdfDocumentSplitting.Split(_whole, _parts, CancellationToken.None))
        {
            bytes += output.Bytes.Length;
        }

        return bytes;
    }

    /// <summary>Builds a document from one page of each of two documents in turn.</summary>
    /// <returns>The size of the result.</returns>
    [Benchmark]
    public int AssembleInterleaved() => PdfDocumentAssembly.Assemble([_first, _second], _interleaved, CancellationToken.None).Length;

    /// <summary>Makes a document with a page for each image.</summary>
    /// <returns>The size of the result.</returns>
    [Benchmark]
    public int ImagePages() => PdfImagePages.CreateDocument(_images, PdfImagePageOptions.Default, CancellationToken.None).Length;
}
