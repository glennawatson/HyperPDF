// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Documents;

/// <summary>
/// Watches files for changes with one operating system watcher for each folder, however many of its files are watched. A
/// watcher for every open file runs out on Linux, where a user may hold only a few hundred at once across every program, so
/// with hundreds of documents open the later ones would never be told their file changed.
/// </summary>
[DebuggerDisplay("FolderWatches: {_folders.Count} folders")]
public sealed class FolderWatches
{
    /// <summary>How long the file must be quiet before a change is reported.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(400);

    /// <summary>How file names are compared: as the file system of this operating system does.</summary>
    private static readonly StringComparison NameComparison = OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>Guards <see cref="_folders"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>The names of the entries that change in each folder watched so far, shared by everything watching a file in it.</summary>
    private readonly Dictionary<string, IObservable<string?>> _folders = [with(StringComparer.Ordinal)];

    /// <summary>
    /// Creates a cold observable that watches a file while subscribed and emits once a burst of writes, or an atomic
    /// replace, has settled and the file exists again. Notifications arrive on a thread pool thread.
    /// </summary>
    /// <param name="filePath">The file to watch.</param>
    /// <returns>The change notifications.</returns>
    public AsObservableSignal<RxVoid> Watch(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var name = Path.GetFileName(fullPath);
        return new(Signal.Defer(() => Changes(Path.GetDirectoryName(fullPath)))
            .Where(changed => string.Equals(changed, name, NameComparison))
            .AsSignal()
            .Throttle(SettleTime)
            .Where(_ => File.Exists(fullPath)));
    }

    /// <summary>Bridges a watcher's notifications into the names of the entries that changed, and starts raising them.</summary>
    /// <param name="watcher">The watcher.</param>
    /// <returns>The names.</returns>
    private static IObservable<string?> Observe(FileSystemWatcher watcher)
    {
        var changed = watcher.Events().Changed.Select(static change => change.Name);
        var created = watcher.Events().Created.Select(static change => change.Name);
        var renamed = watcher.Events().Renamed.Select(static change => change.Name);
        watcher.EnableRaisingEvents = true;
        return Signal.Merge(changed, created, renamed);
    }

    /// <summary>Creates the watcher of a folder.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The watcher.</returns>
    private static FileSystemWatcher CreateWatcher(string folder) =>
        new(folder) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };

    /// <summary>Gets the names of the entries that change in a folder, or nothing when the folder does not exist.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The names, from one watcher that runs while anything follows them.</returns>
    private IObservable<string?> Changes(string? folder)
    {
        if (folder is null || !Directory.Exists(folder))
        {
            return Signal.Never<string?>();
        }

        lock (_gate)
        {
            // The watcher starts with the first file watched in the folder and stops with the last.
            ref var shared = ref CollectionsMarshal.GetValueRefOrAddDefault(_folders, folder, out var known);
            if (!known)
            {
                shared = Signal.Using(() => CreateWatcher(folder), Observe).Publish().RefCount();
            }

            return shared!;
        }
    }
}
