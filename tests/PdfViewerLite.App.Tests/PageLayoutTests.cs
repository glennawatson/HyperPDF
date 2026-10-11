// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Layout;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks the single page, two page and page by page arrangements in the real window.</summary>
public sealed class PageLayoutTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of the test document.</summary>
    private const int Pages = 10;

    /// <summary>The number of two page rows the test document fills.</summary>
    private const int DualRows = Pages / 2;

    /// <summary>The zoom used when comparing arrangements, small enough for two pages to fit side by side.</summary>
    private const string SmallZoom = "50";

    /// <summary>The left page of the second spread.</summary>
    private const int SecondSpread = 2;

    /// <summary>The left page of the third spread.</summary>
    private const int ThirdSpread = 4;

    /// <summary>The right page of the second spread, as typed into the page box.</summary>
    private const string RightPageEntry = "4";

    /// <summary>The zero based index of <see cref="RightPageEntry"/>.</summary>
    private const int RightPage = 3;

    /// <summary>The page shown before switching the arrangement.</summary>
    private const int KeptPage = 6;

    /// <summary>How close, in pixels, two lengths must be to count as equal.</summary>
    private const double Tolerance = 1;

    /// <summary>The share of the one page height that two pages side by side stay under.</summary>
    private const double HalvedHeight = 0.6;

    /// <summary>Verifies Two Pages puts pages side by side and the menu shows which arrangement is chosen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TwoPagesPutsPagesSideBySide()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("dual.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            _ = tab.SetZoomCommand.Execute(SmallZoom).Subscribe();
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height > scroller.Viewport.Height);
            var singleHeight = scroller.Extent.Height;

            _ = tab.SetLayoutCommand.Execute(nameof(PageLayoutMode.Dual)).Subscribe();
            var halved = await UiWait.UntilAsync(() => scroller.Extent.Height < singleHeight * HalvedHeight);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "two-pages.png");

            await Assert.That(tab.LayoutMode).IsEqualTo(PageLayoutMode.Dual);
            await Assert.That(halved).IsTrue();
            await Assert.That(view.DualLayoutItem.IsChecked).IsTrue();
            await Assert.That(view.SingleLayoutItem.IsChecked).IsFalse();
            await Assert.That(view.CoverLayoutItem.IsChecked).IsFalse();

            _ = tab.SetLayoutCommand.Execute(nameof(PageLayoutMode.Single)).Subscribe();
            var restored = await UiWait.UntilAsync(() => Math.Abs(scroller.Extent.Height - singleHeight) < Tolerance);

            await Assert.That(restored).IsTrue();
            await Assert.That(view.SingleLayoutItem.IsChecked).IsTrue();
            await Assert.That(view.DualLayoutItem.IsChecked).IsFalse();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies Next and Previous move a whole spread at a time when two pages are shown one spread at a time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NextPageMovesBySpread()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("spreads.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            tab.LayoutMode = PageLayoutMode.Dual;
            tab.ZoomMode = ZoomMode.FitPage;
            tab.IsPageByPage = true;
            _ = await UiWait.UntilAsync(() => Math.Abs(scroller.Extent.Height - (scroller.Viewport.Height * DualRows)) < Tolerance);
            var viewport = scroller.Viewport.Height;

            _ = tab.NextPageCommand.Execute().Subscribe();
            var second = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SecondSpread && Math.Abs(scroller.Offset.Y - viewport) < Tolerance);
            _ = tab.NextPageCommand.Execute().Subscribe();
            var third = await UiWait.UntilAsync(() => tab.CurrentPageIndex == ThirdSpread && Math.Abs(scroller.Offset.Y - (viewport * SecondSpread)) < Tolerance);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "two-pages-one-spread.png");
            _ = tab.PreviousPageCommand.Execute().Subscribe();
            var back = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SecondSpread && Math.Abs(scroller.Offset.Y - viewport) < Tolerance);

            await Assert.That(second).IsTrue();
            await Assert.That(third).IsTrue();
            await Assert.That(back).IsTrue();
            await Assert.That(scroller.Extent.Width).IsLessThanOrEqualTo(scroller.Viewport.Width + Tolerance);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies fit width with two pages fills the width with the widest pair and never scrolls sideways.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FitWidthFitsThePair()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("fit.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            tab.ZoomMode = ZoomMode.FitWidth;
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height > scroller.Viewport.Height);
            var singleZoom = tab.Zoom;

            tab.LayoutMode = PageLayoutMode.Dual;
            var refitted = await UiWait.UntilAsync(() => tab.Zoom < singleZoom * HalvedHeight);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "two-pages-fit-width.png");

            await Assert.That(refitted).IsTrue();
            await Assert.That(scroller.Extent.Width).IsLessThanOrEqualTo(scroller.Viewport.Width + Tolerance);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies typing the right hand page of a spread shows that spread and keeps the typed page in the page box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageBoxKeepsRightHandPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("entry.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            tab.LayoutMode = PageLayoutMode.Dual;
            tab.ZoomMode = ZoomMode.FitPage;
            tab.IsPageByPage = true;
            _ = await UiWait.UntilAsync(() => Math.Abs(scroller.Extent.Height - (scroller.Viewport.Height * DualRows)) < Tolerance);

            tab.PageEntry = RightPageEntry;
            _ = tab.GoToPageEntryCommand.Execute().Subscribe();
            var shown = await UiWait.UntilAsync(() => Math.Abs(scroller.Offset.Y - scroller.Viewport.Height) < Tolerance);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            await Assert.That(shown).IsTrue();
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(RightPage);
            await Assert.That(tab.PageEntry).IsEqualTo(RightPageEntry);

            _ = tab.NextPageCommand.Execute().Subscribe();
            var next = await UiWait.UntilAsync(() => tab.CurrentPageIndex == ThirdSpread);

            await Assert.That(next).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies switching between one and two pages keeps the page being read on screen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SwitchingArrangementKeepsThePage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("keep.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var scroller = window.GetVisualDescendants().OfType<PageCanvas>().Single().FindAncestorOfType<ScrollViewer>()!;
            tab.ZoomMode = ZoomMode.FitPage;
            _ = await UiWait.UntilAsync(() => scroller.Extent.Height > scroller.Viewport.Height);
            tab.GoToPage(KeptPage);
            _ = await UiWait.UntilAsync(() => tab.CurrentPageIndex == KeptPage);

            tab.LayoutMode = PageLayoutMode.Dual;
            var dual = await UiWait.UntilAsync(() => tab.LayoutMode == PageLayoutMode.Dual && tab.CurrentPageIndex == KeptPage && tab.Position.PageIndex == KeptPage);
            tab.LayoutMode = PageLayoutMode.DualCover;
            var cover = await UiWait.UntilAsync(() => tab.CurrentPageIndex == KeptPage);
            using var frame = window.CaptureRenderedFrame();
            Save(frame, "two-pages-cover.png");
            tab.LayoutMode = PageLayoutMode.Single;
            var single = await UiWait.UntilAsync(() => tab.CurrentPageIndex == KeptPage && tab.Position.PageIndex == KeptPage);

            await Assert.That(dual).IsTrue();
            await Assert.That(cover).IsTrue();
            await Assert.That(single).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies every arrangement menu item explains itself in a tool tip and to screen readers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LayoutItemsExplainThemselves()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("help.pdf", 1)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = window.GetVisualDescendants().OfType<DocumentView>().Single();
            foreach (var item in (MenuItem[])[view.SingleLayoutItem, view.DualLayoutItem, view.CoverLayoutItem, view.PageByPageItem])
            {
                await Assert.That(ToolTip.GetTip(item) as string).IsNotNullOrEmpty();
                await Assert.That(AutomationProperties.GetHelpText(item)).IsNotNullOrEmpty();
                await Assert.That(item.ToggleType).IsNotEqualTo(MenuItemToggleType.None);
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Saves a frame when <c>PDFVIEWERLITE_SCREENSHOTS</c> names a folder.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="name">The file name.</param>
    private static void Save(WriteableBitmap? frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PDFVIEWERLITE_SCREENSHOTS");
        if (frame is null || string.IsNullOrEmpty(directory))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
