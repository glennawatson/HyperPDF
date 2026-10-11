// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

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
