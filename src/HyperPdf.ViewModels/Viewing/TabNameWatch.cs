// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics;
using HyperPdf.ViewModels.Services;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// Follows the name and the unsaved mark of every open tab for one listener. Each tab is followed once, from when it opens until it
/// closes, so opening or closing a tab among hundreds starts or stops following that tab alone.
/// </summary>
[DebuggerDisplay("TabNameWatch: {_followed.Count} tabs")]
internal sealed class TabNameWatch : IDisposable
{
    /// <summary>What follows each open tab.</summary>
    private readonly Dictionary<DocumentTabViewModel, IDisposable> _followed = [];

    /// <summary>The tabs open now, filled again on each change to tell which tabs closed.</summary>
    private readonly HashSet<DocumentTabViewModel> _open = [];

    /// <summary>Tells the listener that a name, an unsaved mark or the list of tabs changed.</summary>
    private readonly Action<RxVoid> _changed;

    /// <summary>Follows the list of tabs, once started.</summary>
    private IDisposable? _tabs;

    /// <summary>Initializes a new instance of the <see cref="TabNameWatch"/> class.</summary>
    /// <param name="listener">Who is told of each change.</param>
    private TabNameWatch(IObserver<RxVoid> listener) => _changed = listener.OnNext;

    /// <inheritdoc/>
    public void Dispose()
    {
        _tabs?.Dispose();
        _tabs = null;
        foreach (var pair in _followed)
        {
            pair.Value.Dispose();
        }

        _followed.Clear();
        _open.Clear();
    }

    /// <summary>Starts following the open tabs for a listener, which is told now and again on each change.</summary>
    /// <param name="tabs">The open tabs.</param>
    /// <param name="listener">Who is told of each change.</param>
    /// <returns>What stops the following.</returns>
    internal static IDisposable Start(IList tabs, IObserver<RxVoid> listener)
    {
        var watch = new TabNameWatch(listener);
        watch.Follow(tabs);
        watch._tabs = tabs.CollectionChanges.SubscribeSafe(watch.Follow, ErrorTrace.Write);
        return watch;
    }

    /// <summary>Brings the tabs followed in step with the tabs open, and tells the listener the list changed.</summary>
    /// <param name="tabs">The open tabs.</param>
    private void Follow(IList tabs)
    {
        _open.Clear();

        // The list is read by index: walking it through the untyped interface would box its enumerator on every change.
        for (var i = 0; i < tabs.Count; i++)
        {
            if (tabs[i] is not DocumentTabViewModel tab || !_open.Add(tab) || _followed.ContainsKey(tab))
            {
                continue;
            }

            _followed.Add(
                tab,
                Signal.Merge(tab.WhenChanged(static t => t.Title).AsSignal(), tab.WhenChanged(static t => t.HasUnsavedChanges).AsSignal()).SubscribeSafe(_changed, ErrorTrace.Write));
        }

        if (_followed.Count != _open.Count)
        {
            StopClosed();
        }

        _changed(RxVoid.Default);
    }

    /// <summary>Stops following the tabs that closed.</summary>
    private void StopClosed()
    {
        foreach (var pair in _followed)
        {
            if (_open.Contains(pair.Key))
            {
                continue;
            }

            pair.Value.Dispose();
            _ = _followed.Remove(pair.Key);
        }
    }
}
