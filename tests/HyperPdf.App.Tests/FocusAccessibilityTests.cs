// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.App.Tests;

/// <summary>Checks Focus Mode tells assistive technology about headings, lists, tables and figures.</summary>
public sealed class FocusAccessibilityTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>A third level heading.</summary>
    private const int ThirdLevel = 3;

    /// <summary>The level of a heading found from the layout.</summary>
    private const int LayoutLevel = 2;

    /// <summary>A list item's position.</summary>
    private const int Position = 2;

    /// <summary>A list's size.</summary>
    private const int ListSize = 5;

    /// <summary>The blocks of the table and list document: a heading, four cells and two list items.</summary>
    private const int ExpectedBlocks = 7;

    /// <summary>The place of the first data cell among the blocks.</summary>
    private const int CellIndex = 3;

    /// <summary>A heading level on a block that is not a heading.</summary>
    private const int StrayLevel = 4;

    /// <summary>A tagged document's heading, paragraphs and figure reach the screen reader with their roles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedStructureReachesTheScreenReader()
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
            main.SelectedTab!.FocusMode.IsOn = true;
            _ = await UiWait.UntilAsync(() => Blocks(window).Count >= 4);
            var blocks = Blocks(window);
            var heading = ControlAutomationPeer.CreatePeerForElement(blocks[0].BodyText);
            var figure = ControlAutomationPeer.CreatePeerForElement(blocks[^1].BodyText);

            await Assert.That(heading.GetName()).IsEqualTo(TestPdf.TaggedHeading);
            await Assert.That(heading.GetAutomationControlType()).IsEqualTo(AutomationControlType.Text);
            await Assert.That(AutomationProperties.GetHeadingLevel(blocks[0].BodyText)).IsEqualTo(1);
            await Assert.That(figure.GetName()).IsEqualTo(TestPdf.TaggedFigure);
            await Assert.That(figure.GetAutomationControlType()).IsEqualTo(AutomationControlType.Image);
            await Assert.That(AutomationProperties.GetHelpText(blocks[^1].BodyText)).IsEqualTo("Figure");
            await Assert.That(AutomationProperties.GetHeadingLevel(blocks[1].BodyText)).IsEqualTo(0);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A tagged table is read cell by cell with its row and column, and a tagged list item by item with its place in the list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedTablesAndListsReachTheScreenReader()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "structured.pdf");
        await File.WriteAllBytesAsync(path, StructuredTestPdf.CreateTagged());

        await AssertTableAndListAsync(test, path);
    }

    /// <summary>
    /// An untagged page that the optimiser tagged from its layout reads the same way: the ruled table is read cell by cell
    /// and the numbered list item by item.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptimisedTablesAndListsReachTheScreenReader()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "optimised.pdf");
        using (var source = PdfDocumentReader.Open(StructuredTestPdf.CreateUntagged(), null))
        await using (var output = File.Create(path))
        {
            _ = PdfOptimizer.Optimize(source, output, PdfOptimizeOptions.KeepQuality with { AddInferredTags = true });
        }

        await AssertTableAndListAsync(test, path);
    }

    /// <summary>Where the platform bridge ignores heading levels and list positions, the description says them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DescribesStructureInWordsWhereNeeded()
    {
        var heading = Block(ReadingBlockKind.Heading, ThirdLevel);
        var listed = new FocusBlockViewModel(0, new(ReadingBlockKind.ListItem, "Second", [0], default(PageRect), 1), 0) { PositionInList = Position, ListSize = ListSize };

        await Assert.That(FocusAccessibility.Description(heading, true)).IsEqualTo("Heading level 3");
        await Assert.That(FocusAccessibility.Description(heading, false)).IsNull();
        await Assert.That(FocusAccessibility.Description(listed, true)).IsEqualTo("List item 2 of 5");
        await Assert.That(FocusAccessibility.Description(listed, false)).IsNull();
        await Assert.That(FocusAccessibility.Description(Block(ReadingBlockKind.Footnote, 0), false)).IsEqualTo("Footnote");
        await Assert.That(FocusAccessibility.Description(Block(ReadingBlockKind.Paragraph, 0), true)).IsNull();
    }

    /// <summary>A heading found from the layout still gets a level, and only headings have one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryHeadingHasALevel()
    {
        await Assert.That(Block(ReadingBlockKind.Heading, 0).Level).IsEqualTo(LayoutLevel);
        await Assert.That(Block(ReadingBlockKind.Heading, 1).Level).IsEqualTo(1);
        await Assert.That(Block(ReadingBlockKind.Paragraph, StrayLevel).Level).IsEqualTo(0);
        await Assert.That(FocusAccessibility.ControlType(ReadingBlockKind.TableCell)).IsEqualTo(AutomationControlType.DataItem);
        await Assert.That(FocusAccessibility.ControlType(ReadingBlockKind.ListItem)).IsEqualTo(AutomationControlType.ListItem);
    }

    /// <summary>Opens a document with a heading, a two by two table and a list of two items, and checks how Focus Mode tells assistive technology about them.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="path">The document's path.</param>
    /// <returns>A task.</returns>
    private static async Task AssertTableAndListAsync(TestServices test, string path)
    {
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        await Assert.That(await UiWait.UntilAsync(() => main.SelectedTab!.IsLoaded)).IsTrue();
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            main.SelectedTab!.FocusMode.IsOn = true;
            _ = await UiWait.UntilAsync(() => Blocks(window).Count >= ExpectedBlocks);
            var blocks = Blocks(window);
            ReadingBlockKind[] kinds =
            [
                ReadingBlockKind.Heading,
                ReadingBlockKind.TableCell,
                ReadingBlockKind.TableCell,
                ReadingBlockKind.TableCell,
                ReadingBlockKind.TableCell,
                ReadingBlockKind.ListItem,
                ReadingBlockKind.ListItem,
            ];
            await Assert.That(blocks.Select(static block => block.ViewModel!.Kind)).IsEquivalentTo(kinds);
            var cell = blocks[CellIndex].ViewModel!;
            await Assert.That(cell.Text).IsEqualTo(StructuredTestPdf.DataCell);
            await Assert.That(cell.Row).IsEqualTo(Position);
            await Assert.That(cell.Column).IsEqualTo(1);
            await Assert.That(FocusAccessibility.Description(cell, true)).IsEqualTo("Table cell, row 2, column 1");
            await Assert.That(ControlAutomationPeer.CreatePeerForElement(blocks[CellIndex].BodyText).GetAutomationControlType()).IsEqualTo(AutomationControlType.DataItem);
            var second = blocks[^1].ViewModel!;
            await Assert.That(FocusAccessibility.Description(second, true)).IsEqualTo("List item 2 of 2");
            await Assert.That(AutomationProperties.GetPositionInSet(blocks[^1].BodyText)).IsEqualTo(Position);
            await Assert.That(AutomationProperties.GetSizeOfSet(blocks[^1].BodyText)).IsEqualTo(Position);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Makes a block view model.</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="level">The tagged heading level.</param>
    /// <returns>The block.</returns>
    private static FocusBlockViewModel Block(ReadingBlockKind kind, int level) =>
        new(0, new(kind, "Text", [0], default(PageRect), 1) { Level = level }, 0);

    /// <summary>Finds the Focus Mode blocks shown.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The block views.</returns>
    private static List<FocusBlockView> Blocks(Window window) =>
        [.. window.GetVisualDescendants().OfType<FocusBlockView>().Where(static b => b.ViewModel is not null && b.IsVisible)];
}
