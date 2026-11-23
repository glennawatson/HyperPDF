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
using HyperPdf.Viewer.Icons;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>The search bar opens clear of the side panel, and menus carry icons, stay inside the window, and scroll.</summary>
[NotInParallel]
public sealed class ViewerFindAndMenuTests
{
    /// <summary>The standard window width in pixels.</summary>
    private const int Width = 900;

    /// <summary>The 1200-pixel window width where the find bar and side panel both show.</summary>
    private const int TwelveHundredWidth = 1200;

    /// <summary>The window height in pixels, too little for the whole main menu.</summary>
    private const int ShortHeight = 400;

    /// <summary>The 810-pixel window height checked for main-menu containment.</summary>
    private const int LaptopHeight = 810;

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

    /// <summary>The wheel notches scrolled down a tall menu.</summary>
    private const int ScrollSteps = 3;

    /// <summary>The text of the main menu button.</summary>
    private const string MenuName = "Menu";

    /// <summary>Running the find command, as Ctrl+F and the menu do, opens the search bar.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindCommandOpensTheSearchBar()
    {
        var opened = await WithTab(Width, ShortHeight, static (session, _, tab) =>
        {
            ((System.Windows.Input.ICommand)tab.FindCommand).Execute(null);
            Settle(session);
            return session.Chrome.HasOverlay && session.Chrome.OverlayItems.Exists(static item => item.Kind == ChromeItemKind.Field);
        });

        await Assert.That(opened).IsTrue();
    }

    /// <summary>With the keyboard on the last entry or with the mouse wheel, a menu taller than a small window scrolls cleanly inside it.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MenuShowsItsLastEntryInASmallWindow()
    {
        var outcome = await WithTab(Width, ShortHeight, static (session, window, _) =>
        {
            OpenMainMenu(session, window);
            var popup = session.Chrome.OverlayItems.First(static item => item.Kind == ChromeItemKind.Popup);
            var beforeRows = session.Chrome.OverlayItems.Where(static item => item.Kind == ChromeItemKind.Row).ToArray();
            var firstBefore = beforeRows.Length == 0 ? string.Empty : beforeRows[0].Text;

            var midX = (int)(popup.Bounds.X + (popup.Bounds.Width / Half));
            var midY = (int)(popup.Bounds.Y + (popup.Bounds.Height / Half));
            window.Send(new(WindowEventKind.PointerMoved, midX, midY, 0, 0));
            for (var step = 0; step < ScrollSteps; step++)
            {
                window.Send(new(WindowEventKind.Scrolled, 0, -WindowEvent.ScrollNotch, 0, 0));
            }

            Settle(session);
            var afterWheel = session.Chrome.OverlayItems.Where(static item => item.Kind == ChromeItemKind.Row).ToArray();
            var scrolledByWheel = afterWheel.Length > 0 && afterWheel[0].Text != firstBefore;

            window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Up, 0));
            Settle(session);
            var rows = session.Chrome.OverlayItems.Where(static item => item.Kind == ChromeItemKind.Row).ToArray();
            var last = rows.Length == 0 ? null : rows[^1];
            return new SmallMenuCheck(last is not null && last.Selected && last.Bounds.Bottom <= ShortHeight, scrolledByWheel);
        });

        await Assert.That(outcome.LastFitsInWindow).IsTrue();
        await Assert.That(outcome.ScrolledByWheel).IsTrue();
    }

    /// <summary>At 1200 pixels wide with the side panel open, the find bar stays inside the page area without overlapping the side panel.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindBar_WithSidePanelAtTwelveHundredWide_StaysClearOfSidePanelAndShowsSearchIcon()
    {
        var outcome = await WithTab(TwelveHundredWidth, LaptopHeight, static (session, _, tab) =>
        {
            tab.SidebarVisible = true;
            Settle(session);
            session.Chrome.ShowFind();
            Settle(session);
            var popup = session.Chrome.OverlayItems.First(static item => item.Kind == ChromeItemKind.Popup);
            var field = session.Chrome.OverlayItems.First(static item => item.Kind == ChromeItemKind.Field);
            return new FindPlacement(popup.Bounds.X >= session.Chrome.CanvasBounds.X, popup.Bounds.Right <= TwelveHundredWidth, field.Icon == ChromeIcon.Search);
        });

        await Assert.That(outcome.ClearsSidePanel).IsTrue();
        await Assert.That(outcome.FitsWindow).IsTrue();
        await Assert.That(outcome.HasSearchIcon).IsTrue();
    }

    /// <summary>In an 810-pixel window, every main-menu entry carries an icon and the menu stays below the toolbar and inside the window.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MainMenu_InEightHundredTenWindow_ShowsIconsAndStaysInsideWindow()
    {
        var outcome = await WithTab(TwelveHundredWidth, LaptopHeight, static (session, window, _) =>
        {
            var menuButton = session.Chrome.Bars.First(static item => item.Text == MenuName);
            OpenMainMenu(session, window);
            var popup = session.Chrome.OverlayItems.First(static item => item.Kind == ChromeItemKind.Popup);
            var rows = session.Chrome.OverlayItems.Where(static item => item.Kind == ChromeItemKind.Row).ToArray();
            var allHaveIcons = rows.Length > 0 && Array.TrueForAll(rows, static row => row.Icon != ChromeIcon.None);
            return new MenuCheck(allHaveIcons, popup.Bounds.Y >= menuButton.Bounds.Bottom, popup.Bounds.Bottom <= LaptopHeight);
        });

        await Assert.That(outcome.AllHaveIcons).IsTrue();
        await Assert.That(outcome.BelowToolbar).IsTrue();
        await Assert.That(outcome.FitsBottom).IsTrue();
    }

    /// <summary>Clicks the main menu button on the toolbar.</summary>
    /// <param name="session">The viewer session.</param>
    /// <param name="window">The headless window.</param>
    private static void OpenMainMenu(ViewerSession session, HeadlessWindow window)
    {
        var menu = session.Chrome.Bars.First(static item => item.Text == MenuName);
        var x = (int)(menu.Bounds.X + (menu.Bounds.Width / Half));
        var y = (int)(menu.Bounds.Y + (menu.Bounds.Height / Half));
        window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
        Settle(session);
    }

    /// <summary>Opens a document in a headless window of a size and runs something with the session, the window and the tab.</summary>
    /// <typeparam name="T">What the action gives back.</typeparam>
    /// <param name="width">The window width in pixels.</param>
    /// <param name="height">The window height in pixels.</param>
    /// <param name="act">The action.</param>
    /// <returns>What the action gave.</returns>
    private static async Task<T> WithTab<T>(int width, int height, Func<ViewerSession, HeadlessWindow, DocumentTabViewModel, T> act)
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-findmenu-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "doc.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateTextFilled(Pages, Lines));
        var before = SynchronizationContext.Current;
        try
        {
            using var window = new HeadlessWindow(width, height);
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

    /// <summary>Records where the find bar landed relative to the side panel and window.</summary>
    /// <param name="ClearsSidePanel">Whether the find bar starts at or to the right of the page canvas.</param>
    /// <param name="FitsWindow">Whether the find bar stays inside the window width.</param>
    /// <param name="HasSearchIcon">Whether the search input carries the magnifying-glass icon.</param>
    private readonly record struct FindPlacement(bool ClearsSidePanel, bool FitsWindow, bool HasSearchIcon);

    /// <summary>Records keyboard and wheel scrolling inside a short window.</summary>
    /// <param name="LastFitsInWindow">Whether the last menu row scrolled into view inside the short window.</param>
    /// <param name="ScrolledByWheel">Whether the mouse wheel scrolled the menu rows.</param>
    private readonly record struct SmallMenuCheck(bool LastFitsInWindow, bool ScrolledByWheel);

    /// <summary>Records the main menu layout checks on an 810-pixel window.</summary>
    /// <param name="AllHaveIcons">Whether every visible menu row carries an icon.</param>
    /// <param name="BelowToolbar">Whether the menu popup sits at or below the Menu button.</param>
    /// <param name="FitsBottom">Whether the menu popup fits inside the 810-pixel window height.</param>
    private readonly record struct MenuCheck(bool AllHaveIcons, bool BelowToolbar, bool FitsBottom);
}
