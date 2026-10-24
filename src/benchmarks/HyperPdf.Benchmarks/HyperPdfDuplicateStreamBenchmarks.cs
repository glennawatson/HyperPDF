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

/// <summary>Looks for duplicate objects in a document of 200 pages that each carry their own 64 KB form, ten of which repeat an earlier form's bytes.</summary>
public class HyperPdfDuplicateStreamBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 200;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPage = 3;

    /// <summary>The bytes in each form.</summary>
    private const int FormBytes = 64 * 1024;

    /// <summary>The pages at the end whose forms repeat an earlier form.</summary>
    private const int Repeats = 10;

    /// <summary>The letters the form data is made of.</summary>
    private const int Alphabet = 26;

    /// <summary>The multiplier that mixes the form number and position into letters.</summary>
    private const int Mix = 31;

    /// <summary>The report the finder records its savings in, kept so each run does not build one.</summary>
    private readonly OptimizeReportBuilder _report = new();

    /// <summary>The document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The scratch objects the optimizer keeps between runs.</summary>
    private OptimizerResources _resources = null!;

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(Build(), null);
        _resources = new();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Finds the duplicates.</summary>
    /// <returns>The original of the last page's form, which is zero when it is not a duplicate.</returns>
    [Benchmark]
    public int FindDuplicates()
    {
        var store = _document.Objects;
        var graph = OptimizerGraph.Collect(store, false, null, _resources);
        var finder = DuplicateFinder.Find(store, graph, _report, CancellationToken.None);
        try
        {
            return finder.Aliases[FirstPage + Pages + Pages - 1];
        }
        finally
        {
            finder.Return();
            graph.Return();
        }
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

        List<string> objects = ["<< /Type /Catalog /Pages 2 0 R >>", $"<< /Type /Pages /Kids [{kids}] /Count {Pages} >>"];
        const int forms = FirstPage + Pages;
        for (var i = 0; i < Pages; i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /X {forms + i} 0 R >> >> >>");
        }

        for (var i = 0; i < Pages; i++)
        {
            var seed = i >= Pages - Repeats ? i - (Pages - Repeats) : i;
            objects.Add(MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 1 1]", FormData(seed)));
        }

        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Makes the data of a form, different for each seed.</summary>
    /// <param name="seed">The seed.</param>
    /// <returns>The data, one byte per character.</returns>
    private static string FormData(int seed) =>
        string.Create(FormBytes + seed, seed, static (span, value) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)('a' + (((i * Mix) + (value * Mix * Mix) + (i / Alphabet * value)) % Alphabet));
            }
        });
}
