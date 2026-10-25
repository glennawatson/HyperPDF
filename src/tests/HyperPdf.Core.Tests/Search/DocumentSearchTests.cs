// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Search;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Core.Tests.Search;

/// <summary>Tests for <see cref="DocumentSearch"/>.</summary>
public sealed class DocumentSearchTests
{
    /// <summary>The file path of every fake document.</summary>
    private const string FilePath = "a.pdf";

    /// <summary>A word on every page of a plain fake document.</summary>
    private const string Alpha = "alpha";

    /// <summary>The page count of the differential and ordering documents.</summary>
    private const int ManyPages = 40;

    /// <summary>The page count of the cancellation and disposal documents.</summary>
    private const int LongDocumentPages = 200;

    /// <summary>Every seventh page holds the rare word.</summary>
    private const int RareEvery = 7;

    /// <summary>Every fifth page, offset by <see cref="NoCommonOffset"/>, has no common word.</summary>
    private const int NoCommonEvery = 5;

    /// <summary>The offset of the pages without the common word.</summary>
    private const int NoCommonOffset = 4;

    /// <summary>The longest delay used to shuffle the order pages finish in.</summary>
    private const int DelayModulo = 3;

    /// <summary>Verifies every page is searched starting from the requested page and wrapping.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SearchesFromStartPageAndWraps()
    {
        const int startPage = 2;
        const int matchesPerPage = 2;
        var document = new FakeDocument(FilePath, FakeEngine.A4, FakeEngine.A4, FakeEngine.A4);
        var pages = new List<int>();
        var total = 0;

        await foreach (var result in DocumentSearch.SearchAsync(document, "ALPHA", SearchOptions.None, startPage, CancellationToken.None))
        {
            pages.Add(result.PageIndex);
            total += result.Hits.Count;
        }

        await Assert.That(pages).IsEquivalentTo([startPage, 0, 1]);
        await Assert.That(pages[0]).IsEqualTo(startPage);
        await Assert.That(total).IsEqualTo(document.PageCount * matchesPerPage);
    }

    /// <summary>Verifies case sensitive searches that match nothing yield nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchCaseFiltersResults()
    {
        var document = new FakeDocument(FilePath, FakeEngine.A4);
        var count = 0;

        await foreach (var unused in DocumentSearch.SearchAsync(document, "ALPHA", SearchOptions.MatchCase, 0, CancellationToken.None))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    /// <summary>Verifies parallel search gives exactly the sequential results for several terms, options and start pages.</summary>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="startPage">The start page.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("rare", SearchOptions.None, 0)]
    [Arguments("rare", SearchOptions.MatchCase, 17)]
    [Arguments("common", SearchOptions.None, 0)]
    [Arguments("common", SearchOptions.MatchCase, 39)]
    [Arguments("Common", SearchOptions.WholeWord, 5)]
    [Arguments("missing", SearchOptions.IgnoreDiacritics, 3)]
    [Arguments("common", SearchOptions.None, 400)]
    public async Task ParallelSearchMatchesSequentialSearch(string query, SearchOptions options, int startPage)
    {
        var document = CreateTextDocument(ManyPages);
        document.Preparation = static async (page, token) => await Task.Delay(page % DelayModulo, token).ConfigureAwait(false);

        var expected = new List<string>();
        var clamped = Math.Clamp(startPage, 0, document.PageCount - 1);
        for (var n = 0; n < document.PageCount; n++)
        {
            if (DocumentSearch.SearchPage(document, query, options, (clamped + n) % document.PageCount) is { } sequential)
            {
                expected.Add(Describe(sequential));
            }
        }

        var actual = new List<string>();
        await foreach (var result in DocumentSearch.SearchAsync(document, query, options, startPage, CancellationToken.None))
        {
            actual.Add(Describe(result));
        }

        await Assert.That(string.Join('|', actual)).IsEqualTo(string.Join('|', expected));
        await Assert.That(actual.Count).IsEqualTo(expected.Count);
    }

    /// <summary>Verifies results stay in search order when later pages finish first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResultsStayInOrderWhenLaterPagesFinishFirst()
    {
        const int pages = 12;
        const int delayStep = 2;
        var document = new FakeDocument(FilePath, [.. Enumerable.Repeat(FakeEngine.A4, pages)])
        {
            Preparation = static async (page, token) => await Task.Delay((pages - page) * delayStep, token).ConfigureAwait(false),
        };
        var seen = new List<int>();

        await foreach (var result in DocumentSearch.SearchAsync(document, Alpha, SearchOptions.None, 0, CancellationToken.None))
        {
            seen.Add(result.PageIndex);
        }

        await Assert.That(string.Join(',', seen)).IsEqualTo(string.Join(',', Enumerable.Range(0, pages)));
    }

    /// <summary>Verifies a search with no hits yields nothing and an empty query yields nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyResultsYieldNothing()
    {
        const int pages = 10;
        var document = CreateTextDocument(pages);
        var count = 0;

        await foreach (var unused in DocumentSearch.SearchAsync(document, "absent", SearchOptions.None, 0, CancellationToken.None))
        {
            count++;
        }

        await foreach (var unused in DocumentSearch.SearchAsync(document, string.Empty, SearchOptions.None, 0, CancellationToken.None))
        {
            count++;
        }

        await Assert.That(count).IsEqualTo(0);
    }

    /// <summary>Verifies cancelling mid-search throws and stops all in-flight pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationMidSearchStopsInFlightPages()
    {
        var counters = new PreparationCounters();
        var document = CreateBlockedDocument(counters);
        using var source = new CancellationTokenSource();
        var thrown = false;

        try
        {
            await foreach (var unused in DocumentSearch.SearchAsync(document, Alpha, SearchOptions.None, 0, source.Token))
            {
                await source.CancelAsync();
            }
        }
        catch (OperationCanceledException)
        {
            thrown = true;
        }

        await Assert.That(thrown).IsTrue();
        await Assert.That(counters.Running).IsEqualTo(0);
        await Assert.That(counters.Started).IsLessThan(LongDocumentPages);
    }

    /// <summary>Verifies stopping the enumeration early stops in-flight pages before disposal returns.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StoppingEarlyLeavesNoPageRunning()
    {
        var counters = new PreparationCounters();
        var document = CreateBlockedDocument(counters);

        var enumerator = DocumentSearch.SearchAsync(document, Alpha, SearchOptions.None, 0, CancellationToken.None).GetAsyncEnumerator();
        var moved = await enumerator.MoveNextAsync();
        await enumerator.DisposeAsync();

        await Assert.That(moved).IsTrue();
        await Assert.That(counters.Running).IsEqualTo(0);
    }

    /// <summary>Verifies disposing the document during a search ends it without errors or hits from closed pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposingDocumentDuringSearchEndsSearch()
    {
        const int pages = 50;
        var document = new FakeDocument(FilePath, [.. Enumerable.Repeat(FakeEngine.A4, pages)]);
        var seen = 0;

        await foreach (var unused in DocumentSearch.SearchAsync(document, Alpha, SearchOptions.None, 0, CancellationToken.None))
        {
            seen++;
            document.Dispose();
        }

        await Assert.That(seen).IsGreaterThanOrEqualTo(1);
        await Assert.That(seen).IsLessThan(pages);
    }

    /// <summary>Creates a document where "common" is on most pages and "rare" is on a few.</summary>
    /// <param name="pages">The page count.</param>
    /// <returns>The document.</returns>
    private static FakeDocument CreateTextDocument(int pages) =>
        new(FilePath, [.. Enumerable.Repeat(FakeEngine.A4, pages)]) { PageTextFor = TextOfPage };

    /// <summary>Creates a document whose first page finishes preparing at once and whose others wait until cancelled.</summary>
    /// <param name="counters">Counts the preparations.</param>
    /// <returns>The document.</returns>
    private static FakeDocument CreateBlockedDocument(PreparationCounters counters) =>
        new(FilePath, [.. Enumerable.Repeat(FakeEngine.A4, LongDocumentPages)])
        {
            Preparation = async (page, token) =>
            {
                counters.Begin();
                try
                {
                    await Task.Delay(page == 0 ? 1 : Timeout.Infinite, token).ConfigureAwait(false);
                }
                finally
                {
                    counters.End();
                }
            },
        };

    /// <summary>Gets the text of one page in the differential document.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The text.</returns>
    private static string TextOfPage(int page)
    {
        var unsigned = (uint)page;
        var rare = unsigned % RareEvery == 0 ? "Rare thing. " : string.Empty;
        var common = unsigned % NoCommonEvery == NoCommonOffset ? string.Empty : "common Common commonly café ";
        return $"{rare}{common}end";
    }

    /// <summary>Describes a page result so two results can be compared exactly.</summary>
    /// <param name="result">The result.</param>
    /// <returns>A text form of the page, hits and bounds.</returns>
    private static string Describe(SearchPageResult result) =>
        $"{result.PageIndex}:{string.Join(',', result.Hits.Select(static hit => $"{hit.Match}[{string.Join(';', hit.Bounds)}]"))}";

    /// <summary>Counts how many page preparations started and how many are still running.</summary>
    private sealed class PreparationCounters
    {
        /// <summary>The number of preparations started.</summary>
        private int _started;

        /// <summary>The number of preparations still running.</summary>
        private int _running;

        /// <summary>Gets the number of preparations started.</summary>
        internal int Started => Volatile.Read(ref _started);

        /// <summary>Gets the number of preparations still running.</summary>
        internal int Running => Volatile.Read(ref _running);

        /// <summary>Records a preparation starting.</summary>
        internal void Begin()
        {
            _ = Interlocked.Increment(ref _started);
            _ = Interlocked.Increment(ref _running);
        }

        /// <summary>Records a preparation stopping.</summary>
        internal void End() => _ = Interlocked.Decrement(ref _running);
    }
}
