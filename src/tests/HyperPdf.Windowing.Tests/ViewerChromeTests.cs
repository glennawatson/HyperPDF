// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Engine.Tests;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Services;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Windowing.Tests;

/// <summary>Drives the window's own controls on a window with no display: the tab strip, the toolbar, the menu, the file dialog and the clipboard.</summary>
[NotInParallel]
public sealed class ViewerChromeTests
{
    /// <summary>The window width in pixels.</summary>
    private const int Width = 1100;

    /// <summary>The window height in pixels.</summary>
    private const int Height = 700;

    /// <summary>The pages in the test documents.</summary>
    private const int Pages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 20;

    /// <summary>The longest one wait for the window, in milliseconds.</summary>
    private const int PumpMilliseconds = 20;

    /// <summary>The longest the test waits for one step, in seconds.</summary>
    private const double TimeoutSeconds = 30;

    /// <summary>The turns of the loop given to a dialog that should not come back.</summary>
    private const int SettleTurns = 10;

    /// <summary>The password of the locked test document.</summary>
    private const string Password = "open-sesame";

    /// <summary>What the typing box shows for that password: one dot for each character.</summary>
    private const string MaskedPassword = "•••••••••••";

    /// <summary>The name of the settings file each run keeps in its own folder.</summary>
    private const string SettingsFile = "settings.json";

    /// <summary>The divisor that finds the middle of a control.</summary>
    private const double Half = 2;

    /// <summary>The tab strip, toolbar and status bar are drawn, and their controls change the page, open menus and files, and copy text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ControlsDriveTheViewer()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-chrome-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        ChromeOutcome outcome;
        try
        {
            outcome = RunViewer(folder);
        }
        finally
        {
            // The assertions await, so the window thread context must be gone before them or their continuations would wait for a pump.
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.TabText).IsEqualTo(outcome.TabTitle);
        await Assert.That(outcome.TabText).IsNotEmpty();
        await Assert.That(outcome.BarTextPixels).IsGreaterThan(0);
        await Assert.That(outcome.ZoomRole).IsEqualTo(AccessibleRole.Button);
        await Assert.That(outcome.ZoomedByScreenReader).IsTrue();
        await Assert.That(outcome.PageAfterNext).IsEqualTo(1);
        await Assert.That(outcome.MenuRows).IsGreaterThan(0);
        await Assert.That(outcome.MenuClosedByEscape).IsTrue();
        await Assert.That(outcome.FileDialogOpened).IsTrue();
        await Assert.That(outcome.TabsAfterOpen).IsEqualTo(Pages - 1);
        await Assert.That(outcome.CopiedLength).IsGreaterThan(0);
        await Assert.That(outcome.TabsAfterClose).IsEqualTo(1);
    }

    /// <summary>The bookmarks panel lists a document's bookmarks beside the pages, a press on one chooses it, and the button hides the panel again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BookmarksPanelListsAndChoosesBookmarks()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-bookmarks-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        BookmarksOutcome outcome;
        try
        {
            outcome = RunBookmarks(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.Shown).IsTrue();
        await Assert.That(outcome.Rows).IsEqualTo(Pages - 1);
        await Assert.That(outcome.Chosen).IsEqualTo("Plain");
        await Assert.That(outcome.Hidden).IsTrue();
    }

    /// <summary>A locked document asks for its password in a dialog that never shows the password, opens with the right one, and is not asked again once declined.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LockedDocumentAsksForItsPassword()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-locked-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        LockedOutcome outcome;
        try
        {
            outcome = RunLocked(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.AskedFirst).IsTrue();
        await Assert.That(outcome.DeclinedStaysClosed).IsTrue();
        await Assert.That(outcome.AskedAgainOnReturn).IsTrue();
        await Assert.That(outcome.ShownWhileTyping).IsEqualTo(MaskedPassword);
        await Assert.That(outcome.Opened).IsTrue();
    }

    /// <summary>Opens a locked document, declines the password once, reloads it and then types the password.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What the password dialog did.</returns>
    private static LockedOutcome RunLocked(string folder)
    {
        var path = Path.Combine(folder, "locked.pdf");
        File.WriteAllBytes(path, UserPasswordPdf.Create(Password, "owner"));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([path]);
        var asked = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Field));

        // Escape turns the question down, and the dialog must not come straight back.
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Escape, 0));
        _ = PumpUntil(session, () => !chrome.HasOverlay);
        for (var turn = 0; turn < SettleTurns; turn++)
        {
            _ = session.PumpOnce(PumpMilliseconds);
        }

        var staysClosed = !chrome.HasOverlay;

        // Leaving the tab and coming back asks again, as the user has shown they still want the document.
        session.Main.SelectedTab = null;
        _ = session.PumpOnce(PumpMilliseconds);
        session.Main.SelectedTab = session.Main.Tabs[0];
        var askedAgain = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Field));
        foreach (var character in Password)
        {
            window.Send(new(WindowEventKind.TextInput, 0, 0, character, 0));
        }

        _ = PumpUntil(session, () => chrome.OverlayItems.FirstOrDefault(static item => item.Kind == ChromeItemKind.Field)?.Text.Length == Password.Length);
        var shown = chrome.OverlayItems.FirstOrDefault(static item => item.Kind == ChromeItemKind.Field)?.Text ?? string.Empty;
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Enter, 0));
        var opened = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true, NeedsPassword: false });
        return new(asked, staysClosed, askedAgain, shown, opened);
    }

    /// <summary>Opens a document with two bookmarks and uses the bookmarks panel.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What the panel did.</returns>
    private static BookmarksOutcome RunBookmarks(string folder)
    {
        var path = Path.Combine(folder, "bookmarks.pdf");
        File.WriteAllBytes(path, OpenHintSamples.CreateStyledOutline());
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true, Outline.Count: > 0 });
        if (session.Main.SelectedTab is { SidebarVisible: false })
        {
            Click(window, Find(chrome.Bars, ChromeCommand.Sidebar));
        }

        var shown = PumpUntil(session, () => chrome.CanvasBounds.X > 0);
        var rows = chrome.Bars.Count(static item => item.Command == ChromeCommand.PickRow);
        Click(window, chrome.Bars.LastOrDefault(static item => item.Command == ChromeCommand.PickRow));
        _ = PumpUntil(session, () => session.Main.SelectedTab?.SelectedOutlineItem is not null);
        var chosen = session.Main.SelectedTab?.SelectedOutlineItem?.Title ?? string.Empty;
        Click(window, Find(chrome.Bars, ChromeCommand.Sidebar));
        var hidden = PumpUntil(session, () => chrome.CanvasBounds.X <= 0);
        return new(shown, rows, chosen, hidden);
    }

    /// <summary>Runs the viewer through its controls.</summary>
    /// <param name="folder">A folder for the settings and the documents.</param>
    /// <returns>What the controls did.</returns>
    private static ChromeOutcome RunViewer(string folder)
    {
        var first = Path.Combine(folder, "first.pdf");
        var second = Path.Combine(folder, "second.pdf");
        File.WriteAllBytes(first, TestPdf.CreateTextFilled(Pages, Lines));
        File.WriteAllBytes(second, TestPdf.CreateTextFilled(Pages, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([first]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true } && window.FramesPresented > 0);
        var tabText = Find(chrome.Bars, ChromeCommand.SelectTab)?.Text ?? string.Empty;
        var tabTitle = session.Main.SelectedTab?.Title ?? string.Empty;
        var barText = CountBarText(window, (int)chrome.CanvasBounds.Y);

        var zoomRole = PressZoomThroughTree(session, out var zoomed);

        Click(window, Find(chrome.Bars, ChromeCommand.NextPage));
        _ = PumpUntil(session, () => session.Main.SelectedTab is { CurrentPageIndex: > 0 });
        var page = session.Main.SelectedTab?.CurrentPageIndex ?? -1;

        Click(window, Find(chrome.Bars, ChromeCommand.Menu));
        _ = PumpUntil(session, () => chrome.HasOverlay);
        var menuRows = chrome.OverlayItems.Count(static item => item.Kind == ChromeItemKind.Row);
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Escape, 0));
        var menuClosed = PumpUntil(session, () => !chrome.HasOverlay);

        var dialogOpened = OpenThroughDialog(window, session, second);
        var tabsAfterOpen = session.Main.Tabs.Count;

        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.A, (uint)ViewerModifiers.Control));
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.C, (uint)ViewerModifiers.Control));
        _ = PumpUntil(session, () => window.ClipboardText is not null);

        Click(window, chrome.Bars.LastOrDefault(static item => item.Command == ChromeCommand.CloseTab));
        _ = PumpUntil(session, () => session.Main.Tabs.Count == 1);
        var copied = window.ClipboardText?.Length ?? 0;
        return new(tabText, tabTitle, barText, zoomRole, zoomed, page, menuRows, menuClosed, dialogOpened, tabsAfterOpen, copied, session.Main.Tabs.Count);
    }

    /// <summary>Finds the zoom in button the way a screen reader does and presses it through the accessibility tree.</summary>
    /// <param name="session">The session.</param>
    /// <param name="zoomed">Receives whether the pages got larger.</param>
    /// <returns>The role the tree gives the button.</returns>
    private static AccessibleRole PressZoomThroughTree(ViewerSession session, out bool zoomed)
    {
        // The first question from a screen reader makes the viewer build its tree, which the next pump does.
        _ = session.Accessibility.Get(session.Accessibility.RootId);
        var zoomId = ViewerNodes.FirstControlId + IndexOf(session.Chrome.Bars, ChromeCommand.ZoomIn);
        _ = PumpUntil(session, () => session.Accessibility.Get(zoomId) is not null);
        var before = session.Main.SelectedTab?.Zoom ?? 0;
        zoomed = session.Accessibility.DoAction(zoomId, 0) && PumpUntil(session, () => session.Main.SelectedTab is { } shown && shown.Zoom > before);
        return session.Accessibility.Get(zoomId)?.Role ?? AccessibleRole.Unknown;
    }

    /// <summary>Opens the file dialog with Control and O, types a path into it and presses Enter.</summary>
    /// <param name="window">The window.</param>
    /// <param name="session">The session.</param>
    /// <param name="path">The file to open.</param>
    /// <returns><see langword="true"/> when the dialog opened.</returns>
    private static bool OpenThroughDialog(HeadlessWindow window, ViewerSession session, string path)
    {
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.O, (uint)ViewerModifiers.Control));
        var opened = PumpUntil(session, () => session.Chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Field));
        foreach (var rune in path.EnumerateRunes())
        {
            window.Send(new(WindowEventKind.TextInput, 0, 0, (uint)rune.Value, 0));
        }

        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Enter, 0));
        _ = PumpUntil(session, () => session.Main.Tabs.Count > 1 && session.Main.SelectedTab is { IsLoaded: true, FileName: "second.pdf" });
        return opened;
    }

    /// <summary>Finds the first control that does something.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="command">What the control does.</param>
    /// <returns>The control, or <see langword="null"/>.</returns>
    private static ChromeItem? Find(IReadOnlyList<ChromeItem> items, ChromeCommand command) => items.FirstOrDefault(item => item.Command == command);

    /// <summary>Finds the place of the first control that does something.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="command">What the control does.</param>
    /// <returns>The place, or -1.</returns>
    private static int IndexOf(IReadOnlyList<ChromeItem> items, ChromeCommand command)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Command == command)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Moves the pointer to the middle of a control and presses the left button there.</summary>
    /// <param name="window">The window.</param>
    /// <param name="item">The control, or <see langword="null"/> to do nothing.</param>
    private static void Click(HeadlessWindow window, ChromeItem? item)
    {
        if (item is null)
        {
            return;
        }

        var x = (int)(item.Bounds.X + (item.Bounds.Width / Half));
        var y = (int)(item.Bounds.Y + (item.Bounds.Height / Half));
        window.Send(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        window.Send(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
        window.Send(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
    }

    /// <summary>Counts the pixels of the bars above the pages that are not the colour of the bar's own corner, which is the text and the marks on them.</summary>
    /// <param name="window">The window.</param>
    /// <param name="rows">The rows the tab strip and toolbar take.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountBarText(HeadlessWindow window, int rows)
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels);
        if (pixels.IsEmpty)
        {
            return 0;
        }

        // The far right of the tab strip has no tab on it, so it shows the plain bar.
        var bar = pixels[Width - 1];
        var count = 0;
        foreach (var pixel in pixels[..(Width * rows)])
        {
            if (pixel != bar)
            {
                count++;
            }
        }

        return count;
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

    /// <summary>What the password dialog did.</summary>
    /// <param name="AskedFirst">Whether opening the locked document showed the dialog.</param>
    /// <param name="DeclinedStaysClosed">Whether the dialog stayed away after Escape.</param>
    /// <param name="AskedAgainOnReturn">Whether choosing the tab again showed the dialog again.</param>
    /// <param name="ShownWhileTyping">The text in the typing box after the password was typed.</param>
    /// <param name="Opened">Whether the document opened with the password.</param>
    private readonly record struct LockedOutcome(bool AskedFirst, bool DeclinedStaysClosed, bool AskedAgainOnReturn, string ShownWhileTyping, bool Opened);

    /// <summary>What the bookmarks panel did.</summary>
    /// <param name="Shown">Whether the panel took room beside the pages.</param>
    /// <param name="Rows">The bookmarks listed.</param>
    /// <param name="Chosen">The title of the bookmark chosen by a press on its row.</param>
    /// <param name="Hidden">Whether the button hid the panel again.</param>
    private readonly record struct BookmarksOutcome(bool Shown, int Rows, string Chosen, bool Hidden);

    /// <summary>What the controls did.</summary>
    /// <param name="TabText">The text of the first tab.</param>
    /// <param name="TabTitle">The title of the document in the first tab.</param>
    /// <param name="BarTextPixels">The pixels of text and marks drawn on the tab strip and toolbar.</param>
    /// <param name="ZoomRole">The role a screen reader finds for the zoom in button.</param>
    /// <param name="ZoomedByScreenReader">Whether pressing the zoom in button through the accessibility tree made the pages larger.</param>
    /// <param name="PageAfterNext">The page shown after the next page button was pressed.</param>
    /// <param name="MenuRows">The rows of the main menu.</param>
    /// <param name="MenuClosedByEscape">Whether Escape closed the menu.</param>
    /// <param name="FileDialogOpened">Whether Control and O opened the file dialog.</param>
    /// <param name="TabsAfterOpen">The tabs open after a path was typed into the file dialog.</param>
    /// <param name="CopiedLength">The length of the text Control and C put on the clipboard.</param>
    /// <param name="TabsAfterClose">The tabs open after a tab's close button was pressed.</param>
    private readonly record struct ChromeOutcome(
        string TabText,
        string TabTitle,
        int BarTextPixels,
        AccessibleRole ZoomRole,
        bool ZoomedByScreenReader,
        int PageAfterNext,
        int MenuRows,
        bool MenuClosedByEscape,
        bool FileDialogOpened,
        int TabsAfterOpen,
        int CopiedLength,
        int TabsAfterClose);
}
