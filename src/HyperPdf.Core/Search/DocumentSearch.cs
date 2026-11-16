// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

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
    /// <returns>A cold signal giving the pages with at least one hit, in search order; unsubscribing cancels the search.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IObservable<SearchPageResult> Search(IDocument document, string query, SearchOptions options, int startPage) =>
        Search(document, query, options, startPage, true);

    /// <summary>Searches every page, starting at <paramref name="startPage"/> and wrapping around, optionally without working out where each hit lies on the page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="includeBounds">Whether each hit carries the rectangles it covers; a caller that reads only the matches leaves them out.</param>
    /// <returns>A cold signal giving the pages with at least one hit, in search order; unsubscribing cancels the search.</returns>
    public static IObservable<SearchPageResult> Search(IDocument document, string query, SearchOptions options, int startPage, bool includeBounds)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(query);
        return SearchCore(document, query, options, startPage, includeBounds);
    }

    /// <summary>Searches one page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">The page.</param>
    /// <returns>The result, or <see langword="null"/> when nothing matched or the document closed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SearchPageResult? SearchPage(IDocument document, string query, SearchOptions options, int page) => SearchPage(document, query, options, page, true);

    /// <summary>Searches one page, optionally without working out where each hit lies on the page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">The page.</param>
    /// <param name="includeBounds">Whether each hit carries the rectangles it covers.</param>
    /// <returns>The result, or <see langword="null"/> when nothing matched or the document closed.</returns>
    public static SearchPageResult? SearchPage(IDocument document, string query, SearchOptions options, int page, bool includeBounds)
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
            if (includeBounds)
            {
                document.GetTextBounds(page, match.Start, match.Length, bounds);
            }

            hits[i] = new(match, [.. bounds]);
        }

        return new(page, hits);
    }

    /// <summary>Starts a search of the pages from the starting page, wrapping around, with some pages searched side by side.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="includeBounds">Whether each hit carries the rectangles it covers.</param>
    /// <returns>A cold signal giving each page with a hit in search order, even when a later page finishes first; unsubscribing stops every page still running.</returns>
    private static IObservable<SearchPageResult> SearchCore(IDocument document, string query, SearchOptions options, int startPage, bool includeBounds)
    {
        if (query.Length == 0 || document.PageCount == 0)
        {
            return Signal.Empty<SearchPageResult>();
        }

        var first = Math.Clamp(startPage, 0, document.PageCount - 1);
        var window = Math.Clamp(Environment.ProcessorCount, 1, MaxPagesInFlight);
        return Signal.Defer(() =>
        {
            var order = new SearchOrder();
            return PageSearches(document, query, options, first, includeBounds)
                .Merge(window)
                .SelectMany(order.Release);
        });
    }

    /// <summary>Makes the search of each page from the starting page, wrapping around.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="first">The page to start from.</param>
    /// <param name="includeBounds">Whether each hit carries the rectangles it covers.</param>
    /// <returns>A cold signal for each page, made as the merge asks for it.</returns>
    private static IEnumerable<IObservable<SearchedPage>> PageSearches(IDocument document, string query, SearchOptions options, int first, bool includeBounds)
    {
        var pageCount = document.PageCount;
        for (var position = 0; position < pageCount; position++)
        {
            yield return SearchOne(document, query, options, (first + position) % pageCount, position, includeBounds);
        }
    }

    /// <summary>Prepares and searches one page on the pool.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">How many pages into the search the page is.</param>
    /// <param name="includeBounds">Whether each hit carries the rectangles it covers.</param>
    /// <returns>A cold signal giving the page's result once.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<SearchedPage> SearchOne(IDocument document, string query, SearchOptions options, int page, int position, bool includeBounds) =>
        // Preparing a page is the PDF library's async API, so its call is the one task here.
        Signal.FromAsync(
                async token =>
                {
                    await document.PreparePageAsync(page, token).ConfigureAwait(false);
                    return RxVoid.Default;
                })
            .SwitchMap(_ => Signal.Start(() => SearchPage(document, query, options, page, includeBounds), TaskPoolSequencer.Default))
            .Select(result => new SearchedPage(position, result))
            .SubscribeOn(TaskPoolSequencer.Default);
}
