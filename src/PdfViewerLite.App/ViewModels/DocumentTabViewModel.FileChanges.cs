// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Settings;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <content>Following changes other programs make to the open file.</content>
public sealed partial class DocumentTabViewModel
{
    /// <summary>The file's last write time and length when it was loaded or last seen to change.</summary>
    private DocumentFileStamp _fileStamp;

    /// <summary>Reloads the document from disk, keeping the current page.</summary>
    /// <returns>A task for the selected tab's reload.</returns>
    [ReactiveCommand]
    public async Task ReloadAsync()
    {
        HasPendingReload = false;
        var page = CurrentPageIndex;
        var token = SelectedWorkToken;
        RenderHub.Scheduler.Invalidate(Source.Id);
        RenderHub.RemoveDocumentTiles(Source.Id);
        Source.Reload();
        IsLoaded = false;
        _documentChanges.OnNext(RxVoid.Default);
        if (_selectedWork is null)
        {
            return;
        }

        try
        {
            await EnsureLoadedAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }

        if (!IsLoaded)
        {
            return;
        }

        GoToPage(page);
        Search.Refresh();
    }

    /// <summary>Reloads a changed file, or offers to, depending on the user's choice. Unsaved edits are never discarded silently.</summary>
    internal void OnFileChanged()
    {
        var stamp = DocumentFileStamp.Read(FilePath);
        if (Environment.TickCount64 - Interlocked.Read(ref _savedAt) < SelfSaveWindowMilliseconds)
        {
            _fileStamp = stamp;
            return;
        }

        // macOS can report a write made just before watching began; a file whose time and length are unchanged is the one loaded.
        if (stamp == _fileStamp)
        {
            return;
        }

        _fileStamp = stamp;
        if (_services.Settings.FileChangeAction == FileChangeAction.AskToReload || Source.HasUnsavedChanges)
        {
            HasPendingReload = true;
            return;
        }

        _ = ReloadChangedFileAsync();
    }

    /// <summary>Reloads on the UI thread while observing errors from the file watcher callback.</summary>
    /// <returns>A task.</returns>
    private async Task ReloadChangedFileAsync()
    {
        try
        {
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            Trace.TraceError(error.ToString());
        }
    }

    /// <summary>Starts watching the file for changes, from the file as it is now.</summary>
    private void WatchFile()
    {
        _fileStamp = DocumentFileStamp.Read(FilePath);
        _fileWatch ??= FileChanges.Watch(FilePath)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => OnFileChanged(), static ex => Trace.TraceError(ex.ToString()));
    }
}
