// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using HyperPdf.Core.Accessibility;
using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>Serves the accessible tree of a Win32 window through UI Automation, which is what JAWS, NVDA, Narrator and ZoomText read.</summary>
[SupportedOSPlatform("windows")]
[DebuggerDisplay("UiaAccessibilityBridge: window {_window}")]
public sealed class UiaAccessibilityBridge : IWin32AccessibilityBridge
{
    /// <summary>The IID of IRawElementProviderSimple, which UI Automation expects for the root.</summary>
    private static readonly Guid SimpleIid = new("d6dd68d1-86fd-4332-8666-9abedea2d24c");

    /// <summary>Makes the COM callable wrappers for providers.</summary>
    private readonly StrategyBasedComWrappers _wrappers = new();

    /// <summary>The providers made so far, so each node keeps one identity.</summary>
    private readonly Dictionary<int, UiaProvider> _providers = [];

    /// <summary>Guards the provider table, because UI Automation calls from its own threads.</summary>
    private readonly Lock _gate = new();

    /// <summary>The tree being served, or <see langword="null"/> before <see cref="Attach(IAccessibleTree)"/>.</summary>
    private IAccessibleTree? _tree;

    /// <summary>The subscription to the tree's changes.</summary>
    private IDisposable? _changes;

    /// <summary>The window handle, set by <see cref="AttachWindow"/>.</summary>
    private long _window;

    /// <inheritdoc/>
    public bool IsAssistiveTechnologyActive => NativeMethods.ClientsAreListening() != 0;

    /// <summary>Gets the tree being served.</summary>
    internal IAccessibleTree? Tree => Volatile.Read(ref _tree);

    /// <summary>Gets the window handle.</summary>
    internal long Window => Volatile.Read(ref _window);

    /// <summary>Gets the provider of the window root, or <see langword="null"/> before a tree is attached.</summary>
    internal UiaProvider? RootProvider => WindowRootId() is var id && id != AccessibleNavigation.None ? ProviderFor(id) : null;

    /// <inheritdoc/>
    public void Attach(IAccessibleTree tree)
    {
        ArgumentNullException.ThrowIfNull(tree);
        _changes?.Dispose();
        Volatile.Write(ref _tree, tree);
        _changes = tree.Changes.Subscribe(new ChangeObserver(this));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AttachWindow(long window) => Volatile.Write(ref _window, window);

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
        _changes?.Dispose();
        _changes = null;
        Volatile.Write(ref _tree, null);
        lock (_gate)
        {
            _providers.Clear();
        }
    }

    /// <summary>Tells whether a node is the root UI Automation reaches through the window.</summary>
    /// <param name="id">The node id.</param>
    /// <returns><see langword="true"/> when the node is the window's root.</returns>
    internal bool IsWindowRoot(int id) => id == WindowRootId();

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

    /// <summary>Raises the UI Automation event that matches a change in the tree.</summary>
    /// <param name="change">The change.</param>
    private void OnChange(AccessibleChange change)
    {
        if (!IsAssistiveTechnologyActive)
        {
            return;
        }

        switch (change.Kind)
        {
            case AccessibleChangeKind.Focus:
                {
                    RaiseEvent(change.NodeId, UiaIds.FocusChangedEvent);
                    break;
                }

            case AccessibleChangeKind.Added or AccessibleChangeKind.Removed or AccessibleChangeKind.Children:
                {
                    RaiseStructureChanged(change.NodeId);
                    break;
                }

            default:
                {
                    // Name, state and bounds changes are read on demand the next time a client asks for the property.
                    break;
                }
        }
    }

    /// <summary>Finds the node that stands for this window: the window child of the application whose number is the handle, or else the first window.</summary>
    /// <returns>The node id, or <see cref="AccessibleNavigation.None"/> before a tree is attached.</returns>
    private int WindowRootId()
    {
        if (Tree is not { } tree || tree.Get(tree.RootId) is not { } application)
        {
            return AccessibleNavigation.None;
        }

        var first = AccessibleNavigation.None;
        foreach (var childId in application.Children)
        {
            if (tree.Get(childId) is not { Role: AccessibleRole.Window } child)
            {
                continue;
            }

            if (child.WindowNumber == Window)
            {
                return childId;
            }

            first = first == AccessibleNavigation.None ? childId : first;
        }

        return first == AccessibleNavigation.None ? tree.RootId : first;
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

    /// <summary>Raises a UI Automation event for a node.</summary>
    /// <param name="id">The node id.</param>
    /// <param name="eventId">The event id.</param>
    private void RaiseEvent(int id, int eventId)
    {
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

    /// <summary>Tells clients the children of a node changed, so they read them again.</summary>
    /// <param name="id">The node id.</param>
    private void RaiseStructureChanged(int id)
    {
        var simple = SimplePointer(ProviderFor(id));
        try
        {
            _ = NativeMethods.RaiseStructureChangedEvent(simple, UiaIds.ChildrenInvalidated, 0, 0);
        }
        finally
        {
            _ = Marshal.Release(simple);
        }
    }

    /// <summary>Passes the tree's changes to the bridge.</summary>
    /// <param name="bridge">The bridge.</param>
    private sealed class ChangeObserver(UiaAccessibilityBridge bridge) : IObserver<AccessibleChange>
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnNext(AccessibleChange value) => bridge.OnChange(value);

        /// <inheritdoc/>
        public void OnError(Exception error)
        {
            // A failed change stream leaves UI Automation reading the tree on demand, which still works.
        }

        /// <inheritdoc/>
        public void OnCompleted()
        {
            // A finished change stream leaves UI Automation reading the tree on demand, which still works.
        }
    }
}
