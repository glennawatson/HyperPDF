// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Reads the page content of a document opened from a file, one page at a time and from eight threads at once, as the viewer's render threads do.</summary>
public class HyperPdfFileBackedDocumentBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 400;

    /// <summary>The lines of text on each page.</summary>
    private const int LinesPerPage = 40;

    /// <summary>The threads that read pages at once.</summary>
    private const int Threads = 8;

    /// <summary>The path of the file the document is read from.</summary>
    private string _path = string.Empty;

    /// <summary>The document opened from the file.</summary>
    private PdfDocument _document = null!;

    /// <summary>Writes the file and opens it outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var plain = PdfDocumentReader.Open(TestPdf.CreateTextFilled(Pages, LinesPerPage), null);
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-file-bench-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(_path, PdfCompactWriter.Save(plain.Objects, PdfCompactOptions.Default with { StreamMode = PdfStreamMode.Recompress }));
        _document = PdfDocumentReader.Open(_path, null);
    }

    /// <summary>Closes the document and deletes the file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Decodes the content of every page on one thread.</summary>
    /// <returns>The decoded bytes.</returns>
    [Benchmark(Baseline = true)]
    public long DecodeEveryPage()
    {
        long total = 0;
        for (var i = 0; i < Pages; i++)
        {
            total += DecodePage(i);
        }

        return total;
    }

    /// <summary>Decodes the content of every page from eight threads at once.</summary>
    /// <returns>The decoded bytes.</returns>
    [Benchmark]
    public long DecodeEveryPageOnEightThreads()
    {
        long total = 0;
        _ = Parallel.For(0, Pages, new ParallelOptions { MaxDegreeOfParallelism = Threads }, i => Interlocked.Add(ref total, DecodePage(i)));
        return total;
    }

    /// <summary>Decodes one page's content.</summary>
    /// <param name="index">The page index.</param>
    /// <returns>The decoded length.</returns>
    private long DecodePage(int index)
    {
        using var lease = PdfPageContentScope.Lease(_document, PdfDocumentPages.GetPage(_document, index), CancellationToken.None);
        return lease.Length;
    }
}
