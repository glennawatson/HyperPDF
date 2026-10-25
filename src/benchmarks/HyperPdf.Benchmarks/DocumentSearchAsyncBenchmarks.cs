// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Search;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures <c>DocumentSearch.SearchAsync</c> over a 1,000 page document.</summary>
public class DocumentSearchAsyncBenchmarks
{
    /// <summary>The number of pages searched.</summary>
    private const int Pages = 1000;

    /// <summary>A word on exactly one page.</summary>
    private const string RareTerm = "page500marker";

    /// <summary>A word on every page.</summary>
    private const string CommonTerm = "quick";

    /// <summary>The page the search starts from.</summary>
    private const int StartPage = 250;

    /// <summary>The open document and its temporary file.</summary>
    private BenchmarkPdfDocument _document = null!;

    /// <summary>Opens the 1,000 page document.</summary>
    [GlobalSetup]
    public void Setup() => _document = new(TestPdf.Create(Pages));

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Searches every page for a word found on one page.</summary>
    /// <returns>The number of hits.</returns>
    [Benchmark]
    public Task<int> SearchRareTerm() => CountHitsAsync(RareTerm);

    /// <summary>Searches every page for a word found on every page.</summary>
    /// <returns>The number of hits.</returns>
    [Benchmark]
    public Task<int> SearchCommonTerm() => CountHitsAsync(CommonTerm);

    /// <summary>Searches every page for a word found on every page, as a folder search does, without the rectangles of the hits.</summary>
    /// <returns>The number of hits.</returns>
    [Benchmark]
    public async Task<int> SearchCommonTermWithoutBounds()
    {
        var hits = 0;
        await foreach (var result in DocumentSearch.SearchAsync(_document.Document, CommonTerm, SearchOptions.None, StartPage, false, CancellationToken.None))
        {
            hits += result.Hits.Count;
        }

        return hits;
    }

    /// <summary>Counts the hits of a search that starts partway through the document.</summary>
    /// <param name="query">The query.</param>
    /// <returns>The number of hits.</returns>
    private async Task<int> CountHitsAsync(string query)
    {
        var hits = 0;
        await foreach (var result in DocumentSearch.SearchAsync(_document.Document, query, SearchOptions.None, StartPage, CancellationToken.None))
        {
            hits += result.Hits.Count;
        }

        return hits;
    }
}
