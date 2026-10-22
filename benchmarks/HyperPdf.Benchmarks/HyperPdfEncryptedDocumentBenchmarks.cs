// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading the page content of an AES-128 encrypted document one page at a time and from several threads at once.</summary>
public class HyperPdfEncryptedDocumentBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 400;

    /// <summary>The lines of text on each page.</summary>
    private const int LinesPerPage = 40;

    /// <summary>The threads that read pages at once.</summary>
    private const int Threads = 8;

    /// <summary>The password of the document.</summary>
    private const string Password = "benchmark";

    /// <summary>The encrypted document.</summary>
    private PdfDocument _document = null!;

    /// <summary>Encrypts a document and opens it outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var plain = PdfDocumentReader.Open(TestPdf.CreateTextFilled(Pages, LinesPerPage), null);
        var options = PdfCompactOptions.Default with { Encryption = new() { Algorithm = PdfEncryptionAlgorithm.Aes128, UserPassword = Password, OwnerPassword = Password } };
        _document = PdfDocumentReader.Open(PdfCompactWriter.Save(plain.Objects, options), Password);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

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

    /// <summary>Decodes the content of every page from several threads at once, as the viewer's render threads do.</summary>
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
