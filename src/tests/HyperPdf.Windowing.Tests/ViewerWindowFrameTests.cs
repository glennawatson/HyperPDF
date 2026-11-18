// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Icons;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks what the viewer does for a window the system gives no title bar: its own buttons, moving by the top bar and sizing by the edges.</summary>
[NotInParallel]
public sealed class ViewerWindowFrameTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1000;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>A point across the empty part of the top bar.</summary>
    private const int TitleX = 400;

    /// <summary>A point down the top bar.</summary>
    private const int TitleY = 18;

    /// <summary>How long one turn of the session waits for work, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>How many turns a change is given to reach the screen.</summary>
    private const int Turns = 20;

    /// <summary>A window with a system title bar gets no buttons of the viewer's.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SystemFrameGetsNoButtons()
    {
        var buttons = await WithWindow(false, static (session, window) =>
            session.Chrome.Bars.Count(static item => item.Icon is ChromeIcon.Minimize or ChromeIcon.Maximize or ChromeIcon.Restore));

        await Assert.That(buttons).IsEqualTo(0);
    }

    /// <summary>A window without a system title bar gets buttons that put it away and make it as big as the screen, and its top bar moves it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnFrameHasButtonsAndMovesByTheTopBar()
    {
        var outcome = await WithWindow(true, static (session, window) =>
        {
            var chrome = session.Chrome;
            var bars = chrome.Bars;
            var minimize = IndexOf(bars, ChromeIcon.Minimize);
            var maximize = IndexOf(bars, ChromeIcon.Maximize);
            _ = chrome.PressControl(false, minimize);
            _ = chrome.PressControl(false, maximize);
            Settle(session);
            var restore = IndexOf(chrome.Bars, ChromeIcon.Restore) >= 0;

            // Put it back, so the edges size it again.
            _ = chrome.PressControl(false, IndexOf(chrome.Bars, ChromeIcon.Restore));
            Settle(session);
            Press(window, TitleX, TitleY);
            Settle(session);
            Press(window, Width - 1, Height - 1);
            Settle(session);
            return new FrameOutcome(minimize >= 0 && maximize >= 0, window.Minimized, restore, window.Moves, window.ResizedEdges);
        });

        await Assert.That(outcome.HasButtons).IsTrue();
        await Assert.That(outcome.Minimized).IsTrue();
        await Assert.That(outcome.ShowsRestore).IsTrue();
        await Assert.That(outcome.Moves).IsEqualTo(1);
        await Assert.That(outcome.Edges).IsEqualTo(WindowEdges.Bottom | WindowEdges.Right);
    }

    /// <summary>Finds the place of the first control with an icon.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="icon">The icon.</param>
    /// <returns>The place, or -1.</returns>
    private static int IndexOf(IReadOnlyList<ChromeItem> items, ChromeIcon icon)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Icon == icon)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Presses and lets go of the left button at a point.</summary>
    /// <param name="window">The window.</param>
    /// <param name="x">The x of the point.</param>
    /// <param name="y">The y of the point.</param>
    private static void Press(HeadlessWindow window, int x, int y)
    {
        window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
    }

    /// <summary>Lets the session handle what is queued.</summary>
    /// <param name="session">The session.</param>
    private static void Settle(ViewerSession session)
    {
        for (var i = 0; i < Turns; i++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
        }
    }

    /// <summary>Opens a viewer with no document in a window and acts on it.</summary>
    /// <typeparam name="T">What the action finds.</typeparam>
    /// <param name="ownsFrame">Whether the window has no title bar of the system's.</param>
    /// <param name="act">The action.</param>
    /// <returns>What the action found.</returns>
    private static async Task<T> WithWindow<T>(bool ownsFrame, Func<ViewerSession, HeadlessWindow, T> act)
    {
        await Task.Yield();
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-frame-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, Height) { OwnsFrame = ownsFrame };
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            Settle(session);
            return act(session, window);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>What the viewer did for a window without a system title bar.</summary>
    /// <param name="HasButtons">Whether the buttons were laid out.</param>
    /// <param name="Minimized">Whether the window was put away.</param>
    /// <param name="ShowsRestore">Whether the button shows the window fills the screen.</param>
    /// <param name="Moves">How many moves were started.</param>
    /// <param name="Edges">The edges of the resize that was started.</param>
    private readonly record struct FrameOutcome(bool HasButtons, bool Minimized, bool ShowsRestore, int Moves, WindowEdges Edges);
}
