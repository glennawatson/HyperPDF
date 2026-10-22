// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Benchmarks;

/// <summary>Reads the label of every page of a 5,000 page document that has 500 page label ranges, as a page strip does the first time it lists the pages.</summary>
public class HyperPdfPageLabelBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 5000;

    /// <summary>The pages in each label range.</summary>
    private const int RangeLength = 10;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPage = 3;

    /// <summary>The document's bytes, opened afresh for each measured read so the labels are read cold.</summary>
    private byte[] _pdf = [];

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup() => _pdf = Build();

    /// <summary>Opens the document and reads the label of every page.</summary>
    /// <returns>The characters in all the labels.</returns>
    [Benchmark]
    public int ReadEveryLabel()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        var total = 0;
        for (var i = 0; i < Pages; i++)
        {
            total += PdfDocumentLabels.GetPageLabel(document, i)?.Length ?? 0;
        }

        return total;
    }

    /// <summary>Opens the document and does nothing else, which is the cost the label read includes.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int OpenOnly()
    {
        using var document = PdfDocumentReader.Open(_pdf, null);
        return document.PageCount;
    }

    /// <summary>Builds the document.</summary>
    /// <returns>The file.</returns>
    private static byte[] Build()
    {
        var kids = new StringBuilder();
        for (var i = 0; i < Pages; i++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{FirstPage + i} 0 R ");
        }

        var numbers = new StringBuilder();
        for (var start = 0; start < Pages; start += RangeLength)
        {
            _ = numbers.Append(CultureInfo.InvariantCulture, $"{start} << /S /D /P (S{start / RangeLength}-) /St 1 >> ");
        }

        List<string> objects =
        [
            $"<< /Type /Catalog /Pages 2 0 R /PageLabels << /Nums [{numbers}] >> >>",
            $"<< /Type /Pages /Kids [{kids}] /Count {Pages} >>",
        ];
        for (var i = 0; i < Pages; i++)
        {
            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");
        }

        return MiniPdf.Build([.. objects]);
    }
}
