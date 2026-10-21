// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Search;

/// <summary>Searches a document page by page on the thread pool, yielding each page's results as soon as they are found.</summary>
public static class DocumentSearch
{
    /// <summary>The most pages prepared and searched at once, which bounds memory and thread-pool use.</summary>
    private const int MaxPagesInFlight = 6;

    /// <summary>Searches every page, starting at <paramref name="startPage"/> and wrapping around.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>Pages with at least one hit, in search order.</returns>
    public static IAsyncEnumerable<SearchPageResult> SearchAsync(IDocument document, string query, SearchOptions options, int startPage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(query);
        return SearchCoreAsync(document, query, options, startPage, cancellationToken);
    }

    /// <summary>Searches one page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">The page.</param>
    /// <returns>The result, or <see langword="null"/> when nothing matched or the document closed.</returns>
    public static SearchPageResult? SearchPage(IDocument document, string query, SearchOptions options, int page)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.IsDisposed)
        {
            return null;
        }

        var matches = new List<TextMatch>();
        document.Find(page, query, options, matches);
        if (matches.Count == 0)
        {
            return null;
        }

        var hits = new SearchHit[matches.Count];
        var bounds = new List<PageRect>();
        for (var i = 0; i < matches.Count; i++)
        {
            bounds.Clear();
            var match = matches[i];
            document.GetTextBounds(page, match.Start, match.Length, bounds);
            hits[i] = new(match, [.. bounds]);
        }

        return new(page, hits);
    }

    /// <summary>The search iterator.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>Pages with at least one hit.</returns>
    private static async IAsyncEnumerable<SearchPageResult> SearchCoreAsync(
        IDocument document,
        string query,
        SearchOptions options,
        int startPage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (query.Length == 0 || document.PageCount == 0)
        {
            yield break;
        }

        var pageCount = document.PageCount;
        startPage = Math.Clamp(startPage, 0, pageCount - 1);
        var window = Math.Clamp(Environment.ProcessorCount, 1, MaxPagesInFlight);

        // One source cancels every in-flight page when the caller stops early, fails or cancels.
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var inFlight = new Queue<Task<SearchPageResult?>>(window);
        var next = 0;
        try
        {
            while (next < pageCount || inFlight.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                while (next < pageCount && inFlight.Count < window)
                {
                    inFlight.Enqueue(StartPage(document, query, options, (startPage + next) % pageCount, source.Token));
                    next++;
                }

                // Results leave in search order even when a later page finishes first.
                var result = await inFlight.Dequeue().ConfigureAwait(false);
                if (result is not null)
                {
                    yield return result;
                }
            }
        }
        finally
        {
            await source.CancelAsync().ConfigureAwait(false);
            await DrainAsync(inFlight).ConfigureAwait(false);
        }
    }

    /// <summary>Starts preparing and searching one page on the thread pool.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">Cancels the page.</param>
    /// <returns>A task giving the page result, or <see langword="null"/> when nothing matched.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<SearchPageResult?> StartPage(IDocument document, string query, SearchOptions options, int page, CancellationToken cancellationToken) =>
        Task.Run(
            async () =>
            {
                await document.PreparePageAsync(page, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return SearchPage(document, query, options, page);
            },
            cancellationToken);

    /// <summary>Waits for every started page to stop, ignoring the cancellation and failures of pages nobody will read.</summary>
    /// <param name="inFlight">The started pages.</param>
    /// <returns>A task completing when no page is still running.</returns>
    private static async Task DrainAsync(Queue<Task<SearchPageResult?>> inFlight)
    {
        while (inFlight.Count > 0)
        {
            try
            {
                _ = await inFlight.Dequeue().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or InvalidOperationException)
            {
                // The search already ended, so a stopped page has nothing left to report.
            }
        }
    }
}
