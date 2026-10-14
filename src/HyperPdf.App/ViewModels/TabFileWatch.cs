// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Settings;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// Follows changes other programs make to a tab's open file: the watch, the file as last seen, and when the tab last
/// saved, so its own save is not taken for an outside edit.
/// </summary>
[DebuggerDisplay("TabFileWatch: {_stamp}")]
internal sealed partial class TabFileWatch : ReactiveObject
{
    /// <summary>The file's last write time and length when it was loaded or last seen to change.</summary>
    private DocumentFileStamp _stamp;

    /// <summary>When the file was last saved from this tab.</summary>
    private long _savedAt;

    /// <summary>Initializes a new instance of the <see cref="TabFileWatch"/> class.</summary>
    internal TabFileWatch() =>
        Changes = this.WhenChanged(static watch => watch.WatchedPath)
            .KeepNotNull()
            .DistinctUntilChanged()
            .SwitchMap(FileChanges.Watch);

    /// <summary>Gets the moments the watched file may have changed, on the watcher's thread; nothing is watched until <see cref="Start"/>.</summary>
    internal IObservable<RxVoid> Changes { get; }

    /// <summary>Gets the file being watched, or <see langword="null"/> before the watch starts.</summary>
    [Reactive]
    internal partial string? WatchedPath { get; private set; }

    /// <summary>Starts watching the file for changes, from the file as it is now.</summary>
    /// <param name="path">The file.</param>
    internal void Start(string path)
    {
        _stamp = DocumentFileStamp.Read(path);
        WatchedPath ??= path;
    }

    /// <summary>Records that the tab saved the file just now.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MarkSaved() => Interlocked.Exchange(ref _savedAt, Environment.TickCount64);

    /// <summary>Decides how to respond to a change notice, and remembers the file as it is now when it changed.</summary>
    /// <param name="path">The file.</param>
    /// <param name="action">What the reader asked to happen when an open file changes.</param>
    /// <param name="hasUnsavedChanges">Whether the tab has edits that a reload would lose.</param>
    /// <returns>The response.</returns>
    internal FileChangeResponse Check(string path, FileChangeAction action, bool hasUnsavedChanges)
    {
        var stamp = DocumentFileStamp.Read(path);
        var response = FileChangeResponses.Decide(new(_stamp, stamp, Environment.TickCount64 - Interlocked.Read(ref _savedAt), action, hasUnsavedChanges));
        if (response != FileChangeResponse.Unchanged)
        {
            _stamp = stamp;
        }

        return response;
    }
}
