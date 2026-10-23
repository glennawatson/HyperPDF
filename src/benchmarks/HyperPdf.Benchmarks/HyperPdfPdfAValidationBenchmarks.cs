// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures <see cref="PdfAValidator"/> running every rule area: a 100-page text document checked against PDF/A-1b and
/// PDF/A-2b, and a document with form fields, annotations and outlines checked against PDF/A-2b and PDF/A-4.
/// </summary>
public class HyperPdfPdfAValidationBenchmarks
{
    /// <summary>The pages in the text document.</summary>
    private const int Pages = 100;

    /// <summary>The text document.</summary>
    private PdfDocument _text = null!;

    /// <summary>The form and outline document.</summary>
    private PdfDocument _book = null!;

    /// <summary>Opens the documents outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _text = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        _book = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _text.Dispose();
        _book.Dispose();
    }

    /// <summary>Checks the text document against PDF/A-2b.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark(Baseline = true)]
    public int TextPdfA2B() => PdfAValidator.Validate(_text, PdfALevel.PdfA2B).Failed.Length;

    /// <summary>Checks the text document against PDF/A-1b.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark]
    public int TextPdfA1B() => PdfAValidator.Validate(_text, PdfALevel.PdfA1B).Failed.Length;

    /// <summary>Checks the form and outline document against PDF/A-2b.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark]
    public int BookPdfA2B() => PdfAValidator.Validate(_book, PdfALevel.PdfA2B).Failed.Length;

    /// <summary>Checks the form and outline document against PDF/A-4.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark]
    public int BookPdfA4() => PdfAValidator.Validate(_book, PdfALevel.PdfA4).Failed.Length;
}
