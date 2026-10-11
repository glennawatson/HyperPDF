// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures warmed page-label lookups at short, long and prefixed Roman values, upper-case letters and prefixed decimals.</summary>
public class HyperPdfLabelBenchmarks
{
    /// <summary>The second zero-based page index.</summary>
    private const int SecondPage = 2;

    /// <summary>The third zero-based page index.</summary>
    private const int ThirdPage = 3;

    /// <summary>The fourth zero-based page index.</summary>
    private const int FourthPage = 4;

    /// <summary>The fifth zero-based page index.</summary>
    private const int FifthPage = 5;

    /// <summary>A page dictionary shared by the label fixture.</summary>
    private const string Page = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>The document with four Roman label ranges.</summary>
    private PdfDocument? _document;

    /// <summary>Opens and warms the label ranges.</summary>
    /// <exception cref="InvalidOperationException">The fixture does not have the intended labels.</exception>
    [GlobalSetup]
    public void Setup()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /PageLabels << /Nums [0 << /S /r >> 1 << /S /r /St 3888 >> 2 << /S /R /St 944 >> 3 << /S /R /P (Part-) /St 49 >> "
            + "4 << /S /A /St 28 >> 5 << /S /D /P (p.) /St 7 >>] >> >>",
            "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R 7 0 R 8 0 R] /Count 6 >>",
            Page,
            Page,
            Page,
            Page,
            Page,
            Page);
        _document = PdfDocumentReader.Open(bytes, null);
        if (PdfDocumentLabels.GetPageLabel(_document, 0) != "i"
            || PdfDocumentLabels.GetPageLabel(_document, 1) != "mmmdccclxxxviii"
            || PdfDocumentLabels.GetPageLabel(_document, SecondPage) != "CMXLIV"
            || PdfDocumentLabels.GetPageLabel(_document, ThirdPage) != "Part-XLIX"
            || PdfDocumentLabels.GetPageLabel(_document, FourthPage) != "BB"
            || PdfDocumentLabels.GetPageLabel(_document, FifthPage) != "p.7")
        {
            throw new InvalidOperationException("Page label fixture did not load as expected.");
        }
    }

    /// <summary>Closes the fixture.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Reads the short lower-case label.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? ShortLower() => PdfDocumentLabels.GetPageLabel(_document!, 0);

    /// <summary>Reads a longer lower-case label.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? LongLower() => PdfDocumentLabels.GetPageLabel(_document!, 1);

    /// <summary>Reads an upper-case label with subtractive symbols.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? Upper() => PdfDocumentLabels.GetPageLabel(_document!, SecondPage);

    /// <summary>Reads a prefixed upper-case label.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? PrefixedUpper() => PdfDocumentLabels.GetPageLabel(_document!, ThirdPage);

    /// <summary>Reads an upper-case letter label.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? UpperLetters() => PdfDocumentLabels.GetPageLabel(_document!, FourthPage);

    /// <summary>Reads a prefixed decimal label.</summary>
    /// <returns>The label.</returns>
    [Benchmark]
    public string? PrefixedDecimal() => PdfDocumentLabels.GetPageLabel(_document!, FifthPage);
}
