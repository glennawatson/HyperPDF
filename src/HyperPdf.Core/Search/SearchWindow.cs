// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Search;

/// <summary>
/// Searches pages a few at a time: a page starts only once fewer than the window of pages ahead of the one being read have started, so
/// a slow page never lets the pages behind it pile up. Dispose stops every page still running.
/// </summary>
internal sealed class SearchWindow : IDisposable
{
    /// <summary>The document.</summary>
    private readonly IDocument _document;

    /// <summary>The text to find.</summary>
    private readonly string _query;

    /// <summary>The search options.</summary>
    private readonly SearchOptions _options;

    /// <summary>The page to start from.</summary>
    private readonly int _first;

    /// <summary>Whether each hit carries the rectangles it covers.</summary>
    private readonly bool _includeBounds;

    /// <summary>How many pages may be started ahead of the one being read.</summary>
    private readonly int _window;

    /// <summary>The pages started and not yet read, by their position in the search.</summary>
    private readonly Dictionary<int, ConnectableSignal<SearchPageResult?>> _started = [];

    /// <summary>Stops the pages when disposed.</summary>
    private readonly MultipleDisposable _running = new();

    /// <summary>The position of the next page to start.</summary>
    private int _next;

    /// <summary>Initializes a new instance of the <see cref="SearchWindow"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="first">The page to start from.</param>
    /// <param name="includeBounds">Whether each hit carries the rectangles it covers.</param>
    /// <param name="window">How many pages may be started ahead of the one being read.</param>
    internal SearchWindow(IDocument document, string query, SearchOptions options, int first, bool includeBounds, int window)
    {
        _document = document;
        _query = query;
        _options = options;
        _first = first;
        _includeBounds = includeBounds;
        _window = window;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _running.Dispose();

    /// <summary>Makes the signal of each page in search order, starting the pages ahead of it as it is asked for.</summary>
    /// <returns>A cold signal for each page, giving its hits when it has any.</returns>
    internal IEnumerable<IObservable<SearchPageResult>> Pages()
    {
        var count = _document.PageCount;
        for (var position = 0; position < count; position++)
        {
            Start(Math.Min(count, position + _window));
            _ = _started.Remove(position, out var page);
            yield return page!.Where(static result => result is not null).Select(static result => result!);
        }
    }

    /// <summary>Starts the pages up to a position, when they have not started.</summary>
    /// <param name="upTo">The position the pages start before.</param>
    private void Start(int upTo)
    {
        for (; _next < upTo; _next++)
        {
            var page = SearchOne((_first + _next) % _document.PageCount).Replay();
            _started[_next] = page;
            _running.Add(page.Connect());
        }
    }

    /// <summary>Prepares and searches one page on the pool.</summary>
    /// <param name="page">The page.</param>
    /// <returns>A cold signal giving the page's result once, or <see langword="null"/> when nothing matched.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<SearchPageResult?> SearchOne(int page) =>
        _document.PreparePage(page)
            .SwitchMap(_ => Signal.Start(() => DocumentSearch.SearchPage(_document, _query, _options, page, _includeBounds), TaskPoolSequencer.Default))
            .SubscribeOn(TaskPoolSequencer.Default);
}
