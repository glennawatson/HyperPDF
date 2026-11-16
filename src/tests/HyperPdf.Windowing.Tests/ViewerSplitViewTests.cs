// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Windowing.Windows;
namespace HyperPdf.Windowing.Tests;

/// <summary>Splitting the view shows the document twice, side by side, and each view has the events that happen over it.</summary>
[NotInParallel]
public sealed class ViewerSplitViewTests
{
    /// <summary>The window width in device pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in device pixels.</summary>
    private const int Height = 700;

    /// <summary>The number of views a split shows.</summary>
    private const double Halves = 2;

    /// <summary>How far below the top of the view the pointer is put, in device pixels.</summary>
    private const int PointerOffset = 50;

    /// <summary>How many wheel notches the test turns.</summary>
    private const int Notches = 3;

    /// <summary>The pages of the document.</summary>
    private const int Pages = 4;

    /// <summary>The lines on each page.</summary>
    private const int Lines = 20;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest a step may take, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>How far the two views' widths may be from adding up to the page area, in device pixels.</summary>
    private const double WidthTolerance = 1;

    /// <summary>Splitting shows a second view in the right half, and putting it away gives the first view the whole area again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplittingGivesEachViewHalfTheArea()
    {
        var outcome = Run(static (window, session) =>
        {
            Run(session, split: true);
            var firstHalf = session.Canvas.Bounds;
            var secondHalf = session.Canvas.Neighbour?.Bounds ?? default;
            Run(session, split: false);
            return new SplitOutcome(firstHalf, secondHalf, session.Canvas.Bounds, session.Canvas.Neighbour is null);
        });

        await Assert.That(outcome.FirstHalf.Width).IsLessThan(outcome.Restored.Width);
        await Assert.That(outcome.FirstHalf.Width + outcome.SecondHalf.Width).IsBetween(outcome.Restored.Width - WidthTolerance, outcome.Restored.Width + WidthTolerance);
        await Assert.That(outcome.SecondHalf.X).IsEqualTo(outcome.FirstHalf.X + outcome.FirstHalf.Width);
        await Assert.That(outcome.PutAway).IsTrue();
    }

    /// <summary>A wheel turn goes to the view the pointer was last over, and leaves the other view where it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelGoesToTheViewUnderThePointer()
    {
        var outcome = Run(static (window, session) =>
        {
            Run(session, split: true);
            var second = session.Canvas.Neighbour!;
            var inSecond = (int)(second.Bounds.X + (second.Bounds.Width / Halves));
            var before = (session.Canvas.Offset.Y, second.Offset.Y);
            window.Send(new(WindowEventKind.PointerMoved, inSecond, (int)(second.Bounds.Y + PointerOffset), 0, 0));
            window.Send(new(WindowEventKind.Scrolled, 0, -WindowEvent.ScrollNotch * Notches, 0, 0));
            _ = session.PumpOnce(PumpMilliseconds);
            return new WheelOutcome(session.Canvas.Offset.Y - before.Item1, second.Offset.Y - before.Item2);
        });

        await Assert.That(outcome.First).IsEqualTo(0);
        await Assert.That(outcome.Second).IsGreaterThan(0);
    }

    /// <summary>Splits or joins the view, and pumps until the window shows it.</summary>
    /// <param name="session">The session.</param>
    /// <param name="split">Whether the view should be split.</param>
    private static void Run(ViewerSession session, bool split)
    {
        var tab = session.Main.SelectedTab!;
        if (tab.IsSplitView != split)
        {
            ((ICommand)tab.SplitViewCommand).Execute(null);
        }

        _ = PumpUntil(session, () => (session.Canvas.Neighbour is not null) == split);
    }

    /// <summary>Opens a document in a headless viewer and runs a scenario against it on the window thread.</summary>
    /// <typeparam name="T">What the scenario reports.</typeparam>
    /// <param name="scenario">The scenario.</param>
    /// <returns>What the scenario reported.</returns>
    private static T Run<T>(Func<HeadlessWindow, ViewerSession, T> scenario)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-split-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        try
        {
            var path = Path.Combine(folder, "split.pdf");
            File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            return scenario(window, session);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

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

    /// <summary>The page area before, during and after a split.</summary>
    /// <param name="FirstHalf">The first view's area while split.</param>
    /// <param name="SecondHalf">The second view's area while split.</param>
    /// <param name="Restored">The page area after the view was put whole again.</param>
    /// <param name="PutAway">Whether the second view was gone.</param>
    private readonly record struct SplitOutcome(LayoutRect FirstHalf, LayoutRect SecondHalf, LayoutRect Restored, bool PutAway);

    /// <summary>How far each view scrolled.</summary>
    /// <param name="First">How far the first view scrolled.</param>
    /// <param name="Second">How far the second view scrolled.</param>
    private readonly record struct WheelOutcome(double First, double Second);
}
