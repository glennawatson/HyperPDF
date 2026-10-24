// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.App.Accessibility;

/// <summary>
/// All the elements of the app at one moment. A snapshot never changes after it is built, so a platform bridge on another thread
/// can read it while the UI thread builds the next one.
/// </summary>
/// <param name="Nodes">The elements by number.</param>
/// <param name="RootId">The number of the application element.</param>
/// <param name="FocusedId">The number of the element with the keyboard focus, or -1.</param>
[DebuggerDisplay("AccessibleSnapshot: {Nodes.Count} elements")]
internal sealed record AccessibleSnapshot(IReadOnlyDictionary<int, AccessibleNode> Nodes, int RootId, int FocusedId)
{
    /// <summary>Gets a snapshot that holds only an empty application element.</summary>
    internal static AccessibleSnapshot Empty { get; } = new(
        new Dictionary<int, AccessibleNode> { [0] = AvaloniaNodeFactory.CreateApplication([]) },
        0,
        -1);

    /// <summary>Finds the deepest element that holds a point, preferring later siblings because they are drawn on top.</summary>
    /// <param name="x">Pixels from the left of the screen.</param>
    /// <param name="y">Pixels from the top of the screen.</param>
    /// <returns>The element's number, or -1 when no window holds the point.</returns>
    internal int HitTest(int x, int y)
    {
        var current = Nodes.TryGetValue(RootId, out var root) ? root : null;
        var found = -1;
        while (current is not null)
        {
            var next = ChildAt(current, x, y);
            if (next is null)
            {
                return found;
            }

            found = next.Id;
            current = next;
        }

        return found;
    }

    /// <summary>Checks whether a point is inside bounds.</summary>
    /// <param name="bounds">The bounds.</param>
    /// <param name="x">Pixels from the left of the screen.</param>
    /// <param name="y">Pixels from the top of the screen.</param>
    /// <returns>Whether the point is inside.</returns>
    private static bool Contains(AccessibleBounds bounds, int x, int y) =>
        x >= bounds.X && y >= bounds.Y && x < bounds.X + bounds.Width && y < bounds.Y + bounds.Height;

    /// <summary>Finds the last child of an element that holds a point.</summary>
    /// <param name="parent">The element.</param>
    /// <param name="x">Pixels from the left of the screen.</param>
    /// <param name="y">Pixels from the top of the screen.</param>
    /// <returns>The child, or <see langword="null"/> when none holds the point.</returns>
    private AccessibleNode? ChildAt(AccessibleNode parent, int x, int y)
    {
        for (var index = parent.Children.Count - 1; index >= 0; index--)
        {
            if (Nodes.TryGetValue(parent.Children[index], out var child) && Contains(child.Bounds, x, y))
            {
                return child;
            }
        }

        return null;
    }
}
