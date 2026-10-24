// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>Finds what changed between two snapshots, so a screen reader can be told only about that.</summary>
internal static class SnapshotDiff
{
    /// <summary>Lists the changes that turn one snapshot into the next, removals first and the focus last.</summary>
    /// <param name="before">The earlier snapshot.</param>
    /// <param name="after">The later snapshot.</param>
    /// <returns>The changes.</returns>
    internal static List<AccessibleChange> Compare(AccessibleSnapshot before, AccessibleSnapshot after)
    {
        var changes = new List<AccessibleChange>();
        foreach (var (id, old) in before.Nodes)
        {
            if (!after.Nodes.ContainsKey(id))
            {
                changes.Add(new(AccessibleChangeKind.Removed, id, old, null));
            }
        }

        foreach (var (id, current) in after.Nodes)
        {
            if (before.Nodes.TryGetValue(id, out var old))
            {
                AddPropertyChanges(old, current, changes);
            }
            else
            {
                changes.Add(new(AccessibleChangeKind.Added, id, null, current));
            }
        }

        if (after.FocusedId >= 0 && after.FocusedId != before.FocusedId && after.Nodes.TryGetValue(after.FocusedId, out var focused))
        {
            changes.Add(new(AccessibleChangeKind.Focus, focused.Id, null, focused));
        }

        return changes;
    }

    /// <summary>Adds a change for each property that differs between an element's two versions.</summary>
    /// <param name="old">The element before.</param>
    /// <param name="current">The element after.</param>
    /// <param name="changes">The list the changes are added to.</param>
    private static void AddPropertyChanges(AccessibleNode old, AccessibleNode current, List<AccessibleChange> changes)
    {
        if (!string.Equals(old.Name, current.Name, StringComparison.Ordinal))
        {
            changes.Add(new(AccessibleChangeKind.Name, current.Id, old, current));
        }

        if (!string.Equals(old.Description, current.Description, StringComparison.Ordinal))
        {
            changes.Add(new(AccessibleChangeKind.Description, current.Id, old, current));
        }

        // The focus is announced on its own, so a focus flag that moved is not announced twice.
        if ((old.States & ~AccessibleStates.Focused) != (current.States & ~AccessibleStates.Focused))
        {
            changes.Add(new(AccessibleChangeKind.States, current.Id, old, current));
        }

        AddTextChanges(old, current, changes);
    }

    /// <summary>Adds a change when an element's text or caret differs.</summary>
    /// <param name="old">The element before.</param>
    /// <param name="current">The element after.</param>
    /// <param name="changes">The list the changes are added to.</param>
    private static void AddTextChanges(AccessibleNode old, AccessibleNode current, List<AccessibleChange> changes)
    {
        if (!string.Equals(old.Text?.Value, current.Text?.Value, StringComparison.Ordinal))
        {
            changes.Add(new(AccessibleChangeKind.Text, current.Id, old, current));
        }
        else if (old.Text?.CaretOffset != current.Text?.CaretOffset)
        {
            changes.Add(new(AccessibleChangeKind.Caret, current.Id, old, current));
        }
    }
}
