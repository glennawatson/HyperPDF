// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Settings;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks that Ctrl and the wheel only zoom, and keep the content under the pointer still.</summary>
public sealed class WheelZoomAnchorTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of the document.</summary>
    private const int Pages = 40;

    /// <summary>The page width in points.</summary>
    private const float PageWidth = 612;

    /// <summary>The page height in points.</summary>
    private const float PageHeight = 792;

    /// <summary>The number of notches zoomed in and then out.</summary>
    private const int Notches = 8;

    /// <summary>The most a point may move, in device independent pixels.</summary>
    private const double Tolerance = 1.5;

    /// <summary>The zoom the tests start from.</summary>
    private const double StartZoom = 1;

    /// <summary>The zoom that leaves blank area beside the pages.</summary>
    private const double SmallZoom = 0.25;

    /// <summary>The page the tests start at.</summary>
    private const int StartPage = 10;

    /// <summary>The least a plain wheel notch scrolls, in pixels.</summary>
    private const double MinWheelScroll = 10;

    /// <summary>The share of the viewport that is the middle.</summary>
    private const double Middle = 0.5;

    /// <summary>How far in from the right edge the empty area is probed, in pixels.</summary>
    private const double EdgeInset = 4;

    /// <summary>Verifies the point under the pointer stays still for several pointer positions.</summary>
    /// <param name="name">The scenario name.</param>
    /// <param name="viewX">The pointer's share of the viewport width.</param>
    /// <param name="viewY">The pointer's share of the viewport height.</param>
    /// <param name="pageFraction">How far down the start page the view starts.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("middle", 0.5, 0.5, 0.0)]
    [Arguments("top-edge", 0.3, 0.02, 0.0)]
    [Arguments("page-gap", 0.5, 0.5, 0.985)]
    [Arguments("below-page", 0.4, 0.9, 0.5)]
    [Arguments("page-boundary", 0.6, 0.25, 0.8)]
    public Task InstantZoomKeepsPointerContentStill(string name, double viewX, double viewY, double pageFraction) =>
        RunAsync(new(name, viewX, viewY, pageFraction), MotionPreference.Reduced, false);

    /// <summary>Verifies the point under the pointer stays still when the zoom eases.</summary>
    /// <param name="name">The scenario name.</param>
    /// <param name="viewX">The pointer's share of the viewport width.</param>
    /// <param name="viewY">The pointer's share of the viewport height.</param>
    /// <param name="pageFraction">How far down the start page the view starts.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("middle", 0.5, 0.5, 0.0)]
    [Arguments("page-gap", 0.5, 0.5, 0.985)]
    [Arguments("page-boundary", 0.6, 0.25, 0.8)]
    public Task EasedZoomKeepsPointerContentStill(string name, double viewX, double viewY, double pageFraction) =>
        RunAsync(new(name, viewX, viewY, pageFraction), MotionPreference.Normal, false);

    /// <summary>Verifies the point stays still with the thumbnails sidebar following the current page.</summary>
    /// <param name="name">The scenario name.</param>
    /// <param name="viewX">The pointer's share of the viewport width.</param>
    /// <param name="viewY">The pointer's share of the viewport height.</param>
    /// <param name="pageFraction">How far down the start page the view starts.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("page-gap", 0.5, 0.5, 0.985)]
    [Arguments("page-boundary", 0.6, 0.25, 0.8)]
    [Arguments("below-page", 0.4, 0.9, 0.5)]
    public Task ZoomWithThumbnailsKeepsPointerContentStill(string name, double viewX, double viewY, double pageFraction) =>
        RunAsync(new(name, viewX, viewY, pageFraction), MotionPreference.Reduced, true);

    /// <summary>Verifies Ctrl and the wheel zoom without scrolling with the pointer at the viewport's right edge, beside small pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CtrlWheelAtTheViewportEdgeZooms()
    {
        using var test = new TestServices();
        test.Services.Settings.Motion = MotionPreference.Reduced;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("outside.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = await StartAsync(test, window, tab, 0);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;

            // A small page leaves blank canvas beside it, where the canvas draws nothing to hit.
            tab.SetZoom(SmallZoom);
            Pump();
            var local = new Point(scroller.Viewport.Width - EdgeInset, scroller.Viewport.Height * Middle);
            var rig = new Rig(window, canvas, scroller, tab, scroller.TranslatePoint(local, window)!.Value, local);
            var zoom = tab.Zoom;
            await WheelAndCheckAsync(rig, new(0, 1), "outside");
            await Assert.That(tab.Zoom).IsGreaterThan(zoom);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies the plain wheel still scrolls.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainWheelStillScrolls()
    {
        using var test = new TestServices();
        test.Services.Settings.Motion = MotionPreference.Reduced;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("plain.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            var canvas = await StartAsync(test, window, tab, 0);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            var point = scroller.TranslatePoint(new(scroller.Viewport.Width * Middle, scroller.Viewport.Height * Middle), window)!.Value;
            var before = scroller.Offset.Y;
            var zoom = tab.Zoom;
            window.MouseWheel(point, new(0, -1));
            Pump();
            await Assert.That(scroller.Offset.Y - before).IsGreaterThan(MinWheelScroll);
            await Assert.That(tab.Zoom).IsEqualTo(zoom);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Runs repeated Ctrl and wheel zooms in then out, checking the anchor after every notch.</summary>
    /// <param name="scenario">Where the pointer is.</param>
    /// <param name="motion">The motion preference.</param>
    /// <param name="thumbnails">Whether the thumbnails sidebar is shown.</param>
    /// <returns>A task.</returns>
    private static async Task RunAsync(Scenario scenario, MotionPreference motion, bool thumbnails)
    {
        using var test = new TestServices();
        test.Services.Settings.Motion = motion;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument($"{scenario.Name}.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            if (thumbnails)
            {
                tab.SidebarMode = SidebarMode.Thumbnails;
                tab.SidebarVisible = true;
            }

            var canvas = await StartAsync(test, window, tab, scenario.PageFraction);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            var local = new Point(scroller.Viewport.Width * scenario.ViewX, scroller.Viewport.Height * scenario.ViewY);
            var rig = new Rig(window, canvas, scroller, tab, scroller.TranslatePoint(local, window)!.Value, local);
            for (var notch = 0; notch < Notches + Notches; notch++)
            {
                await WheelAndCheckAsync(rig, new(0, notch < Notches ? 1 : -1), $"{scenario.Name}-{notch}");
            }
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Sends one notch and checks the page point under the pointer did not move.</summary>
    /// <param name="rig">The views and pointer.</param>
    /// <param name="delta">The wheel delta.</param>
    /// <param name="label">Names the notch in a failure.</param>
    /// <returns>A task.</returns>
    private static async Task WheelAndCheckAsync(Rig rig, Vector delta, string label)
    {
        var (page, point) = FindPagePoint(rig.Canvas, rig.Local + rig.Scroller.Offset);
        var before = rig.Tab.Zoom;
        rig.Window.MouseWheel(rig.Pointer, delta, RawInputModifiers.Control);
        _ = await UiWait.UntilAsync(() => !rig.Canvas.IsZoomEasing);
        Pump();
        var drift = (rig.Local + rig.Scroller.Offset) - rig.Canvas.PageToCanvas(page, point);
        var moved = string.Create(CultureInfo.InvariantCulture, $"zoom {before:F3}->{rig.Tab.Zoom:F3} page={rig.Tab.CurrentPageIndex} offset={rig.Scroller.Offset.Y:F1}");
        TestContext.Current?.Output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{label}: {moved} drift=({drift.X:F2},{drift.Y:F2})"));
        await Assert.That(Math.Abs(drift.Y)).IsLessThanOrEqualTo(Tolerance);
        if (rig.Scroller.Extent.Width <= rig.Scroller.Viewport.Width)
        {
            return;
        }

        await Assert.That(Math.Abs(drift.X)).IsLessThanOrEqualTo(Tolerance);
    }

    /// <summary>Opens the view at a zoom and position, and waits for it to settle.</summary>
    /// <param name="test">The services.</param>
    /// <param name="window">The window.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="pageFraction">How far down the start page the view starts.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> StartAsync(TestServices test, Window window, DocumentTabViewModel tab, double pageFraction)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
        var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
        _ = await UiWait.UntilAsync(() => scroller.Viewport.Height > 0 && tab.PageCount == Pages);
        tab.SetZoom(StartZoom);
        _ = await UiWait.UntilAsync(() => scroller.Extent.Height > Pages * PageHeight * StartZoom);
        tab.NavigateTo(new(StartPage, null, pageFraction));
        _ = await UiWait.UntilAsync(() => tab.CurrentPageIndex >= StartPage && test.Services.RenderHub.Scheduler.IsIdle);
        Pump();
        return canvas;
    }

    /// <summary>Finds the page, and the point on it, under a canvas point, extrapolating across gaps.</summary>
    /// <param name="canvas">The page canvas.</param>
    /// <param name="at">The canvas point.</param>
    /// <returns>The page and the page point.</returns>
    private static PageLocation FindPagePoint(PageCanvas canvas, Point at)
    {
        var best = 0;
        var bestDistance = double.MaxValue;
        for (var page = 0; page < Pages; page++)
        {
            var top = canvas.PageToCanvas(page, new(0, PageHeight)).Y;
            var bottom = canvas.PageToCanvas(page, new(0, 0)).Y;
            var distance = Math.Max(Math.Max(top - at.Y, at.Y - bottom), 0);
            if (distance >= bestDistance)
            {
                continue;
            }

            best = page;
            bestDistance = distance;
        }

        var origin = canvas.PageToCanvas(best, new(0, 0));
        var far = canvas.PageToCanvas(best, new(PageWidth, PageHeight));
        var x = (float)((at.X - origin.X) / (far.X - origin.X) * PageWidth);
        var y = (float)((at.Y - origin.Y) / (far.Y - origin.Y) * PageHeight);
        return new(best, new PagePoint(x, y));
    }

    /// <summary>Runs queued work and a frame.</summary>
    private static void Pump()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Where the pointer sits and how far down the start page the view starts.</summary>
    /// <param name="Name">The scenario name.</param>
    /// <param name="ViewX">The pointer's share of the viewport width.</param>
    /// <param name="ViewY">The pointer's share of the viewport height.</param>
    /// <param name="PageFraction">How far down the start page the view starts.</param>
    private readonly record struct Scenario(string Name, double ViewX, double ViewY, double PageFraction);

    /// <summary>The views and the pointer a notch is sent through.</summary>
    /// <param name="Window">The window.</param>
    /// <param name="Canvas">The page canvas.</param>
    /// <param name="Scroller">The scroll viewer.</param>
    /// <param name="Tab">The tab.</param>
    /// <param name="Pointer">The pointer in window coordinates.</param>
    /// <param name="Local">The pointer in scroll viewer coordinates.</param>
    private sealed record Rig(Window Window, PageCanvas Canvas, ScrollViewer Scroller, DocumentTabViewModel Tab, Point Pointer, Point Local);
}
