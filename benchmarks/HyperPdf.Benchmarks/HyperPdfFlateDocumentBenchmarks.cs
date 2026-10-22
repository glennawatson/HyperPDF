// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the save and PDF/A check paths on a document whose page content is Flate compressed, so every page stream
/// needs decoding to be read, and whose page tree is large enough for a walk of it to show.
/// </summary>
public class HyperPdfFlateDocumentBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int Pages = 400;

    /// <summary>The lines of text on each page.</summary>
    private const int LinesPerPage = 40;

    /// <summary>The document with Flate compressed page content.</summary>
    private PdfDocument _document = null!;

    /// <summary>Gets the edit made by the incremental save benchmark.</summary>
    private static PdfMetadataEdit Edit => new() { Title = "Benchmark title", Author = "Benchmark author" };

    /// <summary>Builds the document outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        using var plain = PdfDocumentReader.Open(TestPdf.CreateTextFilled(Pages, LinesPerPage), null);
        var compressed = PdfCompactWriter.Save(plain.Objects, PdfCompactOptions.Default with { StreamMode = PdfStreamMode.Recompress });
        _document = PdfDocumentReader.Open(compressed, null);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Rewrites the document with the default stream mode, which checks every filtered stream for damage.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] CompactDefault() => PdfCompactWriter.Save(_document.Objects, PdfCompactOptions.Default);

    /// <summary>Rewrites the document copying every stream as it is.</summary>
    /// <returns>The saved file.</returns>
    [Benchmark]
    public byte[] CompactPreserve() => PdfCompactWriter.Save(_document.Objects, PdfCompactOptions.Default with { StreamMode = PdfStreamMode.Preserve });

    /// <summary>Edits the document's properties, saves the change incrementally, then undoes the edit.</summary>
    /// <returns>The saved length.</returns>
    [Benchmark]
    public int IncrementalSaveAfterMetadataEdit()
    {
        PdfDocumentMetadataEditing.SetMetadata(_document, Edit);
        var length = PdfIncrementalWriter.Save(_document.Objects).Length;
        _ = PdfDocumentEditing.Undo(_document);
        return length;
    }

    /// <summary>Decodes the content stream of every page once, which is the work each PDF/A rule area repeats.</summary>
    /// <returns>The decoded bytes.</returns>
    [Benchmark]
    public long DecodeEveryPageOnce()
    {
        long total = 0;
        for (var i = 0; i < _document.PageCount; i++)
        {
            using var lease = PdfPageContentScope.Lease(_document, PdfDocumentPages.GetPage(_document, i), CancellationToken.None);
            total += lease.Length;
        }

        return total;
    }

    /// <summary>Checks the document against PDF/A-2b.</summary>
    /// <returns>The number of rules that failed.</returns>
    [Benchmark]
    public int ValidatePdfA2B() => PdfAValidator.Validate(_document, PdfALevel.PdfA2B).Failed.Length;
}
