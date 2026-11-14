// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Accessibility;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks the parent, child and sibling walks every platform's accessibility bridge relies on.</summary>
public sealed class AccessibleNavigationTests
{
    /// <summary>A parent is found, and the root has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParentIsFoundAndTheRootHasNone()
    {
        var tree = new FakeAccessibleTree();

        await Assert.That(AccessibleNavigation.Parent(tree, FakeAccessibleTree.FirstLineId)).IsEqualTo(FakeAccessibleTree.PageId);
        await Assert.That(AccessibleNavigation.Parent(tree, FakeAccessibleTree.ApplicationId)).IsEqualTo(AccessibleNavigation.None);
    }

    /// <summary>The first and last child follow the order the parent lists.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FirstAndLastChildFollowTheListedOrder()
    {
        var tree = new FakeAccessibleTree();

        await Assert.That(AccessibleNavigation.FirstChild(tree, FakeAccessibleTree.PageId)).IsEqualTo(FakeAccessibleTree.FirstLineId);
        await Assert.That(AccessibleNavigation.LastChild(tree, FakeAccessibleTree.PageId)).IsEqualTo(FakeAccessibleTree.SecondLineId);
        await Assert.That(AccessibleNavigation.FirstChild(tree, FakeAccessibleTree.FirstLineId)).IsEqualTo(AccessibleNavigation.None);
    }

    /// <summary>Siblings step both ways and stop at the ends.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SiblingsStepBothWaysAndStopAtTheEnds()
    {
        var tree = new FakeAccessibleTree();

        await Assert.That(AccessibleNavigation.NextSibling(tree, FakeAccessibleTree.FirstLineId)).IsEqualTo(FakeAccessibleTree.SecondLineId);
        await Assert.That(AccessibleNavigation.PreviousSibling(tree, FakeAccessibleTree.SecondLineId)).IsEqualTo(FakeAccessibleTree.FirstLineId);
        await Assert.That(AccessibleNavigation.NextSibling(tree, FakeAccessibleTree.SecondLineId)).IsEqualTo(AccessibleNavigation.None);
        await Assert.That(AccessibleNavigation.PreviousSibling(tree, FakeAccessibleTree.FirstLineId)).IsEqualTo(AccessibleNavigation.None);
    }

    /// <summary>An element the tree does not know has no relatives.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownElementHasNoRelatives()
    {
        var tree = new FakeAccessibleTree();
        const int Missing = 99;

        await Assert.That(AccessibleNavigation.Parent(tree, Missing)).IsEqualTo(AccessibleNavigation.None);
        await Assert.That(AccessibleNavigation.FirstChild(tree, Missing)).IsEqualTo(AccessibleNavigation.None);
        await Assert.That(AccessibleNavigation.NextSibling(tree, Missing)).IsEqualTo(AccessibleNavigation.None);
    }
}
