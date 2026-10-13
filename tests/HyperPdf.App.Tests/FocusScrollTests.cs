// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.VisualTree;
using HyperPdf.App.ViewModels;
using HyperPdf.App.Views;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks that Focus Mode scrolls to the place asked for and settles there.</summary>
public sealed class FocusScrollTests
{
    /// <summary>The pages in the article.</summary>
    private const int Pages = 40;

    /// <summary>The page scrolled to first.</summary>
    private const int FarPage = 30;

    /// <summary>The page scrolled to second, nearer the top.</summary>
    private const int NearPage = 5;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>How far a page may sit below the top of the viewport and still count as scrolled to.</summary>
    private const double Slack = 24;

    /// <summary>The pump rounds that show the view stays where it settled.</summary>
    private const int SettleRounds = 10;

    /// <summary>A lone scroll request brings its page to the top of the viewport and the view settles there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScrollRequestEndsAtItsPage()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "article.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await OpenFocusAsync(window, tab);

            tab.FocusMode.ScrollTo(FarPage, 0);

            await WaitForAsync(view, FarPage);
            await UiWait.PumpAsync(SettleRounds);
            await Assert.That(AtTop(view, FarPage)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A newer request made while an older page is still loading wins; the older one never moves the view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewerRequestWinsWhileTheOlderPageLoads()
    {
        var engine = new PausingEngine { ExposesFeatures = true };
        using var test = new TestServices(engine);
        var path = Path.Combine(test.Directory, "article.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateArticle(Pages));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await OpenFocusAsync(window, tab);

            // The near page is loaded before the far page is held, so the newer request does not wait for the older one.
            _ = await UiWait.UntilAsync(() => tab.FocusMode.Pages[NearPage].Blocks.Count > 0, static () => "The near page did not load.");
            var older = engine.ArmLayout(FarPage);
            tab.FocusMode.ScrollTo(FarPage, 0);
            _ = await UiWait.UntilAsync(() => older.Reached.Task.IsCompleted, static () => "The far page did not start loading.");

            tab.FocusMode.ScrollTo(NearPage, 0);
            await WaitForAsync(view, NearPage);

            // The far page finishes loading after the newer scroll; its older scroll must not move the view.
            older.Release();
            _ = await UiWait.UntilAsync(() => tab.FocusMode.Pages[FarPage].Blocks.Count > 0, static () => "The far page did not finish loading.");

            // Loading is done; the rounds give its older scroll the chance to move the view, which it must not.
            await UiWait.PumpAsync(SettleRounds);
            await Assert.That(tab.FocusMode.Pages[FarPage].Blocks.Count).IsGreaterThan(0);
            await Assert.That(AtTop(view, NearPage)).IsTrue();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Turns Focus Mode on and waits until its first page is shown.</summary>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>Focus Mode's view.</returns>
    private static async Task<FocusView> OpenFocusAsync(MainWindow window, DocumentTabViewModel tab)
    {
        tab.FocusMode.IsOn = true;
        var view = window.GetVisualDescendants().OfType<FocusView>().Single();
        _ = await UiWait.UntilAsync(() => view.PageViews().Count > 0, static () => "Focus Mode showed no page.");
        return view;
    }

    /// <summary>Waits until a page is at the top of the view.</summary>
    /// <param name="view">Focus Mode.</param>
    /// <param name="page">The page.</param>
    /// <returns>A task.</returns>
    private static async Task WaitForAsync(FocusView view, int page) =>
        _ = await UiWait.UntilAsync(() => AtTop(view, page), () => $"Page {page} is not at the top: offset {view.FocusScroller.Offset}, extent {view.FocusScroller.Extent}.");

    /// <summary>Whether a page's top is at the top of the viewport.</summary>
    /// <param name="view">Focus Mode.</param>
    /// <param name="page">The page.</param>
    /// <returns>Whether the page is scrolled to.</returns>
    private static bool AtTop(FocusView view, int page)
    {
        foreach (var found in view.PageViews())
        {
            if (found.ViewModel?.PageIndex == page)
            {
                return found.TranslatePoint(default, view.FocusScroller) is { } point && point.Y <= Slack && point.Y + found.Bounds.Height > 0;
            }
        }

        return false;
    }
}
