// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Search;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>Find-in-document state: the query, incremental results and the current hit.</summary>
[DebuggerDisplay("SearchViewModel: {Query}: {Results.Count}")]
public sealed partial class SearchViewModel : ReactiveObject, IDisposable
{
    /// <summary>The number of characters of context shown on each side of a hit.</summary>
    private const int ContextLength = 32;

    /// <summary>How long typing must pause before a search starts.</summary>
    private static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Subscriptions following the query settings.</summary>
    private readonly MultipleDisposable _subscriptions;

    /// <summary>Hits grouped by page, for highlighting.</summary>
    private readonly Dictionary<int, List<SearchHit>> _hitsByPage = [];

    /// <summary>Cancels the running search.</summary>
    private CancellationTokenSource? _search;

    /// <summary>Whether a tab switch stopped a search before it finished.</summary>
    private bool _interrupted;

    /// <summary>Initializes a new instance of the <see cref="SearchViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public SearchViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        _subscriptions =
        [
            this.WhenChanged(static x => x.Query, static x => x.MatchCase, static x => x.WholeWord)
                .Throttle(TypingDelay, RxSchedulers.MainThreadScheduler)
                .SubscribeSafe(_ => StartSearch(), ErrorTrace.Write),

            // A result picked in the sidebar list moves to its hit; selecting the current hit's result moves nothing.
            this.WhenChanged(static x => x.SelectedResult)
                .Skip(1)
                .KeepNotNull()

                // The filter reads the current hit: a constructor cannot hand 'this' to a state-passing operator.
                .Where(result => result.Index != CurrentIndex)
                .SubscribeSafe(result => MoveTo(result.Index), ErrorTrace.Write),

            // The running search stops with the view model, after the subscriptions that could start another.
            Scope.Create(Cancel),
        ];
    }

    /// <summary>Gets a number that changes each time the highlighted hits change, so the pages draw them again.</summary>
    [Reactive]
    public partial int HighlightVersion { get; private set; }

    /// <summary>Gets or sets the query.</summary>
    [Reactive]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether letter case must match.</summary>
    [Reactive]
    public partial bool MatchCase { get; set; }

    /// <summary>Gets or sets a value indicating whether only whole words match.</summary>
    [Reactive]
    public partial bool WholeWord { get; set; }

    /// <summary>Gets or sets a value indicating whether the search bar is open.</summary>
    [Reactive]
    public partial bool IsOpen { get; set; }

    /// <summary>Gets a value indicating whether a search is running.</summary>
    [Reactive]
    public partial bool IsSearching { get; private set; }

    /// <summary>Gets the status text, for example "3 of 12".</summary>
    [Reactive]
    public partial string Status { get; private set; } = string.Empty;

    /// <summary>Gets the index of the current hit, or -1.</summary>
    [Reactive]
    public partial int CurrentIndex { get; private set; } = -1;

    /// <summary>Gets or sets the selected result in the sidebar list.</summary>
    [Reactive]
    public partial SearchResultItemViewModel? SelectedResult { get; set; }

    /// <summary>Gets the results in search order.</summary>
    public ObservableCollection<SearchResultItemViewModel> Results { get; } = [];

    /// <summary>Gets the current hit, if any.</summary>
    public SearchHit? CurrentHit => CurrentIndex >= 0 && CurrentIndex < Results.Count ? Results[CurrentIndex].Hit : null;

    /// <summary>Gets the hits on a page.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The hits, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<SearchHit>? GetHits(int pageIndex) => _hitsByPage.GetValueOrDefault(pageIndex);

    /// <summary>Opens the search bar. The sidebar stays on whichever panel the user chose.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Open() => IsOpen = true;

    /// <summary>Closes the search bar and clears highlights.</summary>
    [ReactiveCommand]
    public void Close()
    {
        IsOpen = false;
        Query = string.Empty;
        _interrupted = false;
        Cancel();
        Clear();
    }

    /// <summary>Moves to the next hit.</summary>
    [ReactiveCommand]
    public void Next()
    {
        if (Results.Count > 0)
        {
            MoveTo((CurrentIndex + 1) % Results.Count);
        }
    }

    /// <summary>Moves to the previous hit.</summary>
    [ReactiveCommand]
    public void Previous()
    {
        if (Results.Count > 0)
        {
            MoveTo(CurrentIndex <= 0 ? Results.Count - 1 : CurrentIndex - 1);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Restarts the search after the document reloads.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Refresh() => StartSearch();

    /// <summary>Stops page-search I/O and CPU work when its tab loses selection; a search that was not finished starts again when the tab is selected.</summary>
    internal void CancelForTabSwitch()
    {
        _interrupted = _search is not null && IsSearching;
        Cancel();
    }

    /// <summary>Starts again a search the tab switch interrupted, once the document is open.</summary>
    internal void ResumeAfterTabSwitch()
    {
        if (!_interrupted || _owner.TryGetDocument() is null)
        {
            return;
        }

        _interrupted = false;
        StartSearch();
    }

    /// <summary>Gets the text around a hit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="match">The match.</param>
    /// <returns>The context.</returns>
    private static string GetContext(IDocument document, TextMatch match)
    {
        var start = Math.Max(0, match.Start - ContextLength);
        var end = Math.Min(document.GetCharacterCount(match.PageIndex), match.Start + match.Length + ContextLength);
        var text = document.GetText(match.PageIndex, start, end - start);
        return text.ReplaceLineEndings(" ").Trim();
    }

    /// <summary>Selects a hit and scrolls to it.</summary>
    /// <param name="index">The hit index.</param>
    private void MoveTo(int index)
    {
        CurrentIndex = index;
        var item = Results[index];
        SelectedResult = item;
        UpdateStatus();
        var bounds = item.Hit.Bounds;
        _owner.NavigateTo(item.PageIndex, bounds.Length > 0 ? bounds[0] : null, 0);
        HighlightVersion++;
    }

    /// <summary>Cancels the running search.</summary>
    private void Cancel()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
        IsSearching = false;
    }

    /// <summary>Clears results.</summary>
    private void Clear()
    {
        Results.Clear();
        _hitsByPage.Clear();
        CurrentIndex = -1;
        SelectedResult = null;
        Status = string.Empty;
        HighlightVersion++;
    }

    /// <summary>Starts a new search for the current query.</summary>
    private void StartSearch()
    {
        _interrupted = false;
        Cancel();
        Clear();
        var document = _owner.TryGetDocument();
        if (string.IsNullOrWhiteSpace(Query) || document is null)
        {
            return;
        }

        var options = (MatchCase ? SearchOptions.MatchCase : SearchOptions.None) | (WholeWord ? SearchOptions.WholeWord : SearchOptions.None);
        _search = new();
        _ = RunSearchAsync(document, Query, options, _owner.CurrentPageIndex, _search.Token);
    }

    /// <summary>Runs a search, adding results on the UI thread as pages complete.</summary>
    /// <param name="document">The document.</param>
    /// <param name="query">The query.</param>
    /// <param name="options">The options.</param>
    /// <param name="startPage">The page to start from.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>A task.</returns>
    private async Task RunSearchAsync(IDocument document, string query, SearchOptions options, int startPage, CancellationToken cancellationToken)
    {
        IsSearching = true;
        Status = "Searching…";
        try
        {
            await foreach (var page in DocumentSearch.SearchAsync(document, query, options, Math.Max(0, startPage), cancellationToken))
            {
                // A page read before a newer search cancelled this one still arrives; the results now belong to the newer search.
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var list = new List<SearchHit>(page.Hits.Count);
                foreach (var hit in page.Hits)
                {
                    list.Add(hit);
                    Results.Add(new(Results.Count, page.PageIndex, hit, GetContext(document, hit.Match)));
                }

                _hitsByPage[page.PageIndex] = list;
                if (CurrentIndex < 0)
                {
                    MoveTo(0);
                }

                UpdateStatus();
                HighlightVersion++;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        // The last page can finish just as a newer search starts; the busy flag and status now belong to the newer one.
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IsSearching = false;
        UpdateStatus();
    }

    /// <summary>Updates the status text.</summary>
    private void UpdateStatus() => Status = Results.Count switch
    {
        0 when IsSearching => "Searching…",
        0 => "No results",
        _ => $"{CurrentIndex + 1} of {Results.Count}{(IsSearching ? "+" : string.Empty)}",
    };
}
