// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Pdf20;
using HyperPdfLibrary.Tests.Pdf20;

namespace HyperPdf.Benchmarks;

/// <summary>Measures saving a PDF 2.0 copy: moving the document information into XMP, removing deprecated entries, and copying a long document.</summary>
public class HyperPdfPdf20ConversionBenchmarks
{
    /// <summary>The pages in the long document.</summary>
    private const int Pages = 100;

    /// <summary>A page with every standard and custom information entry and no XMP.</summary>
    private PdfDocument _info = null!;

    /// <summary>A page that uses many deprecated entries.</summary>
    private PdfDocument _deprecated = null!;

    /// <summary>A 100-page document with an information dictionary.</summary>
    private PdfDocument _book = null!;

    /// <summary>Opens the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _info = PdfDocumentReader.Open(Pdf20TestPdf.InfoOnly(), null);
        _deprecated = PdfDocumentReader.Open(Pdf20TestPdf.DeprecatedEntries(), null);
        _book = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _info.Dispose();
        _deprecated.Dispose();
        _book.Dispose();
    }

    /// <summary>Copies the page whose information moves into a new XMP packet.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark(Baseline = true)]
    public long InfoToXmp() => Convert(_info);

    /// <summary>Copies the page that uses many deprecated entries.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long DeprecatedEntries() => Convert(_deprecated);

    /// <summary>Copies the 100-page document.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long HundredPages() => Convert(_book);

    /// <summary>Copies a document into memory.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The bytes written.</returns>
    private static long Convert(PdfDocument document)
    {
        using var stream = new MemoryStream();
        _ = Pdf20Converter.SaveAsPdf20(document, stream);
        return stream.Length;
    }
}
