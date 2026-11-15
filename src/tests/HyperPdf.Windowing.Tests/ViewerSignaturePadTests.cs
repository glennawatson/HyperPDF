// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Windowing.Tests;

/// <summary>A signature is drawn with the pointer on the pad beside the rows of the signature window, and the view model gets the stroke.</summary>
[NotInParallel]
public sealed class ViewerSignaturePadTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The steps the pointer takes across the pad.</summary>
    private const int Steps = 6;

    /// <summary>The pixels the pointer moves at each step.</summary>
    private const int StepPixels = 10;

    /// <summary>The points in the first stroke of the remembered drawing.</summary>
    private const int FirstStroke = 3;

    /// <summary>The points in the second stroke of the remembered drawing.</summary>
    private const int SecondStroke = 2;

    /// <summary>The divisor that alternates the height of the points.</summary>
    private const int Parity = 2;

    /// <summary>The strokes of the remembered drawing.</summary>
    private const int StrokeCount = 2;

    /// <summary>The distance between the points of the remembered drawing, in its own units.</summary>
    private const int Spread = 25;

    /// <summary>The width of the pad a remembered drawing is fitted to.</summary>
    private const int PadWidth = 200;

    /// <summary>The height of the pad a remembered drawing is fitted to.</summary>
    private const int PadHeight = 100;

    /// <summary>The divisor that finds the middle of the pad.</summary>
    private const int Half = 2;

    /// <summary>A stroke made with the pointer reaches the view model as a drawn mark, and the pad can be wiped for a new start.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StrokeOnThePadReachesTheViewModel()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-pad-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        bool padShown;
        bool drawn;
        bool cleared;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            using var mark = new SignatureMarkViewModel(SignatureMarkKind.Signature, null, static _ => null);
            mark.Style = SignatureMarkStyle.Drawn;
            using var showing = chrome.ShowForm(ToolForms.SignatureMark(mark)).SubscribeSafe(static _ => { }, ErrorTrace.Write);
            padShown = PumpUntil(session, () => Pad(chrome) is not null);
            var pad = Pad(chrome);
            if (pad is not null)
            {
                var x = (int)pad.Bounds.X + StepPixels;
                var y = (int)(pad.Bounds.Y + (pad.Bounds.Height / Half));
                window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
                window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
                for (var step = 1; step <= Steps; step++)
                {
                    window.Send(new(WindowEventKind.PointerMoved, x + (step * StepPixels), y + (step * StepPixels), (uint)ViewerButtons.Left, 0));
                }

                window.Send(new(WindowEventKind.PointerUp, x + (Steps * StepPixels), y + (Steps * StepPixels), (uint)ViewerButtons.Left, 0));
            }

            drawn = PumpUntil(session, () => mark.Drawing is not null);
            Signal.Return(RxVoid.Default).InvokeCommand(mark.ClearDrawingCommand).Dispose();
            cleared = PumpUntil(session, () => mark.Drawing is null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(padShown).IsTrue();
        await Assert.That(drawn).IsTrue();
        await Assert.That(cleared).IsTrue();
    }

    /// <summary>A drawing the view model remembered becomes ink that lies inside the pad.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RememberedDrawingFillsThePad()
    {
        var points = Enumerable.Range(0, FirstStroke + SecondStroke).Select(static i => new PagePoint(i * Spread, (i % Parity) * Spread)).ToArray();
        var mark = SignatureMark.Drawn(SignatureMarkKind.Signature, points, [FirstStroke, SecondStroke]);
        var area = new LayoutRect(0, 0, PadWidth, PadHeight);
        var ink = PadInks.FromMark(mark, area);
        var inside = ink is not null && ink.Done.SelectMany(static stroke => stroke).All(static point => point.X >= 0 && point.X <= PadWidth && point.Y >= 0 && point.Y <= PadHeight);
        await Assert.That(ink).IsNotNull();
        await Assert.That(ink!.Done.Length).IsEqualTo(StrokeCount);
        await Assert.That(inside).IsTrue();
    }

    /// <summary>Finds the drawing pad of the open window: the picture beside its rows.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <returns>The pad, or <see langword="null"/> when none is shown.</returns>
    private static ChromeItem? Pad(ViewerChrome chrome) => chrome.OverlayItems.FirstOrDefault(static item => item.Kind == ChromeItemKind.Pad);

    /// <summary>Pumps the session until a condition holds or the time runs out.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when the condition held in time.</returns>
    private static bool PumpUntil(ViewerSession session, Func<bool> done)
    {
        var started = Stopwatch.GetTimestamp();
        while (!done())
        {
            if (Stopwatch.GetElapsedTime(started).TotalSeconds > TimeoutSeconds)
            {
                return false;
            }

            _ = session.PumpOnce(PumpMilliseconds);
        }

        return true;
    }
}
