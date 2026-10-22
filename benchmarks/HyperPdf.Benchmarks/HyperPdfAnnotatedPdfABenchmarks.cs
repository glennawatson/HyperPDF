// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Checks a document of 400 pages that each carry five link annotations against PDF/A-2b, which reads every page's annotations in several rule areas.</summary>
public class HyperPdfAnnotatedPdfABenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 400;

    /// <summary>The annotations on each page.</summary>
    private const int AnnotationsPerPage = 5;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPage = 3;

    /// <summary>The height of a link rectangle.</summary>
    private const int LinkHeight = 10;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(Build(), null);

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Checks the document against PDF/A-2b.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark]
    public int ValidatePdfA2B() => PdfAValidator.Validate(_document, PdfALevel.PdfA2B).Failed.Length;

    /// <summary>Builds the document.</summary>
    /// <returns>The file.</returns>
    private static byte[] Build()
    {
        var kids = new StringBuilder();
        for (var i = 0; i < Pages; i++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{FirstPage + i} 0 R ");
        }

        List<string> objects = ["<< /Type /Catalog /Pages 2 0 R >>", $"<< /Type /Pages /Kids [{kids}] /Count {Pages} >>"];
        const int firstAnnotation = FirstPage + Pages;
        for (var i = 0; i < Pages; i++)
        {
            var annots = new StringBuilder();
            for (var a = 0; a < AnnotationsPerPage; a++)
            {
                _ = annots.Append(CultureInfo.InvariantCulture, $"{firstAnnotation + (i * AnnotationsPerPage) + a} 0 R ");
            }

            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [{annots}] >>");
        }

        for (var i = 0; i < Pages * AnnotationsPerPage; i++)
        {
            objects.Add($"<< /Type /Annot /Subtype /Link /Rect [10 {i % Pages} 100 {(i % Pages) + LinkHeight}] /F 4 /Border [0 0 0] /A << /S /URI /URI (https://example.com/{i}) >> >>");
        }

        return MiniPdf.Build([.. objects]);
    }
}
