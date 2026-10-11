// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests Split View: a second view of the same document beside the first.</summary>
public sealed class SplitViewTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 4;

    /// <summary>The page the first view shows.</summary>
    private const int ShownPage = 2;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1400;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The two views once split.</summary>
    private const int TwoViews = 2;

    /// <summary>Verifies splitting shares the document at the same page, and choosing it again, another tab or closing puts the second view away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitsAndUnsplits()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("split.pdf", Pages), test.CreateDocument("other.pdf", Pages)]);
        var tab = main.Tabs[0];
        main.SelectedTab = tab;
        tab.ReportPosition(new(ShownPage, 0), ShownPage);

        _ = await tab.SplitViewCommand.Execute().ToTask();
        var split = main.SplitTab!;
        await Assert.That(split.Source).IsSameReferenceAs(tab.Source);
        await Assert.That(split.IsSecondaryView).IsTrue();
        await Assert.That(split.CurrentPageIndex).IsEqualTo(ShownPage);
        await Assert.That(tab.IsSplitView && split.IsSplitView).IsTrue();

        _ = await split.SplitViewCommand.Execute().ToTask();
        await Assert.That(main.SplitTab).IsNull();
        await Assert.That(tab.IsSplitView).IsFalse();

        _ = await tab.SplitViewCommand.Execute().ToTask();
        main.SelectedTab = main.Tabs[1];
        await Assert.That(main.SplitTab).IsNull();

        main.SelectedTab = tab;
        _ = await tab.SplitViewCommand.Execute().ToTask();
        main.CloseTabWithoutAsking(tab);
        await Assert.That(main.SplitTab).IsNull();
    }

    /// <summary>Verifies the window shows both views side by side while split, then only the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShowsBothViews()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("split-window.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            _ = await main.SelectedTab!.SplitViewCommand.Execute().ToTask();

            await Assert.That(await UiWait.UntilAsync(() => Views(window).Count == TwoViews)).IsTrue();
            var views = Views(window);
            await Assert.That(views[0].Bounds.Width).IsGreaterThan(0);
            await Assert.That(views[1].Bounds.Width).IsGreaterThan(0);
            await Assert.That(window.FindControl<GridSplitter>("SplitSplitter")!.IsVisible).IsTrue();

            _ = await main.SelectedTab.SplitViewCommand.Execute().ToTask();
            await Assert.That(await UiWait.UntilAsync(() => Views(window).Count == 1)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Lists the document views shown.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The views.</returns>
    private static List<DocumentView> Views(Window window) =>
        [.. window.GetVisualDescendants().OfType<DocumentView>().Where(static view => view.IsEffectivelyVisible)];
}
