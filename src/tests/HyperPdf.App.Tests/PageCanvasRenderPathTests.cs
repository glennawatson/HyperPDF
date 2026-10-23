// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the canvas skips repeated work on an unchanged view without losing tile requests or position reports.</summary>
public sealed class PageCanvasRenderPathTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of the test document.</summary>
    private const int Pages = 40;

    /// <summary>How far down the document the scroll test goes, in pixels.</summary>
    private const double ScrollDistance = 3000;

    /// <summary>The milliseconds the first frame's requests get to settle before the cache is emptied.</summary>
    private const int SettleMilliseconds = 250;

    /// <summary>Verifies that tiles are asked for again after the cache is emptied and the same view is repainted.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Repaint_AsksForTilesAgainAfterTheCacheIsCleared()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("evicted.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var filled = await UiWait.UntilAsync(() => tab.RenderHub.Cache.Count > 0);
            await Assert.That(filled).IsTrue();

            await Task.Delay(TimeSpan.FromMilliseconds(SettleMilliseconds));
            tab.RenderHub.Cache.Clear();
            canvas.InvalidateVisual();

            var refilled = await UiWait.UntilAsync(() => tab.RenderHub.Cache.Count > 0);
            await Assert.That(refilled).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that a scroll reports the page now at the top, and that reporting the same view again changes nothing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReportPosition_FollowsTheScrollAndRepeatsAreHarmless()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("report.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height > ScrollDistance);

            scroller.Offset = new(0, ScrollDistance);
            var moved = await UiWait.UntilAsync(() => tab.CurrentPageIndex > 0);
            await Assert.That(moved).IsTrue();

            var page = tab.CurrentPageIndex;
            var position = tab.Position;
            canvas.ReportPosition();
            canvas.ReportPosition();
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(page);
            await Assert.That(tab.Position).IsEqualTo(position);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that a tab with no search hits or reading has no marks, so the layer does not redraw on scroll.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MarksLayer_HasNoMarksWithoutHitsOrReading()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("marks.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var layer = window.GetVisualDescendants().OfType<PageMarksLayer>().Single();
            await Assert.That(layer.HasMarks).IsFalse();
            await Assert.That(main.SelectedTab!.Search.HasHits).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
