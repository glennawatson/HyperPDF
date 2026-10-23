// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>Moves through the tree of elements for UI Automation. A window's element is a root: UI Automation links it to its parent itself.</summary>
internal static class UiaNavigation
{
    /// <summary>Finds the element in a direction from another.</summary>
    /// <param name="tree">The elements.</param>
    /// <param name="node">The element to start from.</param>
    /// <param name="direction">The direction.</param>
    /// <returns>The number of the element there, or -1 when there is none.</returns>
    internal static int Find(IAccessibleTree tree, AccessibleNode node, NavigateDirection direction) =>
        direction switch
        {
            NavigateDirection.Parent => IsRoot(tree, node) ? -1 : node.ParentId,
            NavigateDirection.FirstChild => Child(tree, node, true),
            NavigateDirection.LastChild => Child(tree, node, false),
            NavigateDirection.NextSibling => Sibling(tree, node, 1),
            NavigateDirection.PreviousSibling => Sibling(tree, node, -1),
            _ => -1,
        };

    /// <summary>Checks whether an element is the root of a window.</summary>
    /// <param name="tree">The elements.</param>
    /// <param name="node">The element.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsRoot(IAccessibleTree tree, AccessibleNode node) => node.ParentId < 0 || node.ParentId == tree.RootId;

    /// <summary>Finds the first or last child that still exists.</summary>
    /// <param name="tree">The elements.</param>
    /// <param name="node">The parent.</param>
    /// <param name="first">Whether to find the first child; otherwise the last.</param>
    /// <returns>The child's number, or -1 when there is none.</returns>
    private static int Child(IAccessibleTree tree, AccessibleNode node, bool first)
    {
        var children = node.Children;
        for (var step = 0; step < children.Count; step++)
        {
            var id = children[first ? step : children.Count - 1 - step];
            if (tree.Get(id) is not null)
            {
                return id;
            }
        }

        return -1;
    }

    /// <summary>Finds the sibling before or after an element.</summary>
    /// <param name="tree">The elements.</param>
    /// <param name="node">The element.</param>
    /// <param name="step">1 for the next sibling, -1 for the previous.</param>
    /// <returns>The sibling's number, or -1 when there is none.</returns>
    private static int Sibling(IAccessibleTree tree, AccessibleNode node, int step)
    {
        if (IsRoot(tree, node) || tree.Get(node.ParentId) is not { } parent)
        {
            return -1;
        }

        var siblings = parent.Children;
        var index = IndexOf(siblings, node.Id);
        for (var next = index + step; index >= 0 && next >= 0 && next < siblings.Count; next += step)
        {
            if (tree.Get(siblings[next]) is not null)
            {
                return siblings[next];
            }
        }

        return -1;
    }

    /// <summary>Finds a number in a list.</summary>
    /// <param name="ids">The list.</param>
    /// <param name="id">The number.</param>
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
}
