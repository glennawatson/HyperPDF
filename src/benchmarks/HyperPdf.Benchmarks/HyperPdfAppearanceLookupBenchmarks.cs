// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdf.Benchmarks;

/// <summary>Finds the normal appearance stream of an annotation, which the page recorder does for each annotation it draws.</summary>
public class HyperPdfAppearanceLookupBenchmarks
{
    /// <summary>The annotation's dictionary.</summary>
    private PdfDictionary _annotation = null!;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The document's name table.</summary>
    private PdfNameTable _names = null!;

    /// <summary>Opens a document with one annotation that has an appearance stream.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [5 0 R] >>",
                MiniPdf.Stream(string.Empty, string.Empty),
                "<< /Type /Annot /Subtype /Square /Rect [10 10 100 100] /AP << /N 6 0 R >> >>",
                MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 90 90]", "0 0 90 90 re S")),
            null);
        _annotation = PdfDocumentPages.GetPage(_document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(0)!;
        _names = new();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Finds the appearance stream.</summary>
    /// <returns>Whether a stream was found.</returns>
    [Benchmark]
    public bool GetAppearance() => PageRecorder.GetAppearance(_annotation, _names) is not null;
}
