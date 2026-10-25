// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>Measures encoding of PDF text strings: ASCII text, Latin-1 and special PDFDocEncoding characters, and text that needs UTF-16 or UTF-8.</summary>
public class HyperPdfTextStringBenchmarks
{
    /// <summary>Plain ASCII text.</summary>
    private const string Ascii = "Quarterly report for the finance committee";

    /// <summary>Text with PDFDocEncoding characters outside ASCII.</summary>
    private const string Latin = "Café menu €12 • déjà vu";

    /// <summary>Text that needs Unicode.</summary>
    private const string Unicode = "Ω mega 中文 text";

    /// <summary>The header version of a PDF 1.7 document.</summary>
    private const string Version17 = "1.7";

    /// <summary>The header version of a PDF 2.0 document.</summary>
    private const string Version20 = "2.0";

    /// <summary>Encodes ASCII text.</summary>
    /// <returns>The byte count.</returns>
    [Benchmark(Baseline = true)]
    public int EncodeAscii() => PdfText.Encode(Ascii, Version17).Length;

    /// <summary>Encodes Latin-1 and special PDFDocEncoding text.</summary>
    /// <returns>The byte count.</returns>
    [Benchmark]
    public int EncodeLatin() => PdfText.Encode(Latin, Version17).Length;

    /// <summary>Encodes Unicode text as UTF-16BE for a PDF 1.7 document.</summary>
    /// <returns>The byte count.</returns>
    [Benchmark]
    public int EncodeUnicodeUtf16() => PdfText.Encode(Unicode, Version17).Length;

    /// <summary>Encodes Unicode text as UTF-8 for a PDF 2.0 document.</summary>
    /// <returns>The byte count.</returns>
    [Benchmark]
    public int EncodeUnicodeUtf8() => PdfText.Encode(Unicode, Version20).Length;
}
