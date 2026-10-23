// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Automation.Peers;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Accessibility;

/// <summary>
/// The app's elements as a platform bridge reads them. The UI thread builds a snapshot from the Avalonia automation peers and
/// publishes it; reads from any thread use the latest snapshot, and actions are carried out back on the UI thread.
/// </summary>
[DebuggerDisplay("AvaloniaAccessibleTree: {_current.Snapshot}")]
internal sealed class AvaloniaAccessibleTree : IAccessibleTree, IDisposable
{
    /// <summary>The name of a request to move the focus.</summary>
    private const string FocusAction = "focus";

    /// <summary>The builder, which keeps the numbers of the peers.</summary>
    private readonly PeerTreeBuilder _builder = new();

    /// <summary>The changes between snapshots.</summary>
    private readonly Signal<AccessibleChange> _changes = new();

    /// <summary>The latest snapshot with its peers; replaced as a whole.</summary>
    private PeerTree _current = new(AccessibleSnapshot.Empty, new Dictionary<int, AutomationPeer>());

    /// <inheritdoc/>
    public int RootId => AvaloniaNodeFactory.ApplicationId;

    /// <inheritdoc/>
    public int FocusedId => Volatile.Read(ref _current).Snapshot.FocusedId;

    /// <inheritdoc/>
    public IObservable<AccessibleChange> Changes => _changes;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AccessibleNode? Get(int id) => Volatile.Read(ref _current).Snapshot.Nodes.GetValueOrDefault(id);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int HitTest(int x, int y) => Volatile.Read(ref _current).Snapshot.HitTest(x, y);

    /// <inheritdoc/>
    public bool DoAction(int id, int action)
    {
        var node = Get(id);
        if (node is null || action < 0 || action >= node.Actions.Count)
        {
            return false;
        }

        RunOnUiThread(new(id, node.Actions[action]));
        return true;
    }

    /// <inheritdoc/>
    public bool Focus(int id)
    {
        if (Get(id) is null)
        {
            return false;
        }

        RunOnUiThread(new(id, FocusAction));
        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool SetCaret(int id, int offset) => false;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _changes.Dispose();

    /// <summary>Builds a new snapshot from the windows and tells subscribers what changed. Call it on the UI thread.</summary>
    /// <param name="windows">The windows.</param>
    internal void Refresh(IReadOnlyList<WindowPeer> windows)
    {
        var previous = Volatile.Read(ref _current);
        var next = _builder.Build(windows);
        Volatile.Write(ref _current, next);
        foreach (var change in SnapshotDiff.Compare(previous.Snapshot, next.Snapshot))
        {
            _changes.OnNext(change);
        }
    }

    /// <summary>Carries out a request on the UI thread, where the peers may be used.</summary>
    /// <param name="request">The request.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RunOnUiThread(PendingAction request) =>
        Signal.Return(request, AvaloniaScheduler.Instance).SubscribeSafe(Perform, AppLog.Error);

    /// <summary>Carries out a request. Call it on the UI thread.</summary>
    /// <param name="request">The request.</param>
    private void Perform(PendingAction request)
    {
        if (!Volatile.Read(ref _current).Peers.TryGetValue(request.Id, out var peer))
        {
            return;
        }

        if (request.Action == FocusAction)
        {
            peer.SetFocus();
            return;
        }

        _ = PeerFacts.Invoke(peer, request.Action);
    }

    /// <summary>A request from a screen reader that must be carried out on the UI thread.</summary>
    /// <param name="Id">The element's number.</param>
    /// <param name="Action">The action's name, or the focus action's name when the request moves the focus.</param>
    [DebuggerDisplay("PendingAction: {Id} {Action}")]
    private readonly record struct PendingAction(int Id, string Action);
}
