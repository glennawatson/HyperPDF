// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Tests;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures what the tab does at once when the canvas reports a new current page, the work a fast scroll or scrollbar drag
/// repeats every few milliseconds. The thumbnail selection that follows the page is left until the page settles.
/// </summary>
public class PageChangeFollowBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int PageCount = 200;

    /// <summary>The text lines on each page.</summary>
    private const int TextLines = 40;

    /// <summary>How many pages one report jumps: a third of the document.</summary>
    private const int Jump = PageCount / 3;

    /// <summary>The services of the document's tab.</summary>
    private TestServices _services = null!;

    /// <summary>The view model that owns the tab.</summary>
    private MainViewModel _main = null!;

    /// <summary>The tab under test.</summary>
    private DocumentTabViewModel _tab = null!;

    /// <summary>The page the next call starts from.</summary>
    private int _page;

    /// <summary>Opens the document and shows its thumbnails list.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _services = new();
        _main = new(_services.Services);
        await TestServices.OpenAndWaitAsync(_main, [_services.CreateDocument("page-change.pdf", TestPdf.CreateTextFilled(PageCount, TextLines))]);
        _tab = _main.SelectedTab!;
        _ = _tab.Pages;
    }

    /// <summary>Closes the document and its services.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _main.Dispose();
        _services.Dispose();
    }

    /// <summary>Reports a jump of many pages, as one step of a scrollbar drag does.</summary>
    /// <returns>The page now shown.</returns>
    [Benchmark]
    public int ReportFarPage()
    {
        _page = (_page + Jump) % PageCount;
        _tab.ReportPosition(new(_page, 0), _page);
        return _tab.CurrentPageIndex;
    }
}
