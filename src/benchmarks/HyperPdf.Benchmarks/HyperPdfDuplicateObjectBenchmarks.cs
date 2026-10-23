// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>Optimises a document of 600 pages that each carry their own copy of the same font and graphics state, as a document stitched from many exports does.</summary>
public class HyperPdfDuplicateObjectBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 600;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPage = 3;

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup() => _document = PdfDocumentReader.Open(Build(), null);

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Optimises the document with the lossless preset, which merges the duplicate objects.</summary>
    /// <returns>The bytes written.</returns>
    [Benchmark]
    public long MergeDuplicates() => PdfOptimizer.Optimize(_document, Stream.Null, PdfOptimizeOptions.KeepQuality).BytesAfter;

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
        const int fonts = FirstPage + Pages;
        const int states = fonts + Pages;
        for (var i = 0; i < Pages; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {fonts + i} 0 R >> /ExtGState << /G {states + i} 0 R >> >> >>");
        }

        for (var i = 0; i < Pages; i++)
        {
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        }

        for (var i = 0; i < Pages; i++)
        {
            objects.Add("<< /Type /ExtGState /CA 0.5 /ca 0.5 /LW 2 /LC 1 >>");
        }

        return MiniPdf.Build([.. objects]);
    }
}
