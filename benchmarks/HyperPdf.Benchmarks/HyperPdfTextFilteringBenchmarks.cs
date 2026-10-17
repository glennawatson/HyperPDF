// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Compares unfiltered extraction and opt-in hidden text filtering on the same synthetic page.</summary>
public class HyperPdfTextFilteringBenchmarks
{
    /// <summary>The warmed document.</summary>
    private PdfDocument? _document;

    /// <summary>Gets or sets whether the page includes hidden text and Unicode replacement controls.</summary>
    [Params(false, true)]
    public bool HiddenText { get; set; }

    /// <summary>Opens and warms a document with visible text and optional adversarial segments.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var content = "BT /F1 10 Tf 72 720 Td (A visible accessible sentence) Tj ET";
        if (HiddenText)
        {
            content += " q BT /F1 10 Tf 3 Tr 72 700 Td (Secret) Tj ET Q";
            content += " q BT /F1 0 Tf 72 690 Td (Zero size) Tj ET Q";
            content += " q /Zero gs BT /F1 10 Tf 72 685 Td (Transparent) Tj ET Q";
            content += " q /Zero gs /Fm Do Q";
            content += " q W n BT /F1 10 Tf 72 675 Td (Empty clip) Tj ET Q";
            content += " q 0 0 0 100 re W n BT /F1 10 Tf (Clipped) Tj ET Q";
            content += " /Span << /ActualText <FEFF0041202E0042> >> BDC BT /F1 10 Tf 72 680 Td (X) Tj ET EMC";
        }

        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> /ExtGState << /Zero << /ca 0 /CA 0 >> >> /XObject << /Fm 6 0 R >> >> /Contents 4 0 R >>",
                MiniPdf.Stream(string.Empty, content),
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
                MiniPdf.Stream(
                    "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Group << /S /Transparency /I true >> "
                    + "/Resources << /Font << /F1 5 0 R >> >>",
                    "BT /F1 10 Tf (Grouped hidden text) Tj ET")),
            null);
        _ = PdfDocumentText.GetTextPage(_document, 0);
        _ = PdfDocumentText.GetTextPage(_document, 0, new(true, true));
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document?.Dispose();

    /// <summary>Extracts fresh text through the default OCR-preserving path.</summary>
    /// <returns>The character count.</returns>
    [Benchmark(Baseline = true)]
    public int PreserveText() => PdfDocumentText.ExtractText(_document!, 0).CharCount;

    /// <summary>Extracts fresh text with both explicit filters enabled.</summary>
    /// <returns>The character count.</returns>
    [Benchmark]
    public int FilterText() => PdfDocumentText.GetTextPage(_document!, 0, new(true, true)).CharCount;
}
