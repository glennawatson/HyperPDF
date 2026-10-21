// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Views;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

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

    /// <summary>The side of a marker tile.</summary>
    private const int TileSide = 8;

    /// <summary>Where the rectangle drawn as an edit starts, in points.</summary>
    private const float ShapeStart = 10;

    /// <summary>Where the rectangle drawn as an edit ends, in points.</summary>
    private const float ShapeEnd = 60;

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

    /// <summary>Verifies the two views keep their tiles apart, an edit drops both, and putting the second view away keeps the first view's tiles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ViewsKeepTheirTilesApart()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("split-tiles.pdf", Pages)]);
        var tab = main.SelectedTab!;
        _ = await tab.SplitViewCommand.Execute().ToTask();
        var split = main.SplitTab!;
        var cache = test.Services.RenderHub.Cache;
        var first = TileKey.Preview(tab.TileId, 0, PageRotation.None, PageTone.None.Id);
        var second = TileKey.Preview(split.TileId, 0, PageRotation.None, PageTone.None.Id);
        cache.Add(first, new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software));
        cache.Add(second, new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software));

        _ = await split.SplitViewCommand.Execute().ToTask();
        var firstKeptWhenSecondClosed = cache.Contains(first);
        var secondDroppedWhenClosed = !cache.Contains(second);
        _ = await tab.SplitViewCommand.Execute().ToTask();
        var splitAgain = main.SplitTab!;
        var secondAgain = TileKey.Preview(splitAgain.TileId, 0, PageRotation.None, PageTone.None.Id);
        cache.Add(secondAgain, new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability(), TestTiles.Software));
        var marked = tab.Annotations.AddShape(0, HyperPdf.Core.Annotations.AnnotationKind.Rectangle, new(ShapeStart, ShapeStart), new(ShapeEnd, ShapeEnd));

        using (Assert.Multiple())
        {
            await Assert.That(marked).IsTrue();
            await Assert.That(split.TileId).IsNotEqualTo(tab.TileId);
            await Assert.That(firstKeptWhenSecondClosed).IsTrue();
            await Assert.That(secondDroppedWhenClosed).IsTrue();
            await Assert.That(cache.Contains(first)).IsFalse();
            await Assert.That(cache.Contains(secondAgain)).IsFalse();
        }
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
            await Assert.That(window.FindControl<DocumentAreaView>("DocumentArea")!.FindControl<GridSplitter>("SplitSplitter")!.IsVisible).IsTrue();

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
