// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>Drives dialogs on a window with no display to check how their answers and their stacking follow from the streams they are made of.</summary>
[NotInParallel]
public sealed class OverlayStreamTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 900;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 10;

    /// <summary>The title of the first question.</summary>
    private const string First = "first";

    /// <summary>The title of the question the first one's answer asks.</summary>
    private const string Next = "next";

    /// <summary>The title of the second question.</summary>
    private const string Second = "second";

    /// <summary>The name of the settings file.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The titles in the order their dialogs are answered.</summary>
    private static readonly string[] Expected = [Second, First];

    /// <summary>Enter on a yes or no dialog backs out, and Tab then Enter presses the button that agrees.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EnterBacksOutUnlessTheKeyboardIsOnTheButtonThatAgrees()
    {
        var folder = NewFolder();
        var before = SynchronizationContext.Current;
        var answers = new List<bool>();
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            using var plain = session.Chrome.Confirm(Ask(First)).SubscribeSafe(answers.Add, static _ => { });
            _ = PumpUntil(session, () => Showing(session.Chrome, First));
            Press(window, ViewerKey.Enter);
            _ = PumpUntil(session, () => answers.Count == 1);
            using var tabbed = session.Chrome.Confirm(Ask(Second)).SubscribeSafe(answers.Add, static _ => { });
            _ = PumpUntil(session, () => Showing(session.Chrome, Second));
            Press(window, ViewerKey.Tab);
            Settle(session);
            Press(window, ViewerKey.Enter);
            _ = PumpUntil(session, () => answers.Count > 1);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(answers.Count).IsEqualTo(Expected.Length);
        await Assert.That(answers[0]).IsFalse();
        await Assert.That(answers[1]).IsTrue();
    }

    /// <summary>The answer of a dialog is given once, even when Escape is pressed again after it closed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogAnswersOnceWhenSentAway()
    {
        var folder = NewFolder();
        var before = SynchronizationContext.Current;
        var answers = new List<bool>();
        bool opened;
        bool closed;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            using var asking = session.Chrome.Confirm(Ask(First)).SubscribeSafe(answers.Add, static _ => { });
            opened = PumpUntil(session, () => Showing(session.Chrome, First));
            Press(window, ViewerKey.Escape);
            closed = PumpUntil(session, () => !Showing(session.Chrome, First));
            Press(window, ViewerKey.Escape);
            Settle(session);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(opened).IsTrue();
        await Assert.That(closed).IsTrue();
        await Assert.That(answers.Count).IsEqualTo(1);
        await Assert.That(answers[0]).IsFalse();
    }

    /// <summary>A dialog opened over another keeps it, and the first comes back when the second closes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StackedDialogsComeBackInOrder()
    {
        var folder = NewFolder();
        var before = SynchronizationContext.Current;
        var order = new List<string>();
        bool secondShown;
        bool firstBack;
        bool allClosed;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            using var first = chrome.Confirm(Ask(First)).SubscribeSafe(_ => order.Add(First), static _ => { });
            _ = PumpUntil(session, () => Showing(chrome, First));
            using var second = chrome.Confirm(Ask(Second)).SubscribeSafe(_ => order.Add(Second), static _ => { });
            secondShown = PumpUntil(session, () => Showing(chrome, Second));
            Press(window, ViewerKey.Escape);
            _ = PumpUntil(session, () => order.Count == 1);
            firstBack = PumpUntil(session, () => Showing(chrome, First) && !Showing(chrome, Second));
            Press(window, ViewerKey.Escape);
            allClosed = PumpUntil(session, () => !Showing(chrome, First) && order.Count == 2);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(secondShown).IsTrue();
        await Assert.That(firstBack).IsTrue();
        await Assert.That(allClosed).IsTrue();
        await Assert.That(order).IsEquivalentTo(Expected);
    }

    /// <summary>A dialog opened by the answer of another is shown, because the first closes before its answer is handed over.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogOpenedByAnAnswerStaysOpen()
    {
        var folder = NewFolder();
        var before = SynchronizationContext.Current;
        var chained = new List<bool>();
        bool nextShown;
        bool nextClosed;
        try
        {
            using var window = new HeadlessWindow(Width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            var chrome = session.Chrome;
            IDisposable? next = null;
            using var first = chrome.Confirm(Ask(First)).SubscribeSafe(_ => next = chrome.Confirm(Ask(Next)).SubscribeSafe(chained.Add, static _ => { }), static _ => { });
            _ = PumpUntil(session, () => Showing(chrome, First));
            Press(window, ViewerKey.Escape);
            Settle(session);
            nextShown = Showing(chrome, Next);
            Press(window, ViewerKey.Escape);
            nextClosed = PumpUntil(session, () => !Showing(chrome, Next) && chained.Count == 1);
            next?.Dispose();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(nextShown).IsTrue();
        await Assert.That(nextClosed).IsTrue();
        await Assert.That(chained[0]).IsFalse();
    }

    /// <summary>Tells whether a dialog with a title is on show.</summary>
    /// <param name="chrome">The window's controls.</param>
    /// <param name="title">The title.</param>
    /// <returns><see langword="true"/> when the dialog is on show.</returns>
    private static bool Showing(ViewerChrome chrome, string title) =>
        chrome.OverlayItems.Exists(item => item.Kind == ChromeItemKind.Heading && item.Text == title);

    /// <summary>Makes a folder for one run.</summary>
    /// <returns>The folder.</returns>
    private static string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-overlay-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Makes a yes or no question.</summary>
    /// <param name="title">The title, which tells the questions apart.</param>
    /// <returns>The question.</returns>
    private static ConfirmRequest Ask(string title) => new(title, "Go ahead?", "Yes");

    /// <summary>Presses a key in the window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="key">The key.</param>
    private static void Press(HeadlessWindow window, ViewerKey key) => window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)key, 0));

    /// <summary>Gives the loop a few turns, so anything that should happen has.</summary>
    /// <param name="session">The session.</param>
    private static void Settle(ViewerSession session)
    {
        const int turns = 10;
        for (var turn = 0; turn < turns; turn++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
        }
    }

    /// <summary>Runs the loop until a condition holds or the wait ends.</summary>
    /// <param name="session">The session.</param>
    /// <param name="done">The condition.</param>
    /// <returns><see langword="true"/> when the condition held.</returns>
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
