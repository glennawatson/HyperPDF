// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>Publishes the accessible tree of a Win32 window through UI Automation, which is what JAWS, NVDA, Narrator and ZoomText read.</summary>
[SupportedOSPlatform("windows")]
[DebuggerDisplay("UiaAccessibilityBridge: {Tree.Count} nodes")]
public sealed class UiaAccessibilityBridge : IWin32AccessibilityBridge
{
    /// <summary>The IID of IRawElementProviderSimple, which UI Automation expects for the root.</summary>
    private static readonly Guid SimpleIid = new("d6dd68d1-86fd-4332-8666-9abedea2d24c");

    /// <summary>Makes the COM callable wrappers for providers.</summary>
    private readonly StrategyBasedComWrappers _wrappers = new();

    /// <summary>The providers made so far, so each node keeps one identity.</summary>
    private readonly Dictionary<int, UiaProvider> _providers = [];

    /// <summary>Guards the tree and the provider table, because UI Automation calls from its own threads.</summary>
    private readonly Lock _gate = new();

    /// <summary>The tree last published.</summary>
    private AccessibleTree _tree = AccessibleTree.Empty;

    /// <summary>The window handle, set by <see cref="Attach"/>.</summary>
    private long _window;

    /// <summary>Gets a value indicating whether an assistive technology is using UI Automation.</summary>
    public bool IsAssistiveTechnologyActive => NativeMethods.ClientsAreListening() != 0;

    /// <summary>Gets the tree last published.</summary>
    internal AccessibleTree Tree
    {
        get
        {
            lock (_gate)
            {
                return _tree;
            }
        }
    }

    /// <summary>Gets the window handle.</summary>
    internal long Window => Volatile.Read(ref _window);

    /// <summary>Gets the provider of the window root, or <see langword="null"/> before anything was published.</summary>
    internal UiaProvider? RootProvider
    {
        get
        {
            var tree = Tree;
            return tree.Count == 0 ? null : ProviderFor(tree.Node(0).Id);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Attach(long window) => Volatile.Write(ref _window, window);

    /// <inheritdoc/>
    public void Publish(ReadOnlySpan<AccessibleNode> nodes)
    {
        var tree = new AccessibleTree(nodes);
        lock (_gate)
        {
            _tree = tree;
            DropMissingProviders(tree);
        }

        RaiseStructureChanged();
    }

    /// <inheritdoc/>
    public void Notify(int id, AccessibleChange change)
    {
        if (change != AccessibleChange.Focus || !IsAssistiveTechnologyActive)
        {
            return;
        }

        RaiseEvent(id, UiaIds.FocusChangedEvent);
    }

    /// <inheritdoc/>
    public void CaretMoved(in AccessibleBounds bounds)
    {
        // Magnifiers such as ZoomText follow the focused element's bounds, which the viewer publishes with the focus change.
    }

    /// <inheritdoc/>
    public bool TryAnswerGetObject(long window, nuint primary, nint secondary, out nint result)
    {
        result = 0;
        if ((int)secondary != UiaIds.RootObjectId || RootProvider is not { } root)
        {
            return false;
        }

        var simple = SimplePointer(root);
        try
        {
            result = NativeMethods.ReturnRawElementProvider((nint)window, primary, secondary, simple);
        }
        finally
        {
            _ = Marshal.Release(simple);
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate)
        {
            _tree = AccessibleTree.Empty;
            _providers.Clear();
        }
    }

    /// <summary>Gets the provider for a node, making it on first use.</summary>
    /// <param name="id">The node id.</param>
    /// <returns>The provider.</returns>
    internal UiaProvider ProviderFor(int id)
    {
        lock (_gate)
        {
            ref var provider = ref CollectionsMarshal.GetValueRefOrAddDefault(_providers, id, out _);
            return provider ??= new(this, id);
        }
    }

    /// <summary>Converts window pixel bounds to screen coordinates.</summary>
    /// <param name="bounds">The bounds in window pixels.</param>
    /// <returns>The bounds on screen.</returns>
    internal unsafe UiaRect ToScreen(in AccessibleBounds bounds)
    {
        int* origin = stackalloc int[2];
        origin[0] = 0;
        origin[1] = 0;
        _ = NativeMethods.ClientToScreen((nint)Window, origin);
        return new(origin[0] + bounds.X, origin[1] + bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>Finds the node at a screen point.</summary>
    /// <param name="x">The screen x.</param>
    /// <param name="y">The screen y.</param>
    /// <returns>The node index, or <see cref="AccessibleTree.None"/>.</returns>
    internal unsafe int HitTestScreen(double x, double y)
    {
        int* origin = stackalloc int[2];
        origin[0] = 0;
        origin[1] = 0;
        _ = NativeMethods.ClientToScreen((nint)Window, origin);
        return Tree.HitTest(x - origin[0], y - origin[1]);
    }

    /// <summary>Forgets providers of nodes the new tree no longer has. The caller holds the gate.</summary>
    /// <param name="tree">The new tree.</param>
    private void DropMissingProviders(AccessibleTree tree)
    {
        List<int> missing = [];
        foreach (var id in _providers.Keys)
        {
            if (tree.IndexOf(id) == AccessibleTree.None)
            {
                missing.Add(id);
            }
        }

        foreach (var id in missing)
        {
            _ = _providers.Remove(id);
        }
    }

    /// <summary>Gets a COM pointer to a provider's IRawElementProviderSimple. The caller releases it.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The pointer.</returns>
    private nint SimplePointer(UiaProvider provider)
    {
        var unknown = _wrappers.GetOrCreateComInterfaceForObject(provider, CreateComInterfaceFlags.None);
        try
        {
            var iid = SimpleIid;
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out var simple));
            return simple;
        }
        finally
        {
            _ = Marshal.Release(unknown);
        }
    }

    /// <summary>Raises a UI Automation event for a node, when a client is listening.</summary>
    /// <param name="id">The node id.</param>
    /// <param name="eventId">The event id.</param>
    private void RaiseEvent(int id, int eventId)
    {
        if (Tree.IndexOf(id) == AccessibleTree.None)
        {
            return;
        }

        var simple = SimplePointer(ProviderFor(id));
        try
        {
            _ = NativeMethods.RaiseAutomationEvent(simple, eventId);
        }
        finally
        {
            _ = Marshal.Release(simple);
        }
    }

    /// <summary>Tells clients the children of the root were replaced, so they read the tree again.</summary>
    private void RaiseStructureChanged()
    {
        if (RootProvider is not { } root || !IsAssistiveTechnologyActive)
        {
            return;
        }

        var simple = SimplePointer(root);
        try
        {
            _ = NativeMethods.RaiseStructureChangedEvent(simple, UiaIds.ChildrenInvalidated, 0, 0);
        }
        finally
        {
            _ = Marshal.Release(simple);
        }
    }
}
