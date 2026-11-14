// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the tree UI Automation walks: sibling links, hit testing and focus.</summary>
public sealed class AccessibleTreeTests
{
    /// <summary>The window node id.</summary>
    private const int WindowId = 1;

    /// <summary>The page node id.</summary>
    private const int PageId = 2;

    /// <summary>The first text node id.</summary>
    private const int FirstTextId = 3;

    /// <summary>The second text node id.</summary>
    private const int SecondTextId = 4;

    /// <summary>The root has no parent.</summary>
    private const int NoParent = -1;

    /// <summary>The width of the test window.</summary>
    private const double WindowWidth = 400;

    /// <summary>The height of the test window.</summary>
    private const double WindowHeight = 300;

    /// <summary>The height of one text line.</summary>
    private const double LineHeight = 20;

    /// <summary>A point inside the second text line.</summary>
    private const double InsideSecondLine = 30;

    /// <summary>A point outside the window.</summary>
    private const double Outside = 1000;

    /// <summary>Children link to their parent and to each other in published order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChildrenAreLinkedInOrder()
    {
        var tree = new AccessibleTree(Nodes());
        var page = tree.IndexOf(PageId);
        var first = tree.IndexOf(FirstTextId);
        var second = tree.IndexOf(SecondTextId);

        await Assert.That(tree.Parent(tree.IndexOf(WindowId))).IsEqualTo(AccessibleTree.None);
        await Assert.That(tree.FirstChild(page)).IsEqualTo(first);
        await Assert.That(tree.LastChild(page)).IsEqualTo(second);
        await Assert.That(tree.Next(first)).IsEqualTo(second);
        await Assert.That(tree.Previous(second)).IsEqualTo(first);
        await Assert.That(tree.Next(second)).IsEqualTo(AccessibleTree.None);
    }

    /// <summary>Hit testing returns the deepest node under the point.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HitTestFindsTheDeepestNode()
    {
        var tree = new AccessibleTree(Nodes());

        await Assert.That(tree.Node(tree.HitTest(1, InsideSecondLine)).Id).IsEqualTo(SecondTextId);
        await Assert.That(tree.Node(tree.HitTest(1, WindowHeight - 1)).Id).IsEqualTo(PageId);
        await Assert.That(tree.HitTest(Outside, Outside)).IsEqualTo(AccessibleTree.None);
    }

    /// <summary>The focused node is found, and an empty tree has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FocusedNodeIsFound()
    {
        var tree = new AccessibleTree(Nodes());

        await Assert.That(tree.Node(tree.FocusedIndex()).Id).IsEqualTo(FirstTextId);
        await Assert.That(AccessibleTree.Empty.FocusedIndex()).IsEqualTo(AccessibleTree.None);
    }

    /// <summary>Builds a window holding a page that holds two text lines, with focus on the first line.</summary>
    /// <returns>The nodes.</returns>
    private static AccessibleNode[] Nodes() =>
    [
        new(WindowId, NoParent, AccessibleRole.Window, "HyperPDF", null, new(0, 0, WindowWidth, WindowHeight), AccessibleStates.None),
        new(PageId, WindowId, AccessibleRole.Page, "Page 1", null, new(0, 0, WindowWidth, WindowHeight), AccessibleStates.None),
        new(FirstTextId, PageId, AccessibleRole.Text, "First line", null, new(0, 0, WindowWidth, LineHeight), AccessibleStates.Focused),
        new(SecondTextId, PageId, AccessibleRole.Text, "Second line", null, new(0, LineHeight, WindowWidth, LineHeight), AccessibleStates.None),
    ];
}
