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

/// <summary>The search bar opens when the search is opened, whatever opened it, and a menu taller than the window still reaches its last entry.</summary>
[NotInParallel]
public sealed class ViewerFindAndMenuTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 900;

    /// <summary>The window height in pixels, too little for the whole main menu.</summary>
    private const int ShortHeight = 400;

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

    /// <summary>The turns that let the window settle.</summary>
    private const int SettleTurns = 20;

    /// <summary>Running the find command, as Ctrl+F and the menu do, opens the search bar.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindCommandOpensTheSearchBar()
    {
        var opened = await WithTab(ShortHeight, static (session, _, tab) =>
        {
            ((System.Windows.Input.ICommand)tab.FindCommand).Execute(null);
            Settle(session);
            return session.Chrome.HasOverlay && session.Chrome.OverlayItems.Exists(static item => item.Kind == ChromeItemKind.Field);
        });

        await Assert.That(opened).IsTrue();
    }

    /// <summary>With the keyboard on the last entry, the menu shows that entry wholly inside the window.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MenuShowsItsLastEntryInASmallWindow()
    {
        var inside = await WithTab(ShortHeight, static (session, window, _) =>
        {
            var menu = session.Chrome.Bars.First(static item => item.Text == "Menu");
            var x = (int)(menu.Bounds.X + (menu.Bounds.Width / Half));
            var y = (int)(menu.Bounds.Y + (menu.Bounds.Height / Half));
            window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
            window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
            window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
            Settle(session);

            // Up from no chosen entry goes to the last one.
            window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Up, 0));
            Settle(session);
            var rows = session.Chrome.OverlayItems.Where(static item => item.Kind == ChromeItemKind.Row).ToArray();
            var last = rows.Length == 0 ? null : rows[^1];
            return last is not null && last.Selected && last.Bounds.Bottom <= ShortHeight;
        });

        await Assert.That(inside).IsTrue();
    }

    /// <summary>Opens a document in a headless window of a height and runs something with the session, the window and the tab.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="height">The window height in pixels.</param>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static async Task<T> WithTab<T>(int height, Func<ViewerSession, HeadlessWindow, DocumentTabViewModel, T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-findmenu-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "doc.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(Width, height);
            var context = ViewerSession.UseWindowThread(window);
            using var services = new AppServices(new SettingsStore(Path.Combine(folder, "settings.json")), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
            using var session = new ViewerSession(window, services, context);
            session.Open([path]);
            var started = Stopwatch.GetTimestamp();
            while (session.Main.SelectedTab is not { IsLoaded: true } && Stopwatch.GetElapsedTime(started).TotalSeconds < TimeoutSeconds)
            {
                _ = session.PumpOnce(PumpMilliseconds);
            }

            Settle(session);
            return act(session, window, session.Main.SelectedTab!);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }
    }

    /// <summary>Lets the window handle what is waiting and lay out again.</summary>
    /// <param name="session">The session.</param>
    private static void Settle(ViewerSession session)
    {
        for (var i = 0; i < SettleTurns; i++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
        }
    }
}
