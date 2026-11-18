// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Accessibility;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Settings;
using HyperPdf.Engine;
using HyperPdf.Engine.Tests;
using HyperPdf.Presentation.Input;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.FormFilling;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.Viewer;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Windowing.Windows;
using ReactiveUI.Primitives;

namespace HyperPdf.Windowing.Tests;

/// <summary>Drives the window's own controls on a window with no display: the tab strip, the toolbar, the menu, the file dialog and the clipboard.</summary>
[NotInParallel]
public sealed class ViewerChromeTests
{
    /// <summary>The name of the first sample document.</summary>
    private const string FirstFile = "first.pdf";

    /// <summary>The name of the toolbar button that shows or hides the side panel.</summary>
    private const string SidePanelName = "Side panel";

    /// <summary>The name of the second sample document.</summary>
    private const string SecondFile = "second.pdf";

    /// <summary>The PDF files in the folder the file dialog test lists.</summary>
    private const int TwoFiles = 2;

    /// <summary>The text typed into the sample form's field.</summary>
    private const string TypedName = "Ada";

    /// <summary>The height of the sample form's page in points.</summary>
    private const int PageHeight = 792;

    /// <summary>The step, in points, of the search down the page for the text field.</summary>
    private const int FieldStep = 8;

    /// <summary>A point across the page that is inside the sample form's text field.</summary>
    private const float FieldX = 100F;

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

    /// <summary>The side panel lists the pages as pictures that go to their page, and the annotate button shows a bar of marking tools.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SidePanelShowsPagesAndTheMarkingBarChoosesTools()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-panel-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        SidePanelOutcome outcome;
        try
        {
            outcome = RunSidePanel(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.Pictures).IsGreaterThan(1);
        await Assert.That(outcome.PageAfterPick).IsGreaterThan(0);
        await Assert.That(outcome.NoNotes).IsTrue();
        await Assert.That(outcome.Tools).IsEqualTo(ChromeMarkBar.Tools.Length);
        await Assert.That(outcome.Tool).IsEqualTo(AnnotationTool.Highlight);
        await Assert.That(outcome.BarGone).IsTrue();
    }

    /// <summary>The file dialog lists PDF files or every file, and opens every file that is ticked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileDialogOpensEveryTickedFile()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-files-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        FilesOutcome outcome;
        try
        {
            outcome = RunFileDialog(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.PdfRows).IsEqualTo(TwoFiles);
        await Assert.That(outcome.AllRows).IsGreaterThan(TwoFiles);
        await Assert.That(outcome.StayedOpen).IsTrue();
        await Assert.That(outcome.Picked).IsEqualTo(TwoFiles);
    }

    /// <summary>Starting to fill in a form's text field opens a typing box over the field on the page, and what is typed there is written to the field.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormTextFieldIsFilledInOnThePage()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-form-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        FormOutcome outcome;
        try
        {
            outcome = RunForm(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.Asked).IsTrue();
        await Assert.That(outcome.InPlace).IsTrue();
        await Assert.That(outcome.Closed).IsTrue();
        await Assert.That(outcome.Value).IsEqualTo(TypedName);
        await Assert.That(outcome.Changed).IsTrue();
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

    /// <summary>The print window shows a picture of the sheet that will print beside its settings, and Escape closes it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintWindowShowsAPictureOfTheSheet()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"hyperpdf-print-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var before = SynchronizationContext.Current;
        PrintOutcome outcome;
        try
        {
            outcome = RunPrint(folder);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(before);
            Directory.Delete(folder, true);
        }

        await Assert.That(outcome.Opened).IsTrue();
        await Assert.That(outcome.PictureShown).IsTrue();
        await Assert.That(outcome.PictureWidth).IsGreaterThan(0);
        await Assert.That(outcome.InkPixels).IsGreaterThan(0);
        await Assert.That(outcome.Closed).IsTrue();
    }

    /// <summary>Opens a document, opens its print window and waits for the picture of the first sheet.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What the print window showed.</returns>
    private static PrintOutcome RunPrint(string folder)
    {
        var path = Path.Combine(folder, "print.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
        if (session.Main.SelectedTab is not { } tab)
        {
            return default;
        }

        ((System.Windows.Input.ICommand)tab.Printing.PrintCommand).Execute(null);
        var opened = PumpUntil(session, () => chrome.HasOverlay);
        var shown = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Thumbnail));
        var picture = chrome.OverlayItems.FirstOrDefault(static item => item.Kind == ChromeItemKind.Thumbnail);
        session.Canvas.DrawFrame();
        var ink = picture is null ? 0 : CountInk(window, picture.Bounds);
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Escape, 0));
        var closed = PumpUntil(session, () => !chrome.HasOverlay);
        return new(opened, shown, (int)(picture?.Bounds.Width ?? 0), ink, closed);
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

    /// <summary>Opens a form, starts filling in its text field and types into the box that opens over it.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What the dialog did.</returns>
    private static FormOutcome RunForm(string folder)
    {
        var path = Path.Combine(folder, "form.pdf");
        File.WriteAllBytes(path, TestPdf.CreateForm());
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true, Forms.HasForm: true });
        PageSettling.Wait(session);
        if (session.Main.SelectedTab is not { } tab || FindTextField(tab.Forms) is not { } field)
        {
            return default;
        }

        _ = tab.Forms.Activate(field);
        var asked = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item.Kind == ChromeItemKind.Field));

        // The box lies on the page, over the field, and is not a dialog: nothing else is shown with it.
        var box = chrome.OverlayItems.FirstOrDefault(static item => item.Kind == ChromeItemKind.Field);
        var inPlace = chrome.OverlayItems.Count == 1 && box is not null && chrome.CanvasBounds.Contains((int)box.Bounds.X, (int)box.Bounds.Y);
        foreach (var character in TypedName)
        {
            window.Send(new(WindowEventKind.TextInput, 0, 0, character, 0));
        }

        _ = PumpUntil(session, () => chrome.OverlayItems.FirstOrDefault(static item => item.Kind == ChromeItemKind.Field)?.Text.Length == TypedName.Length);
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Enter, 0));
        var closed = PumpUntil(session, () => !chrome.HasOverlay && tab.Forms.Editing is null);
        return new(asked, inPlace, closed, FindTextField(tab.Forms)?.Value ?? string.Empty, tab.HasUnsavedChanges);
    }

    /// <summary>Finds the text field of the sample form by looking down the page.</summary>
    /// <param name="forms">The form.</param>
    /// <returns>The field, or <see langword="null"/>.</returns>
    private static FormField? FindTextField(FormsViewModel forms)
    {
        for (var y = 0; y < PageHeight; y += FieldStep)
        {
            if (forms.HitTest(0, new(FieldX, y)) is { Kind: FormFieldKind.Text } field)
            {
                return field;
            }
        }

        return null;
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
            Click(window, Find(chrome.Bars, SidePanelName));
        }

        var shown = PumpUntil(session, () => chrome.CanvasBounds.X > 0);
        Click(window, FindMode(chrome.Bars, SidebarMode.Outline));
        _ = PumpUntil(session, () => session.Main.SelectedTab is { SidebarMode: SidebarMode.Outline } && chrome.Bars.FirstOrDefault(static item => item.Kind == ChromeItemKind.Row) is not null);
        var rows = chrome.Bars.Count(static item => item.Kind == ChromeItemKind.Row);
        Click(window, chrome.Bars.LastOrDefault(static item => item.Kind == ChromeItemKind.Row));
        _ = PumpUntil(session, () => session.Main.SelectedTab?.SelectedOutlineItem is not null);
        var chosen = session.Main.SelectedTab?.SelectedOutlineItem?.Title ?? string.Empty;
        Click(window, Find(chrome.Bars, SidePanelName));
        var hidden = PumpUntil(session, () => chrome.CanvasBounds.X <= 0);
        return new(shown, rows, chosen, hidden);
    }

    /// <summary>Opens a document and uses the side panel's page pictures and the marking bar.</summary>
    /// <param name="folder">A folder for the settings and the document.</param>
    /// <returns>What the panel and the bar did.</returns>
    private static SidePanelOutcome RunSidePanel(string folder)
    {
        var path = Path.Combine(folder, "pages.pdf");
        File.WriteAllBytes(path, TestPdf.CreateTextFilled(Pages, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([path]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true });
        if (session.Main.SelectedTab is { SidebarVisible: false })
        {
            Click(window, Find(chrome.Bars, SidePanelName));
        }

        _ = PumpUntil(session, () => chrome.CanvasBounds.X > 0);
        Click(window, FindMode(chrome.Bars, SidebarMode.Thumbnails));
        _ = PumpUntil(session, () => chrome.Bars.Count(static item => item.Kind == ChromeItemKind.Thumbnail) > 1);
        var pictures = chrome.Bars.Count(static item => item.Kind == ChromeItemKind.Thumbnail);
        Click(window, chrome.Bars.LastOrDefault(static item => item.Kind == ChromeItemKind.Thumbnail));
        _ = PumpUntil(session, () => session.Main.SelectedTab is { CurrentPageIndex: > 0 });
        var page = session.Main.SelectedTab?.CurrentPageIndex ?? -1;

        Click(window, FindMode(chrome.Bars, SidebarMode.Annotations));
        _ = PumpUntil(session, () => session.Main.SelectedTab is { SidebarMode: SidebarMode.Annotations });
        _ = session.PumpOnce(PumpMilliseconds);
        var noNotes = chrome.Bars.FirstOrDefault(static item => item.Kind == ChromeItemKind.Row) is null;

        Click(window, Find(chrome.Bars, "Annotate"));
        _ = PumpUntil(session, () => chrome.Bars.FirstOrDefault(static item => item.Parameter is AnnotationTool) is not null);
        var tools = chrome.Bars.Count(static item => item.Parameter is AnnotationTool);
        Click(window, chrome.Bars.FirstOrDefault(static item => item is { Parameter: AnnotationTool.Highlight }));
        _ = PumpUntil(session, () => session.Main.SelectedTab is { Annotations.Tool: AnnotationTool.Highlight });
        var tool = session.Main.SelectedTab?.Annotations.Tool ?? AnnotationTool.Select;
        Click(window, Find(chrome.Bars, "Annotate"));
        var barGone = PumpUntil(session, () => chrome.Bars.FirstOrDefault(static item => item.Parameter is AnnotationTool) is null);
        return new(pictures, page, noNotes, tools, tool, barGone);
    }

    /// <summary>Runs the viewer through its controls.</summary>
    /// <param name="folder">A folder for the settings and the documents.</param>
    /// <returns>What the controls did.</returns>
    private static ChromeOutcome RunViewer(string folder)
    {
        var first = Path.Combine(folder, FirstFile);
        var second = Path.Combine(folder, SecondFile);
        File.WriteAllBytes(first, TestPdf.CreateTextFilled(Pages, Lines));
        File.WriteAllBytes(second, TestPdf.CreateTextFilled(Pages, Lines));
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        session.Open([first]);
        _ = PumpUntil(session, () => session.Main.SelectedTab is { IsLoaded: true } && window.FramesPresented > 0);

        // The document's title arrives with its metadata, which a slow machine can deliver after the first frame, and the strip follows on the next pass.
        _ = PumpUntil(session, () => chrome.Bars.FirstOrDefault(static item => item.Kind == ChromeItemKind.Tab)?.Text == session.Main.SelectedTab?.Title);
        var tabText =chrome.Bars.FirstOrDefault(static item => item.Kind == ChromeItemKind.Tab)?.Text ?? string.Empty;
        var tabTitle = session.Main.SelectedTab?.Title ?? string.Empty;
        var barText = CountBarText(window, (int)chrome.CanvasBounds.Y);

        var zoomRole = PressZoomThroughTree(session, out var zoomed);

        Click(window, Find(chrome.Bars, "Next"));
        _ = PumpUntil(session, () => session.Main.SelectedTab is { CurrentPageIndex: > 0 });
        var page = session.Main.SelectedTab?.CurrentPageIndex ?? -1;

        Click(window, Find(chrome.Bars, "Menu"));
        _ = PumpUntil(session, () => chrome.HasOverlay);
        var menuRows = chrome.OverlayItems.Count(static item => item.Kind == ChromeItemKind.Row);
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.Escape, 0));
        var menuClosed = PumpUntil(session, () => !chrome.HasOverlay);

        var dialogOpened = OpenThroughDialog(window, session, second);
        var tabsAfterOpen = session.Main.Tabs.Count;

        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.A, (uint)ViewerModifiers.Control));
        window.Send(new(WindowEventKind.KeyDown, 0, 0, (uint)ViewerKey.C, (uint)ViewerModifiers.Control));
        _ = PumpUntil(session, () => window.ClipboardText is not null);

        Click(window, chrome.Bars.LastOrDefault(static item => item.Text == "Close tab"));
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
        var zoomId = ViewerNodes.FirstControlId + IndexOf(session.Chrome.Bars, "Zoom in");
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
        _ = PumpUntil(session, () => session.Main.Tabs.Count > 1 && session.Main.SelectedTab is { IsLoaded: true, FileName: SecondFile });
        return opened;
    }

    /// <summary>Opens two files at once by ticking them in the file dialog, after listing every file type and going back to PDF files.</summary>
    /// <param name="folder">A folder for the settings and the documents.</param>
    /// <returns>What the dialog did.</returns>
    private static FilesOutcome RunFileDialog(string folder)
    {
        File.WriteAllBytes(Path.Combine(folder, FirstFile), TestPdf.CreateTextFilled(Pages, Lines));
        File.WriteAllBytes(Path.Combine(folder, SecondFile), TestPdf.CreateTextFilled(Pages, Lines));
        File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a document");
        using var window = new HeadlessWindow(Width, Height);
        var context = ViewerSession.UseWindowThread(window);
        using var services = new AppServices(new SettingsStore(Path.Combine(folder, SettingsFile)), new HyperPdfEngine(), new FallbackPlatform(), new ViewerRenderHead(window));
        using var session = new ViewerSession(window, services, context);
        var chrome = session.Chrome;
        IReadOnlyList<string>? chosen = null;
        using var picking = chrome.PickFiles(folder).Subscribe(paths => chosen = paths);
        _ = PumpUntil(session, () => chrome.OverlayItems.FirstOrDefault(static item => item.Action == OverlayAction.PickRow) is not null);
        var pdfRows = chrome.OverlayItems.Count(static item => item.Action == OverlayAction.PickRow);
        Click(window, chrome.OverlayItems.FirstOrDefault(static item => item.Action == OverlayAction.FileKinds));
        _ = PumpUntil(session, () => chrome.OverlayItems.Count(static item => item.Action == OverlayAction.PickRow) > pdfRows);
        var allRows = chrome.OverlayItems.Count(static item => item.Action == OverlayAction.PickRow);
        Click(window, chrome.OverlayItems.FirstOrDefault(static item => item.Action == OverlayAction.FileKinds));
        _ = PumpUntil(session, () => chrome.OverlayItems.Count(static item => item.Action == OverlayAction.PickRow) == pdfRows);

        Click(window, chrome.OverlayItems.FirstOrDefault(static item => item is { Action: OverlayAction.PickRow, Text: FirstFile }));
        _ = PumpUntil(session, () => chrome.OverlayItems.Any(static item => item is { Action: OverlayAction.PickRow, Selected: true }));
        Click(window, chrome.OverlayItems.FirstOrDefault(static item => item is { Action: OverlayAction.PickRow, Text: SecondFile }));
        _ = PumpUntil(session, () => chrome.OverlayItems.Count(static item => item is { Action: OverlayAction.PickRow, Selected: true }) > 1);
        var stayedOpen = chrome.HasOverlay;
        Click(window, chrome.OverlayItems.FirstOrDefault(static item => item.Action == OverlayAction.Accept));
        _ = PumpUntil(session, () => chosen is not null);
        return new(pdfRows, allRows, stayedOpen, chosen?.Count ?? 0);
    }

    /// <summary>Finds the first control that does something.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="text">The control's name.</param>
    /// <returns>The control, or <see langword="null"/>.</returns>
    private static ChromeItem? Find(IReadOnlyList<ChromeItem> items, string text) => items.FirstOrDefault(item => item.Text == text);

    /// <summary>Finds the side panel tab that shows one kind of list.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="mode">The list.</param>
    /// <returns>The tab, or <see langword="null"/>.</returns>
    private static ChromeItem? FindMode(IReadOnlyList<ChromeItem> items, SidebarMode mode) =>
        items.FirstOrDefault(item => item.Parameter is SidebarMode shown && shown == mode);

    /// <summary>Finds the place of the first control that does something.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="text">The control's name.</param>
    /// <returns>The place, or -1.</returns>
    private static int IndexOf(IReadOnlyList<ChromeItem> items, string text)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Text == text)
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

    /// <summary>Counts the pixels of a part of the last frame that differ from its top left pixel, which is how much was drawn there besides the background.</summary>
    /// <param name="window">The window.</param>
    /// <param name="bounds">The part of the frame, in device pixels.</param>
    /// <returns>The pixels that differ.</returns>
    private static int CountInk(HeadlessWindow window, in LayoutRect bounds)
    {
        var pixels = MemoryMarshal.Cast<byte, uint>(window.Cpu.FramePixels);
        var left = (int)bounds.X;
        var top = (int)bounds.Y;
        var right = Math.Min(Width, (int)bounds.Right);
        var bottom = Math.Min(pixels.Length / Width, (int)bounds.Bottom);
        if (pixels.IsEmpty || left >= right || top >= bottom)
        {
            return 0;
        }

        var paper = pixels[(top * Width) + left];
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            foreach (var pixel in pixels.Slice((y * Width) + left, right - left))
            {
                if (pixel != paper)
                {
                    count++;
                }
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

    /// <summary>What the file dialog did.</summary>
    /// <param name="PdfRows">The files listed at first, which are the PDF files.</param>
    /// <param name="AllRows">The files listed once every file type is shown.</param>
    /// <param name="StayedOpen">Whether the dialog stayed open while files were ticked.</param>
    /// <param name="Picked">The files the dialog answered with.</param>
    private readonly record struct FilesOutcome(int PdfRows, int AllRows, bool StayedOpen, int Picked);

    /// <summary>What filling in a form field did.</summary>
    /// <param name="Asked">Whether starting to fill in the field opened a typing box.</param>
    /// <param name="InPlace">Whether the typing box lay on the page and was shown alone, not inside a dialog.</param>
    /// <param name="Closed">Whether Enter closed the dialog and ended the edit.</param>
    /// <param name="Value">The field's value afterwards.</param>
    /// <param name="Changed">Whether the document has changes to save.</param>
    private readonly record struct FormOutcome(bool Asked, bool InPlace, bool Closed, string Value, bool Changed);

    /// <summary>What the print window showed.</summary>
    /// <param name="Opened">Whether the print window opened.</param>
    /// <param name="PictureShown">Whether the picture of a sheet was laid out.</param>
    /// <param name="PictureWidth">The width of the picture in device pixels.</param>
    /// <param name="InkPixels">The pixels inside the picture that differ from its first pixel, which is the page's own content.</param>
    /// <param name="Closed">Whether Escape closed the window.</param>
    private readonly record struct PrintOutcome(bool Opened, bool PictureShown, int PictureWidth, int InkPixels, bool Closed);

    /// <summary>What the side panel's page pictures and the marking bar did.</summary>
    /// <param name="Pictures">The page pictures listed.</param>
    /// <param name="PageAfterPick">The page shown after the last picture was pressed.</param>
    /// <param name="NoNotes">Whether the notes list of a document without notes has no rows.</param>
    /// <param name="Tools">The tools on the marking bar.</param>
    /// <param name="Tool">The tool in use after the highlight button was pressed.</param>
    /// <param name="BarGone">Whether the annotate button hid the bar again.</param>
    private readonly record struct SidePanelOutcome(int Pictures, int PageAfterPick, bool NoNotes, int Tools, AnnotationTool Tool, bool BarGone);

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
