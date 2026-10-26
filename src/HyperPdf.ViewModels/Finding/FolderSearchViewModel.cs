// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Search;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Finding;

/// <summary>
/// Search every PDF in a folder: results arrive file by file while the search runs off the UI thread, and opening one
/// shows its page with the words found.
/// </summary>
[DebuggerDisplay("FolderSearchViewModel: {Query} in {Folder}: {Results.Count} results")]
public sealed partial class FolderSearchViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The most matches kept per file.</summary>
    private const int MaxMatchesPerFile = 200;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Opens a file at a page with the words to show.</summary>
    private readonly Action<string, int, string> _open;

    /// <summary>Owns what ends with the view model: the running search.</summary>
    private readonly MultipleDisposable _lifetime = [];

    /// <summary>Cancels the running search.</summary>
    private CancellationTokenSource? _running;

    /// <summary>Counts the searches started, so a search that finishes late can tell it was replaced.</summary>
    private int _version;

    /// <summary>The resource identity and counts of the displayed search status.</summary>
    private LocalizedMessage _status = LocalizedMessage.Get(UiText, "Choose a folder and the words to find.");

    /// <summary>Initializes a new instance of the <see cref="FolderSearchViewModel"/> class.</summary>
    /// <param name="services">The services.</param>
    /// <param name="open">Opens a file at a page with the words to show.</param>
    public FolderSearchViewModel(AppServices services, Action<string, int, string> open)
    {
        _services = services;
        _open = open;
        _lifetime.Add(Scope.Create(Stop));
    }

    /// <summary>Gets the interaction that asks for a folder.</summary>
    public Interaction<RxVoid, string?> ChooseFolderInteraction { get; } = new();

    /// <summary>Gets the results, in the order the files were searched.</summary>
    public ObservableCollection<FolderSearchResultViewModel> Results { get; } = [];

    /// <summary>Gets or sets the folder searched.</summary>
    [Reactive]
    public partial string Folder { get; set; } = string.Empty;

    /// <summary>Gets or sets the words to find.</summary>
    [Reactive]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether case must match.</summary>
    [Reactive]
    public partial bool MatchCase { get; set; }

    /// <summary>Gets or sets a value indicating whether only whole words match.</summary>
    [Reactive]
    public partial bool WholeWord { get; set; }

    /// <summary>Gets or sets a value indicating whether accents are ignored, so "resume" finds "résumé".</summary>
    [Reactive]
    public partial bool IgnoreDiacritics { get; set; }

    /// <summary>Gets or sets a value indicating whether look-alike characters match: curly and straight quotes, dashes and full-width letters.</summary>
    [Reactive]
    public partial bool FoldCompatibility { get; set; }

    /// <summary>Gets or sets a value indicating whether subfolders are searched too.</summary>
    [Reactive]
    public partial bool IncludeSubfolders { get; set; } = true;

    /// <summary>Gets a value indicating whether a search is running.</summary>
    [Reactive]
    public partial bool IsSearching { get; private set; }

    /// <summary>Gets what the search is doing or found, such as "12 matches in 3 of 40 files".</summary>
    public string Status => _status.Value ?? string.Empty;

    /// <summary>Runs a search to the end, for tests and callers that wait.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    public Task SearchAsync()
    {
        Stop();

        // Whatever search was running now belongs to the past, even if no new one starts.
        _version++;
        Results.Clear();
        if (string.IsNullOrWhiteSpace(Query) || !Directory.Exists(Folder))
        {
            SetStatus(LocalizedMessage.Get(UiText, "Choose a folder and the words to find."));
            IsSearching = false;
            return Task.CompletedTask;
        }

        var cancellation = new CancellationTokenSource();
        _running = cancellation;
        return RunAsync(Folder, Query.Trim(), Options(), IncludeSubfolders, new(_version, cancellation.Token));
    }

    /// <summary>Stops the running search; the results so far stay.</summary>
    [ReactiveCommand]
    public void Stop()
    {
        _running?.Cancel();
        _running?.Dispose();
        _running = null;
    }

    /// <summary>Stops the running search and asks the window to close.</summary>
    [ReactiveCommand]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Close() => Stop();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _lifetime.Dispose();

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Status));
        LocalisationRefresh.RefreshCore(_services, visited);
    }

    /// <summary>Counts matches in words: "1 match", "3 matches".</summary>
    /// <param name="count">The count.</param>
    /// <returns>The words.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static LocalizedMessage Matches(int count) =>
        LocalizedMessage.Concat(
            LocalizedMessage.Literal($"{count.ToString(CultureInfo.CurrentCulture)} "),
            LocalizedMessage.Get(UiText, count == 1 ? "match" : "matches"));

    /// <summary>Summarises the matches and the files they are in: "12 matches in 3 files".</summary>
    /// <param name="matches">The matches.</param>
    /// <param name="files">The files with matches.</param>
    /// <returns>The words.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static LocalizedMessage Summary(int matches, int files) =>
        LocalizedMessage.Format(UiText, "{0} in {1} {2}", Matches(matches), files, LocalizedMessage.Get(UiText, files == 1 ? "file" : "files"));

    /// <summary>Searches the files one by one off the UI thread, adding each file's results as it finishes.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="query">The words.</param>
    /// <param name="options">The options.</param>
    /// <param name="subfolders">Whether to search subfolders.</param>
    /// <param name="run">Stops the search, and says which search it is.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(string folder, string query, SearchOptions options, bool subfolders, FolderSearchRun run)
    {
        IsSearching = true;
        var token = run.Token;
        var matches = 0;
        var withMatches = 0;
        try
        {
            var files = await Task.Run(() => FolderSearch.FindFiles(folder, subfolders), token).ConfigureAwait(true);
            for (var i = 0; i < files.Count && !token.IsCancellationRequested; i++)
            {
                var path = files[i];
                var found = await FolderSearch.SearchFileAsync(_services.Engine, path, query, options, MaxMatchesPerFile, token).ConfigureAwait(true);

                // A file that finished just as the search was stopped or replaced must not add old matches to the results.
                if (token.IsCancellationRequested)
                {
                    break;
                }

                Add(found);
                matches += found.Matches.Count;
                withMatches += found.Matches.Count > 0 ? 1 : 0;
                SetStatus(LocalizedMessage.Format(UiText, "{0}; searched {1} of {2}", Summary(matches, withMatches), i + 1, files.Count));
            }

            // A newer search owns the status and the busy flag, so an older one that unwinds late leaves them alone.
            if (run.Version == _version)
            {
                var stopped = token.IsCancellationRequested ? LocalizedMessage.Get(UiText, ", stopped") : LocalizedMessage.Literal(string.Empty);
                SetStatus(files.Count == 0
                    ? LocalizedMessage.Get(UiText, "There are no PDF files in this folder.")
                    : LocalizedMessage.Format(UiText, "{0} of {1}{2}", Summary(matches, withMatches), files.Count, stopped));
            }
        }
        catch (OperationCanceledException) when (run.Version == _version)
        {
            SetStatus(LocalizedMessage.Format(UiText, "{0}, stopped", Matches(matches)));
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one.
        }
        finally
        {
            if (run.Version == _version)
            {
                IsSearching = false;
            }
        }
    }

    /// <summary>Retains a status and notifies its displayed text.</summary>
    /// <param name="message">The resource key and current counts.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStatus(in LocalizedMessage message)
    {
        _status = message;
        this.RaisePropertyChanged(nameof(Status));
    }

    /// <summary>Adds a file's results.</summary>
    /// <param name="found">What was found.</param>
    private void Add(FolderSearchFile found)
    {
        if (found.Problem is { } problem)
        {
            Results.Add(new(found.Path, -1, UiText.Format("Not searched: it {0}.", problem)));
            return;
        }

        foreach (var match in found.Matches)
        {
            Results.Add(new(found.Path, match.PageIndex, match.Snippet));
        }

        if (found.IsTruncated)
        {
            Results.Add(new(found.Path, -1, UiText.Format(UiText.Get("Only the first {0} matches are shown."), MaxMatchesPerFile)));
        }
    }

    /// <summary>Gets the search options.</summary>
    /// <returns>The options.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SearchOptions Options() => SearchOptionChoices.Of(MatchCase, WholeWord, IgnoreDiacritics, FoldCompatibility);

    /// <summary>Asks for a folder.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task ChooseFolderAsync()
    {
        var folder = await ChooseFolderInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(folder))
        {
            Folder = folder;
        }
    }

    /// <summary>Opens a result at its page.</summary>
    /// <param name="result">The result.</param>
    [ReactiveCommand]
    private void Open(FolderSearchResultViewModel? result)
    {
        if (result is { IsMatch: true })
        {
            _open(result.Path, result.PageIndex, Query.Trim());
        }
    }
}
