// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Pdfium;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>
/// Checks that every frame drawn while scrolling or zooming already looks like the settled page, so pages never flash
/// blank or blurry before their tiles arrive.
/// </summary>
public sealed class FrameStabilityTests
{
    /// <summary>The window width.</summary>
    private const int WindowWidth = 1100;

    /// <summary>The window height.</summary>
    private const int WindowHeight = 800;

    /// <summary>The page count of the document.</summary>
    private const int Pages = 40;

    /// <summary>The number of cached tiles that shows rendering is well under way.</summary>
    private const int ExpectedTiles = 6;

    /// <summary>The scroll distance per step, in canvas units.</summary>
    private const double ScrollStep = 90;

    /// <summary>The number of scroll steps.</summary>
    private const int ScrollSteps = 24;

    /// <summary>The number of wheel notches zoomed in and then out.</summary>
    private const int ZoomNotches = 6;

    /// <summary>The bytes in a captured pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The green channel of a BGRA pixel, which carries most of the brightness.</summary>
    private const int Green = 1;

    /// <summary>The brightness jump between neighbouring pixels that counts as a sharp edge.</summary>
    private const int EdgeContrast = 48;

    /// <summary>
    /// The least share of the settled frame's sharp edges the first frame must already show. A frame showing only the
    /// blurred preview keeps about 1%; the last sharp tiles stretched bilinearly by a wheel notch keep over 45%.
    /// </summary>
    private const double MinSharpness = 0.4;

    /// <summary>The zoom change of one wheel notch.</summary>
    private const double WheelStep = 1.1;

    /// <summary>The zoom values two instant notches produce: the start and one per notch.</summary>
    private const int InstantZoomValues = 3;

    /// <summary>How close two zoom factors must be to count as equal.</summary>
    private const double ZoomTolerance = 1e-6;

    /// <summary>Where the wheel zoom is anchored across the viewport.</summary>
    private const double AnchorX = 0.5;

    /// <summary>Where the wheel zoom is anchored down the viewport.</summary>
    private const double AnchorY = 0.3;

    /// <summary>How long to wait for a tile that is still rendering.</summary>
    private static readonly TimeSpan SettlePause = TimeSpan.FromMilliseconds(150);

    /// <summary>Verifies frames drawn while scrolling are as sharp as the settled frames.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScrollingDrawsSettledFrames()
    {
        using var test = new TestServices(TestEngineChoice.Pdfium);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("scroll.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var canvas = await SettleAsync(test, window);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            var worst = 1.0;
            for (var step = 0; step < ScrollSteps; step++)
            {
                worst = Math.Min(worst, await CompareWithSettledAsync(test, window, scroller, () => scroller.Offset = new(scroller.Offset.X, scroller.Offset.Y + ScrollStep), $"scroll-{step}"));
            }

            await Assert.That(worst).IsGreaterThanOrEqualTo(MinSharpness);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies frames drawn while zooming with the wheel stay nearly as sharp as the settled frames.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelZoomDrawsSettledFrames()
    {
        using var test = new TestServices();

        // Without easing each notch lands at once, so the first frame and the settled frame show the same zoom.
        test.Services.Settings.Motion = MotionPreference.Reduced;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("zoom.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var canvas = await SettleAsync(test, window);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            var anchor = scroller.TranslatePoint(new(scroller.Viewport.Width * AnchorX, scroller.Viewport.Height * AnchorY), window)!.Value;
            var worst = 1.0;
            for (var notch = 0; notch < ZoomNotches + ZoomNotches; notch++)
            {
                var delta = new Vector(0, notch < ZoomNotches ? 1 : -1);
                worst = Math.Min(worst, await CompareWithSettledAsync(test, window, scroller, () => window.MouseWheel(anchor, delta, RawInputModifiers.Control), $"zoom-{notch}"));
            }

            await Assert.That(worst).IsGreaterThanOrEqualTo(MinSharpness);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies a wheel notch eases to its zoom rather than jumping, and lands exactly on it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelZoomEasesToTarget()
    {
        using var test = new TestServices();
        test.Services.Settings.Motion = MotionPreference.Normal;
        test.Services.ApplySettings();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("ease.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var canvas = await SettleAsync(test, window);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            var tab = main.SelectedTab!;
            var target = tab.Zoom * WheelStep * WheelStep;
            var zooms = new List<double>();
            using var recording = tab.WhenChanged(static x => x.Zoom).SubscribeSafe(zooms.Add, static _ => { });
            var anchor = scroller.TranslatePoint(new(scroller.Viewport.Width * AnchorX, scroller.Viewport.Height * AnchorY), window)!.Value;
            window.MouseWheel(anchor, new(0, 1), RawInputModifiers.Control);
            window.MouseWheel(anchor, new(0, 1), RawInputModifiers.Control);
            var landed = await UiWait.UntilAsync(() => Math.Abs(tab.Zoom - target) < ZoomTolerance);

            // Two instant notches would give the start and two steps; easing passes through more values on the way.
            await Assert.That(landed).IsTrue();
            await Assert.That(zooms.Count).IsGreaterThan(InstantZoomValues);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Waits until the first pages are rendered.</summary>
    /// <param name="test">The services.</param>
    /// <param name="window">The window.</param>
    /// <returns>The page canvas.</returns>
    private static async Task<PageCanvas> SettleAsync(TestServices test, Window window)
    {
        _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Cache.Count > ExpectedTiles && test.Services.RenderHub.Scheduler.QueueLength == 0);
        return window.GetVisualDescendants().OfType<PageCanvas>().Single();
    }

    /// <summary>Draws the next frame, lets rendering finish, and measures how much of the canvas changed.</summary>
    /// <param name="test">The services.</param>
    /// <param name="window">The window.</param>
    /// <param name="scroller">The canvas scroll viewer.</param>
    /// <param name="change">Scrolls or zooms.</param>
    /// <param name="name">The screenshot name.</param>
    /// <returns>The first frame's sharp edges as a share of the settled frame's.</returns>
    private static async Task<double> CompareWithSettledAsync(TestServices test, Window window, ScrollViewer scroller, Action change, string name)
    {
        // Holding the PDFium lock keeps tiles from arriving, as a slow page would, so the frame shows only what was cached.
        WriteableBitmap first;
        using (PdfiumLibrary.EnterScope())
        {
            change();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            first = window.CaptureRenderedFrame()!;
        }

        using var firstFrame = first;
        _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Scheduler.QueueLength == 0);

        // The last dequeued tile may still be rendering, so keep pumping until a pause brings no new tiles.
        using var pause = new PeriodicTimer(SettlePause);
        var count = -1;
        while (count != test.Services.RenderHub.Cache.Count && await pause.WaitForNextTickAsync())
        {
            count = test.Services.RenderHub.Cache.Count;
            _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Scheduler.QueueLength == 0);
        }

        using var settled = window.CaptureRenderedFrame()!;
        var settledEdges = CountEdges(settled, scroller, window);
        var sharpness = settledEdges > 0 ? (double)CountEdges(first, scroller, window) / settledEdges : 1;
        TestContext.Current?.Output.WriteLine($"{name}: the first frame had {sharpness:P1} of the settled frame's sharp edges");
        if (sharpness < MinSharpness)
        {
            Save(first, $"{name}-first.png");
            Save(settled, $"{name}-settled.png");
        }

        return sharpness;
    }

    /// <summary>Counts the sharp edges inside the scroll viewer: neighbouring pixels whose brightness jumps steeply.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="scroller">The scroll viewer.</param>
    /// <param name="window">The window.</param>
    /// <returns>The number of sharp edges; blurred text has far fewer.</returns>
    private static long CountEdges(WriteableBitmap frame, ScrollViewer scroller, Window window)
    {
        using var locked = frame.Lock();
        var origin = scroller.TranslatePoint(default, window)!.Value;
        var scale = locked.Size.Width / window.Bounds.Width;
        var left = (int)(origin.X * scale);
        var top = (int)(origin.Y * scale);
        var right = Math.Min(locked.Size.Width, (int)((origin.X + scroller.Viewport.Width) * scale));
        var bottom = Math.Min(locked.Size.Height, (int)((origin.Y + scroller.Viewport.Height) * scale));
        var edges = 0L;
        unsafe
        {
            for (var y = top; y < bottom; y++)
            {
                var row = new ReadOnlySpan<byte>((byte*)locked.Address + ((long)y * locked.RowBytes), locked.RowBytes);
                for (var x = left + 1; x < right; x++)
                {
                    if (Math.Abs(row[(x * BytesPerPixel) + Green] - row[((x - 1) * BytesPerPixel) + Green]) > EdgeContrast)
                    {
                        edges++;
                    }
                }
            }
        }

        return edges;
    }

    /// <summary>Saves a frame when screenshots are requested.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="name">The file name.</param>
    private static void Save(WriteableBitmap frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("PDFVIEWERLITE_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
