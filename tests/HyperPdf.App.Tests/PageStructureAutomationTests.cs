// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks the main page view tells assistive technology about headings, lists, tables and figures, not only Focus Mode.</summary>
public sealed class PageStructureAutomationTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The blocks of the table and list document: a heading, four cells and two list items.</summary>
    private const int StructuredBlocks = 7;

    /// <summary>The fewest blocks of the tagged test document: a heading, paragraphs and a figure.</summary>
    private const int TaggedBlocks = 4;

    /// <summary>The place of the first data cell among the blocks.</summary>
    private const int CellIndex = 3;

    /// <summary>The place of the last list item among the blocks.</summary>
    private const int LastItem = 6;

    /// <summary>The row and the position of the second item.</summary>
    private const int Second = 2;

    /// <summary>A tagged document's heading and figure reach the screen reader from the main view with their roles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadingAndFigureAreListedInReadingOrder()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "tagged.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTagged());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var peer = await FindPeerAsync(window);
            _ = await UiWait.UntilAsync(() => peer.GetChildren().Count >= TaggedBlocks);
            var blocks = peer.GetChildren();

            await Assert.That(blocks[0].GetName()).IsEqualTo(TestPdf.TaggedHeading);
            await Assert.That(blocks[0].GetAutomationControlType()).IsEqualTo(AutomationControlType.Text);
            await Assert.That(blocks[0].GetHeadingLevel()).IsEqualTo(1);
            await Assert.That(blocks[1].GetHeadingLevel()).IsEqualTo(0);
            await Assert.That(blocks[^1].GetName()).IsEqualTo(TestPdf.TaggedFigure);
            await Assert.That(blocks[^1].GetAutomationControlType()).IsEqualTo(AutomationControlType.Image);
            await Assert.That(blocks[^1].GetHelpText()).IsEqualTo("Figure");
            await Assert.That(blocks[0].GetParent()).IsNotNull();
            await Assert.That(blocks[0].GetParent()!.GetAutomationControlType()).IsEqualTo(AutomationControlType.Document);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A tagged table is read cell by cell with its row and column, and a list item by item with its place in the list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TablesAndListsAreListedWithTheirPlace()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "structured.pdf");
        await File.WriteAllBytesAsync(path, StructuredTestPdf.CreateTagged());
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var peer = await FindPeerAsync(window);
            _ = await UiWait.UntilAsync(() => peer.GetChildren().Count >= StructuredBlocks);
            var blocks = peer.GetChildren();

            await Assert.That(blocks[0].GetHeadingLevel()).IsGreaterThan(0);
            await Assert.That(blocks[CellIndex].GetName()).IsEqualTo(StructuredTestPdf.DataCell);
            await Assert.That(blocks[CellIndex].GetAutomationControlType()).IsEqualTo(AutomationControlType.DataItem);
            await Assert.That(blocks[CellIndex].GetHelpText()).IsEqualTo($"Table cell, row {Second}, column 1");
            await Assert.That(blocks[LastItem].GetAutomationControlType()).IsEqualTo(AutomationControlType.ListItem);
            await Assert.That(blocks[LastItem].GetHelpText()).IsEqualTo($"List item {Second} of {Second}");
            await Assert.That(blocks[LastItem - 1].GetHelpText()).IsEqualTo($"List item 1 of {Second}");
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Finds the page canvas' automation peer once the document is shown.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The peer.</returns>
    private static async Task<AutomationPeer> FindPeerAsync(MainWindow window)
    {
        PageCanvas? canvas = null;
        _ = await UiWait.UntilAsync(() => (canvas = window.GetVisualDescendants().OfType<PageCanvas>().FirstOrDefault()) is not null);
        return ControlAutomationPeer.CreatePeerForElement(canvas!);
    }
}
