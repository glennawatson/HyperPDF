// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.VisualTree;
using HyperPdf.App.Accessibility;
using HyperPdf.App.Views;
using HyperPdf.Core.Accessibility;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks the elements the app offers to screen readers through its own AT-SPI server.</summary>
public sealed class AccessibleTreeTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Verifies that a second reading of an unchanged window gives the same numbers and no changes.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadingTwice_GivesNoChanges()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tree.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var builder = new PeerTreeBuilder();
            var windows = new List<WindowPeer> { new(ControlAutomationPeer.CreatePeerForElement(window), true, false, 0) };

            var first = builder.Build(windows);
            var second = builder.Build(windows);
            var changes = SnapshotDiff.Compare(first.Snapshot, second.Snapshot);

            await Assert.That(first.Snapshot.Nodes.Count).IsGreaterThan(1);
            await Assert.That(string.Join(", ", changes.Select(static c => $"{c.Kind} {c.NodeId} {c.Current?.Role}{c.Previous?.Role} {c.Current?.Name}"))).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that the window, the page area and some controls are offered with roles and names a screen reader can use.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Window_OffersNamedControlsAndThePageText()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("offers.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            var snapshot = new PeerTreeBuilder().Build([new(ControlAutomationPeer.CreatePeerForElement(window), true, false, 0)]).Snapshot;
            var roles = snapshot.Nodes.Values.Select(static n => n.Role).Distinct().ToList();

            await Assert.That(roles).Contains(AccessibleRole.Window);
            await Assert.That(roles).Contains(AccessibleRole.Button);
            await Assert.That(snapshot.Nodes.Values.Any(static n => n.Role == AccessibleRole.Document && n.Text is not null)).IsTrue();
            var unnamed = snapshot.Nodes.Values
                .Where(static n => n.Role == AccessibleRole.Button && n.Name.Length == 0)
                .Select(n => $"{n.Id} parent {snapshot.Nodes[n.ParentId].Role} '{snapshot.Nodes[n.ParentId].Name}' children {n.Children.Count}")
                .ToList();
            await Assert.That(string.Join("; ", unnamed)).IsEqualTo(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }
}
