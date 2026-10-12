// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Documents;

/// <summary>Observes changes to a file.</summary>
public static class FileChanges
{
    /// <summary>How long the file must be quiet before a change is reported.</summary>
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Creates a cold observable that watches a file while subscribed and emits once a burst of writes, or an atomic
    /// replace, has settled and the file exists again. Notifications arrive on a thread pool thread.
    /// </summary>
    /// <param name="filePath">The file to watch.</param>
    /// <returns>The change notifications.</returns>
    public static AsObservableSignal<RxVoid> Watch(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        return new(Signal.Defer(() => WatchDirectory(directory, fullPath))
            .Throttle(SettleTime)
            .Where(_ => File.Exists(fullPath)));
    }

    /// <summary>Watches the file's directory, or never emits when it does not exist.</summary>
    /// <param name="directory">The directory containing the file.</param>
    /// <param name="fullPath">The file.</param>
    /// <returns>The raw notifications.</returns>
    private static AsObservableSignal<RxVoid> WatchDirectory(string? directory, string fullPath) =>
        new(directory is not null && Directory.Exists(directory)
            ? Signal.Using(() => CreateWatcher(directory, fullPath), Observe)
            : Signal.Never<RxVoid>());

    /// <summary>Creates the watcher.</summary>
    /// <param name="directory">The directory containing the file.</param>
    /// <param name="fullPath">The file.</param>
    /// <returns>The watcher.</returns>
    private static FileSystemWatcher CreateWatcher(string directory, string fullPath) =>
        new(directory, Path.GetFileName(fullPath)) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };

    /// <summary>Bridges the watcher's notifications into one observable and starts raising them.</summary>
    /// <param name="watcher">The watcher.</param>
    /// <returns>The notifications.</returns>
    private static AsObservableSignal<RxVoid> Observe(FileSystemWatcher watcher)
    {
        var changed = watcher.Events().Changed;
        var created = watcher.Events().Created;
        var renamed = watcher.Events().Renamed;
        watcher.EnableRaisingEvents = true;
        return new(Signal.Merge(changed.Select(static _ => RxVoid.Default), created.Select(static _ => RxVoid.Default), renamed.Select(static _ => RxVoid.Default)));
    }
}
