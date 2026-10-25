// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Diagnostics;
using ReactiveUI.Primitives;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>
/// Tells UI Automation what changed, so a screen reader speaks a new focus, a changed name or state, and moved text. Changes to the
/// children of an element come in bursts when a page is rebuilt, so they are gathered and sent once for each parent.
/// </summary>
[DebuggerDisplay("UiaEvents")]
internal sealed class UiaEvents : IDisposable
{
    /// <summary>How long structure changes are gathered before they are sent.</summary>
    private static readonly TimeSpan GatherPeriod = TimeSpan.FromMilliseconds(100);

    /// <summary>The states that change a property, with the property they change.</summary>
    private static readonly StateProperty[] StateProperties =
    [
        new(AccessibleStates.Checked | AccessibleStates.Indeterminate, UiaPropertyId.ToggleState),
        new(AccessibleStates.Expanded | AccessibleStates.Expandable, UiaPropertyId.ExpandCollapseState),
        new(AccessibleStates.Selected, UiaPropertyId.SelectionItemIsSelected),
        new(AccessibleStates.Enabled, UiaPropertyId.IsEnabled),
        new(AccessibleStates.Showing, UiaPropertyId.IsOffscreen),
        new(AccessibleStates.Editable, UiaPropertyId.ValueIsReadOnly),
    ];

    /// <summary>The elements events are raised for.</summary>
    private readonly UiaElements _elements;

    /// <summary>The parents whose children changed since the last send.</summary>
    private readonly HashSet<int> _changedParents = [];

    /// <summary>Guards <see cref="_changedParents"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>Stops listening to the tree; set once the events object is complete, so no change reaches a half-built one.</summary>
    private IDisposable? _subscription;

    /// <summary>Sends the gathered structure changes; set once the events object is complete.</summary>
    private Timer? _flush;

    /// <summary>Initializes a new instance of the <see cref="UiaEvents"/> class.</summary>
    /// <param name="elements">The elements events are raised for.</param>
    private UiaEvents(UiaElements elements) => _elements = elements;

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscription?.Dispose();
        _flush?.Dispose();
    }

    /// <summary>Starts raising events for the changes of a tree.</summary>
    /// <param name="elements">The elements events are raised for.</param>
    /// <returns>The events, to dispose when the elements are no longer served.</returns>
    internal static UiaEvents Start(UiaElements elements)
    {
        var events = new UiaEvents(elements);
        events._flush = new(static state => ((UiaEvents)state!).SendStructureChanges(), events, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        // The step reads the elements held here; Primitives has no state-passing SubscribeSafe yet.
        events._subscription = elements.Tree.Changes.SubscribeSafe(events.Apply, AppLog.Error);
        return events;
    }

    /// <summary>Sends the events for one change, when a client is listening.</summary>
    /// <param name="change">The change.</param>
    private void Apply(AccessibleChange change)
    {
        if (change.Kind == AccessibleChangeKind.Removed)
        {
            _elements.Forget(change.NodeId);
        }

        if (NativeMethods.UiaClientsAreListening() == 0)
        {
            return;
        }

        try
        {
            Raise(change);
        }
        catch (COMException ex)
        {
            // The client or the element went away while the event was on its way; there is nobody left to tell.
            AppLog.Warning(ex, $"UI Automation event for {change.Kind} {change.NodeId} was not delivered.");
        }
    }

    /// <summary>Raises the events for one change.</summary>
    /// <param name="change">The change.</param>
    private void Raise(AccessibleChange change)
    {
        switch (change.Kind)
        {
            case AccessibleChangeKind.Added or AccessibleChangeKind.Removed:
            {
                MarkParentChanged((change.Current ?? change.Previous)?.ParentId ?? -1);
                break;
            }

            case AccessibleChangeKind.Focus:
            {
                RaiseEvent(change.NodeId, UiaEventId.AutomationFocusChanged);
                break;
            }

            case AccessibleChangeKind.Name or AccessibleChangeKind.Description:
            {
                RaiseTextProperty(change);
                break;
            }

            case AccessibleChangeKind.States:
            {
                RaiseStateProperties(change);
                break;
            }

            case AccessibleChangeKind.Text:
            {
                RaiseEvent(change.NodeId, UiaEventId.TextTextChanged);
                break;
            }

            case AccessibleChangeKind.Caret:
            {
                RaiseEvent(change.NodeId, UiaEventId.TextTextSelectionChanged);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Raises the property change for a name or a description.</summary>
    /// <param name="change">The change.</param>
    private void RaiseTextProperty(AccessibleChange change)
    {
        var isName = change.Kind == AccessibleChangeKind.Name;
        var property = isName ? UiaPropertyId.Name : UiaPropertyId.HelpText;
        var before = isName ? change.Previous?.Name : change.Previous?.Description;
        var after = isName ? change.Current?.Name : change.Current?.Description;
        var oldValue = ComVariant.FromString(before ?? string.Empty);
        var newValue = ComVariant.FromString(after ?? string.Empty);
        try
        {
            RaisePropertyChanged(change.NodeId, property, oldValue, newValue);
        }
        finally
        {
            oldValue.Free();
            newValue.Free();
        }
    }

    /// <summary>Raises the property changes for the states that differ between an element's two versions.</summary>
    /// <param name="change">The change.</param>
    private void RaiseStateProperties(AccessibleChange change)
    {
        if (change.Previous is not { } previous || change.Current is not { } current)
        {
            return;
        }

        foreach (var (states, property) in StateProperties)
        {
            if ((previous.States & states) != (current.States & states))
            {
                RaisePropertyChanged(change.NodeId, property, UiaProperties.Read((int)property, previous), UiaProperties.Read((int)property, current));
            }
        }

        if ((previous.States & AccessibleStates.Selected) == 0 && (current.States & AccessibleStates.Selected) != 0)
        {
            RaiseEvent(change.NodeId, UiaEventId.SelectionItemElementSelected);
        }
    }

    /// <summary>Notes that an element's children changed, to send once the burst of changes is over.</summary>
    /// <param name="parentId">The parent's number.</param>
    private void MarkParentChanged(int parentId)
    {
        if (parentId < 0 || parentId == _elements.Tree.RootId)
        {
            return;
        }

        lock (_gate)
        {
            if (_changedParents.Add(parentId) && _changedParents.Count == 1)
            {
                _ = _flush?.Change(GatherPeriod, Timeout.InfiniteTimeSpan);
            }
        }
    }

    /// <summary>Sends the gathered structure changes. It runs on a timer thread.</summary>
    private void SendStructureChanges()
    {
        int[] parents;
        lock (_gate)
        {
            parents = [.. _changedParents];
            _changedParents.Clear();
        }

        foreach (var parent in parents)
        {
            try
            {
                RaiseStructureChanged(parent);
            }
            catch (COMException ex)
            {
                AppLog.Warning(ex, $"UI Automation structure change for {parent} was not delivered.");
            }
        }
    }

    /// <summary>Raises a structure change for the children of an element.</summary>
    /// <param name="parentId">The parent's number.</param>
    private void RaiseStructureChanged(int parentId)
    {
        if (_elements.Tree.Get(parentId) is null)
        {
            return;
        }

        var provider = NativeMethods.ComPointerOf<IRawElementProviderSimple>(_elements.Get(parentId));
        try
        {
            _ = NativeMethods.UiaRaiseStructureChangedEvent(provider, (int)StructureChangeType.ChildrenInvalidated, 0, 0);
        }
        finally
        {
            _ = Marshal.Release(provider);
        }
    }

    /// <summary>Raises an event for an element.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="eventId">The event.</param>
    private void RaiseEvent(int id, UiaEventId eventId)
    {
        if (_elements.Tree.Get(id) is null)
        {
            return;
        }

        var provider = NativeMethods.ComPointerOf<IRawElementProviderSimple>(_elements.Get(id));
        try
        {
            _ = NativeMethods.UiaRaiseAutomationEvent(provider, (int)eventId);
        }
        finally
        {
            _ = Marshal.Release(provider);
        }
    }

    /// <summary>Raises a property change for an element.</summary>
    /// <param name="id">The element's number.</param>
    /// <param name="property">The property.</param>
    /// <param name="oldValue">The value before.</param>
    /// <param name="newValue">The value after.</param>
    private void RaisePropertyChanged(int id, UiaPropertyId property, ComVariant oldValue, ComVariant newValue)
    {
        if (_elements.Tree.Get(id) is null)
        {
            return;
        }

        var provider = NativeMethods.ComPointerOf<IRawElementProviderSimple>(_elements.Get(id));
        try
        {
            _ = NativeMethods.UiaRaiseAutomationPropertyChangedEvent(provider, (int)property, oldValue, newValue);
        }
        finally
        {
            _ = Marshal.Release(provider);
        }
    }

    /// <summary>The states that change one property.</summary>
    /// <param name="States">The states.</param>
    /// <param name="Property">The property.</param>
    [DebuggerDisplay("StateProperty: {Property}")]
    private readonly record struct StateProperty(AccessibleStates States, UiaPropertyId Property);
}
