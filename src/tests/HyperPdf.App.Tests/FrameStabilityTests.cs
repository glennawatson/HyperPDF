// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HyperPdf.App.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Settings;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

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

    /// <summary>The width of each marked page, in points.</summary>
    private const int MarkedPageWidth = 612;

    /// <summary>The margin and spacing of the vertical marks, in points.</summary>
    private const int MarkSpacing = 24;

    /// <summary>The catalog, page tree and shared content objects before the page objects.</summary>
    private const int SharedPdfObjects = 3;

    /// <summary>The object number of the first page in the marked PDF.</summary>
    private const int FirstMarkedPageObject = SharedPdfObjects + 1;

    /// <summary>The edges needed to show that the visible page text, rather than only its paper, has drawn.</summary>
    private const int ReadyEdges = 20;

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

    /// <summary>Verifies a tile arrival does not cancel other tiles requested for the same view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepaintingSameViewKeepsRenderGeneration()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("repaint.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var canvas = await SettleAsync(test, window);
            var scroller = canvas.FindAncestorOfType<ScrollViewer>()!;
            var client = main.SelectedTab!.CanvasClient;
            var generation = client.Generation;
            canvas.InvalidateVisual();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            using var repainted = window.CaptureRenderedFrame();
            await Assert.That(client.Generation).IsEqualTo(generation);

            scroller.Offset = new(scroller.Offset.X, scroller.Offset.Y + ScrollStep);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            using var scrolled = window.CaptureRenderedFrame();
            await Assert.That(client.Generation).IsGreaterThan(generation);
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Verifies frames drawn while scrolling are as sharp as the settled frames.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ScrollingDrawsSettledFrames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("scroll.pdf", CreateStripedPdf())]);
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
        Mark("settle canvas");
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<PageCanvas>().Any());
        var canvas = window.GetVisualDescendants().OfType<PageCanvas>().Single();
        Mark("settle frames");
        _ = await UiWait.UntilAsync(() =>
        {
            var scroller = canvas.FindAncestorOfType<ScrollViewer>();
            if (scroller is null || scroller.Viewport.Width <= 0 || scroller.Viewport.Height <= 0 || window.Bounds.Width <= 0)
            {
                return false;
            }

            Mark("settle capture");
            using var frame = window.CaptureRenderedFrame();
            Mark("settle edges");
            return frame is not null && CountEdges(frame, scroller, window) > ReadyEdges && test.Services.RenderHub.Scheduler.IsIdle;
        });
        Mark("settled");
        return canvas;
    }

    /// <summary>Fills every page with thin vertical marks so edge counts measure content across the full viewport.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateStripedPdf()
    {
        var content = new StringBuilder("0 0 0 rg\n");
        for (var x = MarkSpacing; x < MarkedPageWidth - MarkSpacing; x += MarkSpacing)
        {
            _ = content.Append(CultureInfo.InvariantCulture, $"{x} 0 2 792 re f\n");
        }

        var kids = new StringBuilder();
        for (var page = 0; page < Pages; page++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{page + FirstMarkedPageObject} 0 R ");
        }

        var objects = new string[Pages + SharedPdfObjects];
        objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Count {Pages} /Kids [{kids}] >>");
        objects[2] = MiniPdf.Stream(string.Empty, content.ToString());
        for (var page = 0; page < Pages; page++)
        {
            objects[page + SharedPdfObjects] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 3 0 R >>";
        }

        return MiniPdf.Build(objects);
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
        var tab = scroller.GetVisualDescendants().OfType<PageCanvas>().Single().Tab!;
        var client = tab.CanvasClient;
        var beforeGeneration = client.Generation;
        var beforeTiles = test.Services.RenderHub.Cache.Count;
        Mark($"{name} change");
        change();
        Mark($"{name} jobs1");
        Dispatcher.UIThread.RunJobs();
        Mark($"{name} tick");
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Mark($"{name} jobs2");
        Dispatcher.UIThread.RunJobs();
        Mark($"{name} capture1");
        using var first = window.CaptureRenderedFrame()!;
        var firstGeneration = client.Generation;
        var firstTiles = test.Services.RenderHub.Cache.Count;
        Mark($"{name} idle");
        _ = await UiWait.UntilAsync(() => test.Services.RenderHub.Scheduler.IsIdle);

        Mark($"{name} capture2");
        using var settled = window.CaptureRenderedFrame()!;
        Mark($"{name} edges");
        var settledEdges = CountEdges(settled, scroller, window);
        var firstEdges = CountEdges(first, scroller, window);
        var sharpness = settledEdges > 0 ? (double)firstEdges / settledEdges : 1;
        TestContext.Current?.Output.WriteLine(
            $"{name}: {sharpness:P1} sharp; page={tab.CurrentPageIndex}; offset={scroller.Offset.Y:F0} viewport={scroller.Viewport.Height:F0}; "
            + $"edges={firstEdges}/{settledEdges}; tiles={beforeTiles}/{firstTiles}/{test.Services.RenderHub.Cache.Count}; "
            + $"generations={beforeGeneration}/{firstGeneration}/{client.Generation}");
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

    /// <summary>Records the step a test reached, to find where a hung run stopped.</summary>
    /// <param name="step">The step.</param>
    private static void Mark(string step)
    {
        var directory = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var folder = Path.Combine(directory, "test-diagnostics");
        _ = Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "frame-progress.log"), $"{TimeProvider.System.GetUtcNow():O} {Environment.CurrentManagedThreadId} {step}\n");
    }

    /// <summary>Saves a frame when screenshots are requested.</summary>
    /// <param name="frame">The frame.</param>
    /// <param name="name">The file name.</param>
    private static void Save(WriteableBitmap frame, string name)
    {
        var directory = Environment.GetEnvironmentVariable("HYPERPDF_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        _ = Directory.CreateDirectory(directory);
        frame.Save(Path.Combine(directory, name), new PngBitmapEncoderOptions());
    }
}
