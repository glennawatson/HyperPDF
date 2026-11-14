// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Windowing.Accessibility;

/// <summary>Walks an <see cref="IAccessibleTree"/> by element number, which every platform's accessibility API needs for parent, sibling and child queries.</summary>
public static class AccessibleNavigation
{
    /// <summary>The number that means there is no element.</summary>
    private const int NoElement = -1;

    /// <summary>Gets the number that means there is no element in a direction.</summary>
    public static int None => NoElement;

    /// <summary>Gets the parent of an element.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="id">The element number.</param>
    /// <returns>The parent's number, or <see cref="None"/>.</returns>
    public static int Parent(IAccessibleTree tree, int id)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var node = tree.Get(id);
        return node is null || tree.Get(node.ParentId) is null ? NoElement : node.ParentId;
    }

    /// <summary>Gets the first child of an element.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="id">The element number.</param>
    /// <returns>The child's number, or <see cref="None"/>.</returns>
    public static int FirstChild(IAccessibleTree tree, int id)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Get(id) is { Children: { Count: > 0 } children } ? children[0] : NoElement;
    }

    /// <summary>Gets the last child of an element.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="id">The element number.</param>
    /// <returns>The child's number, or <see cref="None"/>.</returns>
    public static int LastChild(IAccessibleTree tree, int id)
    {
        ArgumentNullException.ThrowIfNull(tree);
        return tree.Get(id) is { Children: { Count: > 0 } children } ? children[^1] : NoElement;
    }

    /// <summary>Gets the sibling after an element.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="id">The element number.</param>
    /// <returns>The sibling's number, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int NextSibling(IAccessibleTree tree, int id) => Sibling(tree, id, 1);

    /// <summary>Gets the sibling before an element.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="id">The element number.</param>
    /// <returns>The sibling's number, or <see cref="None"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PreviousSibling(IAccessibleTree tree, int id) => Sibling(tree, id, -1);

    /// <summary>Gets the sibling a step away from an element, in the order the parent lists its children.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="id">The element number.</param>
    /// <param name="step">1 for the next sibling, -1 for the previous one.</param>
    /// <returns>The sibling's number, or <see cref="None"/>.</returns>
    private static int Sibling(IAccessibleTree tree, int id, int step)
    {
        ArgumentNullException.ThrowIfNull(tree);
        var parent = tree.Get(id) is { } node ? tree.Get(node.ParentId) : null;
        if (parent is null)
        {
            return NoElement;
        }

        var position = IndexOfChild(parent.Children, id);
        if (position < 0)
        {
            return NoElement;
        }

        var at = position + step;
        return at >= 0 && at < parent.Children.Count ? parent.Children[at] : NoElement;
    }

    /// <summary>Finds a child's position in its parent's list.</summary>
    /// <param name="children">The parent's children.</param>
    /// <param name="id">The child number.</param>
    /// <returns>The position, or -1 when the child is not listed.</returns>
    private static int IndexOfChild(IReadOnlyList<int> children, int id)
    {
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] == id)
            {
                return i;
            }
        }

        return NoElement;
    }
}
