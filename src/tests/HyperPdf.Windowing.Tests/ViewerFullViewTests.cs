// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>A presentation puts every bar away, and reading mode keeps only the bar that leaves it.</summary>
[NotInParallel]
public sealed class ViewerFullViewTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 800;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 5;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>Half, for the middle of a control.</summary>
    private const double Half = 2;

    /// <summary>While presenting, the window has no tab strip, toolbar or buttons at all, and they come back afterwards.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PresentingHidesEveryBar()
    {
        var outcome = await WithTab(static (session, _, tab) =>
        {
            var before = session.Chrome.Bars.Count;
            ((System.Windows.Input.ICommand)tab.PresentCommand).Execute(null);
            Wait(session, () => tab.IsPresenting);
            Turn(session);
            var during = session.Chrome.Bars.Count;
            ((System.Windows.Input.ICommand)tab.PresentCommand).Execute(null);
            Wait(session, () => !tab.IsPresenting);
            Turn(session);
            return new BarCounts(before, during, session.Chrome.Bars.Count);
        });

        await Assert.That(outcome.Before).IsGreaterThan(0);
        await Assert.That(outcome.During).IsEqualTo(0);
        await Assert.That(outcome.After).IsEqualTo(outcome.Before);
    }

    /// <summary>In reading mode only the bar with the way back is left, and pressing its button leaves reading mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadingModeKeepsOnlyTheWayBack()
    {
        var outcome = await WithTab(static (session, window, tab) =>
        {
            ((System.Windows.Input.ICommand)tab.ReadModeCommand).Execute(null);
            _ = PumpUntil(session, () => tab.IsReading);
            Turn(session);
            var buttons = session.Chrome.Bars.Where(static item => item.Kind == ChromeItemKind.Button).ToArray();
            var tabs = session.Chrome.Bars.Count(static item => item.Kind == ChromeItemKind.Tab);
            var leave = Array.Find(buttons, item => ReferenceEquals(item.Command, tab.ReadModeCommand));
            if (leave is null)
            {
                return new ReadOutcome(buttons.Length, tabs, false);
            }

            var x = (int)(leave.Bounds.X + (leave.Bounds.Width / Half));
            var y = (int)(leave.Bounds.Y + (leave.Bounds.Height / Half));
            window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
            window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
            window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
            return new ReadOutcome(buttons.Length, tabs, PumpUntil(session, () => !tab.IsReading));
        });

        await Assert.That(outcome.Buttons).IsEqualTo(1);
        await Assert.That(outcome.Tabs).IsEqualTo(0);
        await Assert.That(outcome.Left).IsTrue();
    }

    /// <summary>Opens a document in a headless window and runs something with the session and its tab.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static async Task<T> WithTab<T>(Func<ViewerSession, HeadlessWindow, DocumentTabViewModel, T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-fullview-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "view.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
            Turn(session);
            return act(session, window, session.Main.SelectedTab!);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Handles one turn of the session's events and posted work.</summary>
    /// <param name="session">The session.</param>
    private static void Turn(ViewerSession session) => _ = session.PumpOnce(PumpMilliseconds);

    /// <summary>Pumps the session until a condition holds, without reporting whether it did.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    private static void Wait(ViewerSession session, Func<bool> done) => _ = PumpUntil(session, done);

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

    /// <summary>How many controls the bars had before, during and after a presentation.</summary>
    /// <param name="Before">The controls before.</param>
    /// <param name="During">The controls while presenting.</param>
    /// <param name="After">The controls afterwards.</param>
    private readonly record struct BarCounts(int Before, int During, int After);

    /// <summary>What reading mode showed.</summary>
    /// <param name="Buttons">The buttons.</param>
    /// <param name="Tabs">The tabs.</param>
    /// <param name="Left">Whether pressing the leave button left reading mode.</param>
    private readonly record struct ReadOutcome(int Buttons, int Tabs, bool Left);
}
