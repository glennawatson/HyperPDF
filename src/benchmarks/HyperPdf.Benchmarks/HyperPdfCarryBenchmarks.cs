// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures copying pages with the structures they need: inserting two pages of a plain document (the baseline, with
/// nothing to carry), inserting the two pages of a book with a form, outline, named destinations, labels, a layer and an
/// embedded file, and extracting those pages into a new document.
/// </summary>
public class HyperPdfCarryBenchmarks
{
    /// <summary>The pages in the plain document.</summary>
    private const int PlainPages = 4;

    /// <summary>The pages copied.</summary>
    private static readonly int[] CopiedPages = [0, 1];

    /// <summary>The document pages are inserted into.</summary>
    private PdfDocument? _target;

    /// <summary>The plain document pages are copied from.</summary>
    private PdfDocument? _plain;

    /// <summary>The book pages are copied from.</summary>
    private PdfDocument? _book;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _target = PdfDocumentReader.Open(TestPdf.Create(PlainPages), null);
        _plain = PdfDocumentReader.Open(TestPdf.Create(PlainPages), null);
        _book = PdfDocumentReader.Open(CarryTestDocuments.CreateBook(), null);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _target?.Dispose();
        _plain?.Dispose();
        _book?.Dispose();
    }

    /// <summary>Inserts two pages that have no structures to carry, then undoes it.</summary>
    /// <returns>The page count after the insertion.</returns>
    [Benchmark(Baseline = true)]
    public int InsertPlain()
    {
        var target = _target!;
        PdfDocumentPageOperations.InsertPages(target, target.PageCount, _plain!, CopiedPages);
        var count = target.PageCount;
        _ = PdfDocumentEditing.Undo(target);
        return count;
    }

    /// <summary>Inserts the two pages of the book, carrying its form, outline, destinations, labels, layer and file, then undoes it.</summary>
    /// <returns>The page count after the insertion.</returns>
    [Benchmark]
    public int InsertBook()
    {
        var target = _target!;
        PdfDocumentPageOperations.InsertPages(target, target.PageCount, _book!, CopiedPages);
        var count = target.PageCount;
        _ = PdfDocumentEditing.Undo(target);
        return count;
    }

    /// <summary>Extracts the two pages of the book into a new document, carrying the same structures.</summary>
    /// <returns>The length of the new file.</returns>
    [Benchmark]
    public int ExtractBook() => PdfDocumentPageOperations.ExtractPages(_book!, CopiedPages).Length;
}
