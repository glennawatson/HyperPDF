// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Automation.Peers;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>
/// Walks the Avalonia automation peers of every window and builds a snapshot of the elements a screen reader sees. It keeps one number
/// for each peer for as long as the peer lives, so an element keeps its number from one snapshot to the next.
/// </summary>
[DebuggerDisplay("PeerTreeBuilder: next {_nextId}")]
internal sealed class PeerTreeBuilder
{
    /// <summary>The deepest level walked, which keeps a cycle in the peers from running away.</summary>
    private const int MaxDepth = 96;

    /// <summary>The most elements kept in one snapshot.</summary>
    private const int MaxNodes = 40_000;

    /// <summary>The number each peer was given.</summary>
    private readonly ConditionalWeakTable<AutomationPeer, StrongBox<int>> _ids = new();

    /// <summary>The next number to give out; the application has number 0.</summary>
    private int _nextId = AvaloniaNodeFactory.ApplicationId + 1;

    /// <summary>Builds a snapshot of the windows.</summary>
    /// <param name="windows">The windows, in the order they are shown.</param>
    /// <returns>The snapshot and the peers it came from.</returns>
    internal PeerTree Build(IReadOnlyList<WindowPeer> windows)
    {
        var scratch = new BuildState();
        var windowIds = new List<int>(windows.Count);
        foreach (var window in windows)
        {
            AddPeer(window.Peer, AvaloniaNodeFactory.ApplicationId, 0, scratch, windowIds);
            if (windowIds.Count > 0 && scratch.Nodes.TryGetValue(windowIds[^1], out var node))
            {
                scratch.Nodes[node.Id] = node with { States = node.States | WindowStates(window), WindowNumber = window.Number };
            }
        }

        scratch.Nodes[AvaloniaNodeFactory.ApplicationId] = AvaloniaNodeFactory.CreateApplication(windowIds);
        return new(new(scratch.Nodes, AvaloniaNodeFactory.ApplicationId, scratch.FocusedId), scratch.Peers);
    }

    /// <summary>Gets the states that depend on the window rather than its peer.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The states.</returns>
    private static AccessibleStates WindowStates(WindowPeer window) =>
        (window.IsActive ? AccessibleStates.Active : AccessibleStates.None) | (window.IsModal ? AccessibleStates.Modal : AccessibleStates.None);

    /// <summary>Adds a peer and its children, or only its children when the peer is not something a screen reader sees.</summary>
    /// <param name="peer">The peer.</param>
    /// <param name="parentId">The number of the parent element.</param>
    /// <param name="depth">How deep the peer is.</param>
    /// <param name="state">The snapshot being built.</param>
    /// <param name="siblings">The numbers of the parent's children, which this call adds to.</param>
    private void AddPeer(AutomationPeer peer, int parentId, int depth, BuildState state, List<int> siblings)
    {
        if (depth > MaxDepth || state.Nodes.Count > MaxNodes)
        {
            return;
        }

        if (!peer.IsControlElement() && !peer.IsContentElement())
        {
            AddChildren(peer, parentId, depth, state, siblings);
            return;
        }

        if (peer.IsOffscreen() && depth > 0)
        {
            return;
        }

        var id = IdOf(peer);
        var children = new List<int>();
        AddChildren(peer, id, depth + 1, state, children);
        PeerSets.Assign(children, state.Nodes);
        state.Nodes[id] = AvaloniaNodeFactory.Create(id, parentId, peer, children);
        state.Peers[id] = peer;
        siblings.Add(id);
        if (peer.HasKeyboardFocus())
        {
            state.FocusedId = id;
        }
    }

    /// <summary>Adds the children of a peer.</summary>
    /// <param name="peer">The peer.</param>
    /// <param name="parentId">The number the children belong to.</param>
    /// <param name="depth">How deep the children are.</param>
    /// <param name="state">The snapshot being built.</param>
    /// <param name="siblings">The numbers of the children, which this call adds to.</param>
    private void AddChildren(AutomationPeer peer, int parentId, int depth, BuildState state, List<int> siblings)
    {
        foreach (var child in peer.GetChildren())
        {
            AddPeer(child, parentId, depth, state, siblings);
        }
    }

    /// <summary>Gets the number of a peer, giving it one the first time.</summary>
    /// <param name="peer">The peer.</param>
    /// <returns>The number.</returns>
    private int IdOf(AutomationPeer peer)
    {
        if (_ids.TryGetValue(peer, out var existing))
        {
            return existing.Value;
        }

        var id = _nextId++;
        _ids.Add(peer, new(id));
        return id;
    }

    /// <summary>The snapshot as it is built.</summary>
    private sealed class BuildState
    {
        /// <summary>Gets the elements by number.</summary>
        internal Dictionary<int, AccessibleNode> Nodes { get; } = [];

        /// <summary>Gets the peers by element number.</summary>
        internal Dictionary<int, AutomationPeer> Peers { get; } = [];

        /// <summary>Gets or sets the number of the focused element, or -1.</summary>
        internal int FocusedId { get; set; } = -1;
    }
}
