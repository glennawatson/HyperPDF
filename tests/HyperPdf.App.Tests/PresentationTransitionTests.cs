// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Presentation;
using HyperPdf.Core.Settings;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;

namespace HyperPdf.App.Tests;

/// <summary>Checks that presenting follows a document's page transitions and timings only as far as the reader allows.</summary>
public sealed class PresentationTransitionTests
{
    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>The pause used to check that a page does not move on, in milliseconds.</summary>
    private const int Stillness = 900;

    /// <summary>Seconds before now, by which a one second transition is long over.</summary>
    private const int LongAgoSeconds = 2;

    /// <summary>The progress under which a transition has only just started.</summary>
    private const double JustStarted = 0.1;

    /// <summary>The page with a wipe that stays a moment.</summary>
    private const int WipePage = 0;

    /// <summary>The page with a split that stays until the reader moves on.</summary>
    private const int SplitPage = 1;

    /// <summary>The page with a style the viewer only approximates.</summary>
    private const int GlitterPage = 2;

    /// <summary>Gets a three-page document: a wipe that stays 0.8 seconds, a split that stays, and a glitter.</summary>
    private static byte[] Slides => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Dur 0.8 /Trans << /Type /Trans /S /Wipe /D 0.6 /Di 0 >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Trans << /S /Split /D 0.3 /Dm /V /M /O >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Trans << /S /Glitter /D 0.3 >> >>");

    /// <summary>With both settings on, the first page wipes in, moves on by itself, and the next page splits in and stays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsTransitionsAndTimingsWhenAllowed()
    {
        using var test = new TestServices();
        test.Services.Settings.FollowPageTransitions = true;
        test.Services.Settings.FollowPageTimings = true;
        using var main = new MainViewModel(test.Services);
        var (window, tab, canvas) = await OpenAsync(test, main);
        try
        {
            tab.SetPresenting(true);
            var wiped = await UiWait.UntilAsync(() => tab.PagePresentation.Current is { Effect: TransitionEffect.Wipe });
            var drawing = await UiWait.UntilAsync(() => canvas.IsTransitioning);
            var moved = await UiWait.UntilAsync(() => tab.CurrentPageIndex == SplitPage);
            var split = tab.PagePresentation.Current;
            var ended = await UiWait.UntilAsync(() => !canvas.IsTransitioning);
            await Task.Delay(Stillness);

            await Assert.That(wiped).IsTrue();
            await Assert.That(drawing).IsTrue();
            await Assert.That(moved).IsTrue();
            await Assert.That(split?.Effect).IsEqualTo(TransitionEffect.Split);
            await Assert.That(split?.AdvanceAfterSeconds).IsNull();
            await Assert.That(ended).IsTrue();
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(SplitPage);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>With the settings as they are by default, nothing animates and no page changes by itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsNothingByDefault()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var (window, tab, canvas) = await OpenAsync(test, main);
        try
        {
            tab.SetPresenting(true);
            var planned = await UiWait.UntilAsync(() => tab.PagePresentation.Current is not null);
            await Task.Delay(Stillness);

            await Assert.That(planned).IsTrue();
            await Assert.That(tab.PagePresentation.Current?.Effect).IsEqualTo(TransitionEffect.None);
            await Assert.That(tab.PagePresentation.Current?.AdvanceAfterSeconds).IsNull();
            await Assert.That(canvas.IsTransitioning).IsFalse();
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(WipePage);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Reduced motion stops the effect and the timing even when both are allowed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReducedMotionStopsTheEffectAndTheTiming()
    {
        using var test = new TestServices();
        test.Services.Settings.FollowPageTransitions = true;
        test.Services.Settings.FollowPageTimings = true;
        test.Services.Settings.Motion = MotionPreference.Reduced;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        var (window, tab, canvas) = await OpenAsync(test, main);
        try
        {
            tab.SetPresenting(true);
            var planned = await UiWait.UntilAsync(() => tab.PagePresentation.Current is not null);
            await Task.Delay(Stillness);

            await Assert.That(planned).IsTrue();
            await Assert.That(tab.PagePresentation.Current?.Effect).IsEqualTo(TransitionEffect.None);
            await Assert.That(canvas.IsTransitioning).IsFalse();
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(WipePage);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The reader can move pages by hand during a presentation, and a page that waits does not move on after they leave it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ManualNavigationAlwaysWorks()
    {
        using var test = new TestServices();
        test.Services.Settings.FollowPageTransitions = true;
        test.Services.Settings.FollowPageTimings = true;
        using var main = new MainViewModel(test.Services);
        var (window, tab, _) = await OpenAsync(test, main);
        try
        {
            tab.SetPresenting(true);
            _ = await UiWait.UntilAsync(() => tab.PagePresentation.Current is { Effect: TransitionEffect.Wipe });

            // Leaving the first page before its time is up cancels its wait, so only the page they chose is shown.
            tab.GoToPage(GlitterPage);
            _ = await UiWait.UntilAsync(() => tab.PagePresentation.Current is { Style: TransitionStyle.Glitter });
            await Task.Delay(Stillness);

            await Assert.That(tab.CurrentPageIndex).IsEqualTo(GlitterPage);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>A style the viewer only approximates is explained once, in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsApproximatedStylesOnce()
    {
        using var test = new TestServices();
        test.Services.Settings.FollowPageTransitions = true;
        using var main = new MainViewModel(test.Services);
        var (window, tab, _) = await OpenAsync(test, main);
        try
        {
            tab.SetPresenting(true);
            tab.GoToPage(GlitterPage);
            var told = await UiWait.UntilAsync(() => tab.Notice is not null && tab.Notice.Contains("Glitter", StringComparison.Ordinal));
            var first = tab.Notice;
            tab.Notice = null;
            tab.GoToPage(SplitPage);
            tab.GoToPage(GlitterPage);
            await Task.Delay(Stillness);

            await Assert.That(told).IsTrue();
            await Assert.That(first).Contains("simpler effect");
            await Assert.That(tab.Notice).IsNull();
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>The page area is revealed or slid in by the direction the page asks for, to the nearest straight direction.</summary>
    /// <param name="degrees">The direction in degrees.</param>
    /// <param name="expected">The straight direction: 0 left to right, 1 bottom to top, 2 right to left, 3 top to bottom.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, 0)]
    [Arguments(44, 0)]
    [Arguments(46, 1)]
    [Arguments(90, 1)]
    [Arguments(180, 2)]
    [Arguments(270, 3)]
    [Arguments(350, 0)]
    [Arguments(360, 0)]
    [Arguments(-90, 3)]
    public async Task ReducesDirectionsToTheNearestStraightOne(int degrees, int expected) =>
        await Assert.That((int)TransitionTiming.GetDirection(degrees)).IsEqualTo(expected);

    /// <summary>The curtain of a transition starts covering the whole page and ends covering nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EasesFromNothingToEverything()
    {
        PresentationStep step = new(1, TransitionStyle.Wipe, TransitionEffect.Wipe, 1, false, false, 0, false, null);
        var now = Stopwatch.GetTimestamp();
        var started = new TransitionRun(true, step, now).Progress;
        var finished = new TransitionRun(true, step, now - (Stopwatch.Frequency * LongAgoSeconds)).Progress;

        await Assert.That(started).IsLessThan(JustStarted);
        await Assert.That(finished).IsEqualTo(1);
    }

    /// <summary>Opens the slides in a window.</summary>
    /// <param name="test">The services.</param>
    /// <param name="main">The main view model.</param>
    /// <returns>The window, tab and page canvas.</returns>
    private static async Task<(Window Window, DocumentTabViewModel Tab, PageCanvas Canvas)> OpenAsync(TestServices test, MainViewModel main)
    {
        var path = Path.Combine(test.Directory, "slides.pdf");
        await File.WriteAllBytesAsync(path, Slides);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > 0 && window.GetVisualDescendants().OfType<PageCanvas>().Any(static c => c.IsFocused));
        return (window, main.SelectedTab!, window.GetVisualDescendants().OfType<PageCanvas>().Single());
    }
}
