// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading the links of pages whose links are kept, which is what the first pointer move over a page costs once the
/// neighbour preparer has read them away from the UI thread.
/// </summary>
public class PageLinksReadBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int PageCount = 40;

    /// <summary>The text lines on each page.</summary>
    private const int TextLines = 40;

    /// <summary>A document whose links were all read already.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>Opens a document and reads the links of every page once, as the preparer does.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateTextFilled(PageCount, TextLines));
        _ = ReadAll();
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the links of every page whose links are kept.</summary>
    /// <returns>The links found.</returns>
    [Benchmark]
    public int ReadKeptPages() => ReadAll();

    /// <summary>Reads the links of every page.</summary>
    /// <returns>The links found.</returns>
    private int ReadAll()
    {
        var found = 0;
        for (var page = 0; page < PageCount; page++)
        {
            found += _document.Document.GetLinks(page).Count;
        }

        return found;
    }
}
