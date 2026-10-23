// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Saves a document whose page resources hold one dictionary of many entries, to show how the cost of writing a dictionary grows with its size.</summary>
public class HyperPdfLargeDictionarySaveBenchmarks
{
    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Gets or sets the entries in the dictionary.</summary>
    [Params(50, 2000)]
    public int Entries { get; set; }

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var properties = new StringBuilder();
        for (var i = 0; i < Entries; i++)
        {
            _ = properties.Append(CultureInfo.InvariantCulture, $"/P{i} {i} ");
        }

        _document = PdfDocumentReader.Open(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Properties << {properties}>> >> >>"),
            null);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Writes the document with a classic table.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int SaveClassic() => PdfCompactWriter.Save(_document.Objects, PdfCompactOptions.Classic).Length;
}
