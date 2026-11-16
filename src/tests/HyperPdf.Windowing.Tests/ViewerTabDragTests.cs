// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>What the pointer does to the tabs: a tab let go over another takes its place, a middle click closes one, and a right click opens its menu.</summary>
[NotInParallel]
public sealed class ViewerTabDragTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 900;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 600;

    /// <summary>The pages in each test document.</summary>
    private const int Pages = 1;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 3;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>How far in from the tab's left edge the pointer rests, in pixels.</summary>
    private const int PreviewInset = 20;

    /// <summary>How far below the tab strip the pointer goes to leave, in pixels.</summary>
    private const int PreviewFar = 300;

    /// <summary>The rows of the area under a tab that are compared.</summary>
    private const int PreviewRows = 24;

    /// <summary>The columns of the area under a tab that are compared.</summary>
    private const int PreviewColumns = 120;

    /// <summary>The documents opened for the tab tests that need two.</summary>
    private const int TwoTabs = 2;

    /// <summary>The documents opened for the strip test, more than fit in the narrow window.</summary>
    private const int ManyTabs = 6;

    /// <summary>The width of the narrow window, in pixels.</summary>
    private const int NarrowWidth = 380;

    /// <summary>Half, for the middle of a control.</summary>
    private const double Half = 2;

    /// <summary>The name of the document opened first.</summary>
    private const string FirstName = "first.pdf";

    /// <summary>The name of the document opened second.</summary>
    private const string SecondName = "second.pdf";

    /// <summary>The names in the order the tabs have once the first has been let go over the second.</summary>
    private static readonly string[] Swapped = [SecondName, FirstName];

    /// <summary>The names of the tabs left once the first has been closed.</summary>
    private static readonly string[] OnlySecond = [SecondName];

    /// <summary>Pressing on the first tab and letting go over the second swaps them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabLetGoOverAnotherTakesItsPlace()
    {
        var order = await WithTwoTabs(static (session, window, tabs) =>
        {
            Drag(window, tabs[0], tabs[1]);
            _ = PumpUntil(session, () => session.Main.Tabs[0].FileName == SecondName);
            return session.Main.Tabs.Select(static tab => tab.FileName).ToArray();
        });

        await Assert.That(order).IsEquivalentTo(Swapped);
    }

    /// <summary>A middle click on a tab closes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MiddleClickClosesTheTab()
    {
        var order = await WithTwoTabs(static (session, window, tabs) =>
        {
            Press(window, tabs[0], ViewerButtons.Middle);
            _ = PumpUntil(session, () => session.Main.Tabs.Count == 1);
            return session.Main.Tabs.Select(static tab => tab.FileName).ToArray();
        });

        await Assert.That(order).IsEquivalentTo(OnlySecond);
    }

    /// <summary>A right click on a tab opens its menu.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RightClickOpensTheTabMenu()
    {
        var opened = await WithTwoTabs(static (session, window, tabs) =>
        {
            Press(window, tabs[1], ViewerButtons.Right);
            return PumpUntil(session, () => session.Chrome.HasOverlay);
        });

        await Assert.That(opened).IsTrue();
    }

    /// <summary>With more tabs than fit, the strip scrolls to keep the chosen tab in view, and the tabs left of it go out of view.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StripScrollsToTheChosenTab()
    {
        var outcome = await WithTabs(ManyTabs, NarrowWidth, static (session, _, tabs) =>
        {
            var last = session.Main.Tabs.Count - 1;
            session.Main.SelectedTab = session.Main.Tabs[last];
            Turn(session);
            var shown = session.Chrome.Bars.Where(static item => item.Kind == ChromeItemKind.Tab).ToArray();
            var chosen = Array.Find(shown, item => item.Index == last);
            return new StripOutcome(tabs.Length, shown.Length, chosen is not null && chosen.Bounds.X >= 0 && chosen.Bounds.Right <= NarrowWidth, Array.Exists(shown, static item => item.Index == 0));
        });

        await Assert.That(outcome.ShownBefore).IsLessThan(ManyTabs);
        await Assert.That(outcome.ChosenInView).IsTrue();
        await Assert.That(outcome.FirstStillShown).IsFalse();
    }

    /// <summary>A wheel notch down over the tab strip chooses the next tab, and one up chooses the one before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WheelOverTheStripChoosesNeighbouringTabs()
    {
        var chosen = await WithTwoTabs(static (session, window, tabs) =>
        {
            session.Main.SelectedTab = session.Main.Tabs[0];
            Turn(session);
            window.Send(new(WindowEventKind.PointerMoved, (int)tabs[0].Bounds.X + 1, (int)tabs[0].Bounds.Y + 1, 0, 0));
            Turn(session);
            window.Send(new(WindowEventKind.Scrolled, 0, -WindowEvent.ScrollNotch, 0, 0));
            Turn(session);
            var down = session.Main.Tabs.IndexOf(session.Main.SelectedTab!);
            window.Send(new(WindowEventKind.Scrolled, 0, WindowEvent.ScrollNotch, 0, 0));
            Turn(session);
            return (int[])[down, session.Main.Tabs.IndexOf(session.Main.SelectedTab!)];
        });

        await Assert.That(chosen[0]).IsEqualTo(1);
        await Assert.That(chosen[1]).IsEqualTo(0);
    }

    /// <summary>The pointer resting on a tab opens a preview under it, which draws over the pages, and moving away takes it back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HoverOpensAPreviewUnderTheTab()
    {
        var outcome = await WithTwoTabs(static (session, window, tabs) =>
        {
            var tab = tabs[1];
            var left = (int)tab.Bounds.X;
            var top = (int)tab.Bounds.Bottom;
            Turn(session);
            session.Canvas.DrawFrame();
            Turn(session);
            session.Canvas.DrawFrame();
            var before = Region(window, left, top);

            window.Send(new(WindowEventKind.PointerMoved, left + PreviewInset, (int)(tab.Bounds.Y + (tab.Bounds.Height / Half)), 0, 0));
            Turn(session);
            session.Canvas.DrawFrame();
            var during = Region(window, left, top);

            window.Send(new(WindowEventKind.PointerMoved, left, top + PreviewFar, 0, 0));
            Turn(session);
            session.Canvas.DrawFrame();
            var after = Region(window, left, top);

            // Other parts of the window may still be settling, so the preview going is shown by the area no longer being what it was with it open.
            return new PreviewOutcome(!before.AsSpan().SequenceEqual(during), !during.AsSpan().SequenceEqual(after));
        });

        await Assert.That(outcome.Opened).IsTrue();
        await Assert.That(outcome.Closed).IsTrue();
    }

    /// <summary>Copies the pixels of the area under a tab, where its preview opens.</summary>
    /// <param name="window">The window.</param>
    /// <param name="left">The left of the area.</param>
    /// <param name="top">The top of the area.</param>
    /// <returns>The pixels.</returns>
    private static uint[] Region(HeadlessWindow window, int left, int top)
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels);
        var result = new uint[PreviewRows * PreviewColumns];
        for (var row = 0; row < PreviewRows; row++)
        {
            pixels.Slice(((top + row) * Width) + left, PreviewColumns).CopyTo(result.AsSpan(row * PreviewColumns, PreviewColumns));
        }

        return result;
    }

    /// <summary>Opens two documents in a headless window and runs something with the session, the window and the two tabs' controls.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static Task<T> WithTwoTabs<T>(Func<ViewerSession, HeadlessWindow, ChromeItem[], T> act) => WithTabs(TwoTabs, Width, act);

    /// <summary>Opens documents in a headless window of a width and runs something with the session, the window and the controls of the tabs shown.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="count">How many documents to open.</param>
    /// <param name="width">The window width in pixels.</param>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static async Task<T> WithTabs<T>(int count, int width, Func<ViewerSession, HeadlessWindow, ChromeItem[], T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-tabs-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var paths = new string[count];
        for (var i = 0; i < count; i++)
        {
            paths[i] = Path.Combine(folder, i switch { 0 => FirstName, 1 => SecondName, _ => $"doc{i}.pdf" });
            await File.WriteAllBytesAsync(paths[i], TestPdf.CreateTextFilled(Pages, Lines));
        }

        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(width, Height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open(paths);
            _ = PumpUntil(session, () => session.Main.Tabs.Count == count && session.Main.SelectedTab is not null);
            Turn(session);
            var tabs = session.Chrome.Bars.Where(static item => item.Kind == ChromeItemKind.Tab).ToArray();
            return act(session, window, tabs);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Presses in the middle of one control and lets go in the middle of another.</summary>
    /// <param name="window">The window.</param>
    /// <param name="from">The control pressed.</param>
    /// <param name="to">The control the pointer is let go over.</param>
    private static void Drag(HeadlessWindow window, ChromeItem from, ChromeItem to)
    {
        var fromX = (int)(from.Bounds.X + (from.Bounds.Width / Half));
        var toX = (int)(to.Bounds.X + (to.Bounds.Width / Half));
        var y = (int)(from.Bounds.Y + (from.Bounds.Height / Half));
        window.Send(new(WindowEventKind.PointerMoved, fromX, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, fromX, y, (uint)ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerMoved, toX, y, 0, 0));
        window.Send(new(WindowEventKind.PointerUp, toX, y, (uint)ViewerButtons.Left, 0));
    }

    /// <summary>Presses a button in the middle of a control and lets go of it there.</summary>
    /// <param name="window">The window.</param>
    /// <param name="item">The control.</param>
    /// <param name="button">The button.</param>
    private static void Press(HeadlessWindow window, ChromeItem item, ViewerButtons button)
    {
        var x = (int)(item.Bounds.X + (item.Bounds.Width / Half));
        var y = (int)(item.Bounds.Y + (item.Bounds.Height / Half));
        window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, x, y, (uint)button, 0));
        window.Send(new(WindowEventKind.PointerUp, x, y, (uint)button, 0));
    }

    /// <summary>Handles one turn of the session's events and posted work.</summary>
    /// <param name="session">The session.</param>
    private static void Turn(ViewerSession session) => _ = session.PumpOnce(PumpMilliseconds);

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

    /// <summary>What happened under a tab as the pointer came and went.</summary>
    /// <param name="Opened">Whether the area under the tab changed when the pointer rested on it.</param>
    /// <param name="Closed">Whether the area was no longer as it was with the preview open once the pointer had gone.</param>
    private readonly record struct PreviewOutcome(bool Opened, bool Closed);

    /// <summary>What the tab strip showed in the narrow window.</summary>
    /// <param name="Total">How many tabs the strip showed with the first one chosen.</param>
    /// <param name="ShownBefore">How many tabs the strip showed after the last was chosen.</param>
    /// <param name="ChosenInView">Whether the chosen tab lay wholly inside the window.</param>
    /// <param name="FirstStillShown">Whether the first tab was still in the strip.</param>
    private readonly record struct StripOutcome(int Total, int ShownBefore, bool ChosenInView, bool FirstStillShown);
}
