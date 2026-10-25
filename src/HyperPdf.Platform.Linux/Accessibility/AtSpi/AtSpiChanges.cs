// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Turns a change to the tree into the AT-SPI events a screen reader listens for.</summary>
internal static class AtSpiChanges
{
    /// <summary>The app states that are announced as AT-SPI state changes, with their AT-SPI names.</summary>
    private static readonly StateName[] AnnouncedStates =
    [
        new(AccessibleStates.Checked, "checked"),
        new(AccessibleStates.Indeterminate, "indeterminate"),
        new(AccessibleStates.Expanded, "expanded"),
        new(AccessibleStates.Selected, "selected"),
        new(AccessibleStates.Pressed, "pressed"),
        new(AccessibleStates.Busy, "busy"),
        new(AccessibleStates.Enabled, "enabled"),
        new(AccessibleStates.Showing, "showing"),
        new(AccessibleStates.Invalid, "invalid-entry"),
    ];

    /// <summary>Sends the events for one change.</summary>
    /// <param name="change">The change.</param>
    /// <param name="tree">The tree, to find parents.</param>
    /// <param name="emitter">Where the events are sent.</param>
    internal static void Apply(AccessibleChange change, IAccessibleTree tree, AtSpiEmitter emitter)
    {
        switch (change.Kind)
        {
            case AccessibleChangeKind.Added or AccessibleChangeKind.Removed:
            {
                AnnounceChild(change, tree, emitter);
                break;
            }

            case AccessibleChangeKind.Name:
            {
                emitter.PropertyChanged(change.NodeId, "accessible-name", change.Current?.Name ?? string.Empty);
                break;
            }

            case AccessibleChangeKind.Description:
            {
                emitter.PropertyChanged(change.NodeId, "accessible-description", change.Current?.Description ?? string.Empty);
                break;
            }

            case AccessibleChangeKind.States:
            {
                AnnounceStates(change, emitter);
                break;
            }

            case AccessibleChangeKind.Text:
            {
                AnnounceText(change, emitter);
                break;
            }

            case AccessibleChangeKind.Caret:
            {
                emitter.CaretMoved(change.NodeId, change.Current?.Text?.CaretOffset ?? 0);
                break;
            }

            case AccessibleChangeKind.Focus:
            {
                emitter.FocusMoved(change.NodeId);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Announces a child that was added to or removed from its parent.</summary>
    /// <param name="change">The change.</param>
    /// <param name="tree">The tree, to find the parent.</param>
    /// <param name="emitter">Where the event is sent.</param>
    private static void AnnounceChild(AccessibleChange change, IAccessibleTree tree, AtSpiEmitter emitter)
    {
        var added = change.Kind == AccessibleChangeKind.Added;
        var child = added ? change.Current : change.Previous;
        if (child is null || child.ParentId < 0)
        {
            return;
        }

        var siblings = tree.Get(child.ParentId)?.Children;
        emitter.ChildrenChanged(child.ParentId, added, siblings is null ? 0 : Math.Max(IndexOf(siblings, child.Id), 0), child.Id);
    }

    /// <summary>Finds an element among a list of numbers.</summary>
    /// <param name="ids">The numbers.</param>
    /// <param name="id">The number to find.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOf(IReadOnlyList<int> ids, int id)
    {
        for (var index = 0; index < ids.Count; index++)
        {
            if (ids[index] == id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Announces each state that differs between the old and new element.</summary>
    /// <param name="change">The change.</param>
    /// <param name="emitter">Where the events are sent.</param>
    private static void AnnounceStates(AccessibleChange change, AtSpiEmitter emitter)
    {
        if (change.Previous is not { } before || change.Current is not { } after)
        {
            return;
        }

        foreach (var (flag, name) in AnnouncedStates)
        {
            var was = (before.States & flag) != 0;
            var now = (after.States & flag) != 0;
            if (was != now)
            {
                emitter.StateChanged(after.Id, name, now);
            }
        }

        if (((before.States ^ after.States) & AccessibleStates.Active) != 0)
        {
            emitter.WindowActivity(after.Id, (after.States & AccessibleStates.Active) != 0);
        }
    }

    /// <summary>Announces that all of an element's text was replaced.</summary>
    /// <param name="change">The change.</param>
    /// <param name="emitter">Where the events are sent.</param>
    private static void AnnounceText(AccessibleChange change, AtSpiEmitter emitter)
    {
        var old = change.Previous?.Text?.Value ?? string.Empty;
        var current = change.Current?.Text?.Value ?? string.Empty;
        if (old.Length > 0)
        {
            emitter.TextChanged(change.NodeId, false, 0, old);
        }

        if (current.Length > 0)
        {
            emitter.TextChanged(change.NodeId, true, 0, current);
        }
    }

    /// <summary>An app state and the name AT-SPI gives it.</summary>
    /// <param name="Flag">The app state.</param>
    /// <param name="Name">The AT-SPI name.</param>
    private readonly record struct StateName(AccessibleStates Flag, string Name);
}
