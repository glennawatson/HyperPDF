// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Converts between the screen, window and parent coordinates a screen reader may ask for.</summary>
internal static class AtSpiGeometry
{
    /// <summary>The coordinate type for the whole screen.</summary>
    internal const uint ScreenCoordinates = 0;

    /// <summary>The coordinate type for the element's top level window.</summary>
    internal const uint WindowCoordinates = 1;

    /// <summary>The coordinate type for the element's parent.</summary>
    internal const uint ParentCoordinates = 2;

    /// <summary>The most parents followed when looking for a window, which keeps a cycle from looping.</summary>
    private const int MaxDepth = 256;

    /// <summary>Gets an element's bounds in the coordinates asked for.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="node">The element.</param>
    /// <param name="coordinateType">The coordinate type.</param>
    /// <returns>The bounds.</returns>
    internal static AccessibleBounds Extents(IAccessibleTree tree, AccessibleNode node, uint coordinateType)
    {
        var origin = OriginOf(tree, node, coordinateType);
        return new(node.Bounds.X - origin.X, node.Bounds.Y - origin.Y, node.Bounds.Width, node.Bounds.Height);
    }

    /// <summary>Gets the top left corner of the space the coordinates are measured from.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="node">The element.</param>
    /// <param name="coordinateType">The coordinate type.</param>
    /// <returns>The corner in screen coordinates.</returns>
    internal static Origin OriginOf(IAccessibleTree tree, AccessibleNode node, uint coordinateType)
    {
        var anchor = coordinateType switch
        {
            WindowCoordinates => WindowOf(tree, node),
            ParentCoordinates => tree.Get(node.ParentId),
            _ => null,
        };

        return anchor is null ? new(0, 0) : new(anchor.Bounds.X, anchor.Bounds.Y);
    }

    /// <summary>Finds the top level window an element is in.</summary>
    /// <param name="tree">The tree.</param>
    /// <param name="node">The element.</param>
    /// <returns>The window, or <see langword="null"/> when the element is not inside one.</returns>
    internal static AccessibleNode? WindowOf(IAccessibleTree tree, AccessibleNode node)
    {
        var current = node;
        for (var depth = 0; depth < MaxDepth && current is not null; depth++)
        {
            if (current.Role is AccessibleRole.Window or AccessibleRole.Dialog)
            {
                return current;
            }

            current = tree.Get(current.ParentId);
        }

        return null;
    }
}
