// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.Core.Layout;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the horizontal and wrapped scroll modes in the real window.</summary>
public sealed class ScrollModeTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of the test document.</summary>
    private const int Pages = 12;

    /// <summary>The zoom used so that several pages fit across the window.</summary>
    private const string SmallZoom = "40";

    /// <summary>The page after the first.</summary>
    private const int SecondPage = 1;

    /// <summary>How many pages a wheel turn must move sideways at least, in pixels.</summary>
    private const double MinimumWheelMove = 1;

    /// <summary>The x of the point the wheel turns at, inside the pages.</summary>
    private const double WheelX = 550;

    /// <summary>The y of the point the wheel turns at, inside the pages.</summary>
    private const double WheelY = 400;

    /// <summary>The share of the one page per row height that wrapped pages stay under.</summary>
    private const double HalvedHeight = 0.6;

    /// <summary>Verifies that Scroll Sideways lays every page in one row that scrolls from side to side, and that the menu shows it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_LaysPagesInOneRow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("sideways.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            _ = tab.SetZoomCommand.Execute(SmallZoom).Subscribe();
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height > scroller.Viewport.Height);

            _ = tab.SetScrollModeCommand.Execute(nameof(ScrollMode.Horizontal)).Subscribe();
            var wide = await UiWait.UntilAsync(() => scroller.Extent.Width > scroller.Viewport.Width && scroller.Extent.Height <= scroller.Viewport.Height + 1);

            await Assert.That(wide).IsTrue();
            await Assert.That(tab.ScrollMode).IsEqualTo(ScrollMode.Horizontal);
            await Assert.That(view.HorizontalScrollItem.IsChecked).IsTrue();
            await Assert.That(view.VerticalScrollItem.IsChecked).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that the mouse wheel moves a horizontal layout sideways and not down.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_WheelMovesSideways()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("wheel-sideways.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            tab.ScrollMode = ScrollMode.Horizontal;
            _ = tab.SetZoomCommand.Execute(SmallZoom).Subscribe();
            _ = await UiWait.UntilAsync(() => scroller.Extent.Width > scroller.Viewport.Width);
            var before = scroller.Offset;

            window.MouseWheel(new(WheelX, WheelY), new(0, -1));
            var moved = await UiWait.UntilAsync(() => scroller.Offset.X - before.X > MinimumWheelMove);

            await Assert.That(moved).IsTrue();
            await Assert.That(scroller.Offset.Y).IsEqualTo(before.Y);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that Next page steps one page in a horizontal layout and scrolls it into view.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Horizontal_NextPageStepsOnePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("next-sideways.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            tab.ScrollMode = ScrollMode.Horizontal;
            _ = tab.SetZoomCommand.Execute(SmallZoom).Subscribe();
            _ = await UiWait.UntilAsync(() => scroller.Extent.Width > scroller.Viewport.Width);

            _ = tab.NextPageCommand.Execute().Subscribe();
            var stepped = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SecondPage && scroller.Offset.X > 0);

            await Assert.That(stepped).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies that wrapped pages share rows, so the document is shorter than when it scrolls down one page at a time.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Wrapped_FitsSeveralPagesInARow()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("wrapped.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            _ = tab.SetZoomCommand.Execute(SmallZoom).Subscribe();
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height > scroller.Viewport.Height);
            var downHeight = scroller.Extent.Height;

            _ = tab.SetScrollModeCommand.Execute(nameof(ScrollMode.Wrapped)).Subscribe();
            var shorter = await UiWait.UntilAsync(() => scroller.Extent.Height < downHeight * HalvedHeight);

            await Assert.That(shorter).IsTrue();
            await Assert.That(view.WrappedScrollItem.IsChecked).IsTrue();
            await Assert.That(scroller.Extent.Width).IsLessThanOrEqualTo(scroller.Viewport.Width + 1);
        }
        finally
        {
            window.Close();
        }
    }
}
