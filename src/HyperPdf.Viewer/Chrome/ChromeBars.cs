// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Layout;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Help;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Lays out the bars that are always in the window: the tab strip, the toolbar and the status bar.</summary>
internal static class ChromeBars
{
    /// <summary>The mark after the name of a tab with changes that are not saved.</summary>
    private const string UnsavedMark = " •";

    /// <summary>A tab is never narrower than this share of its widest size, so many tabs scroll instead of shrinking to nothing.</summary>
    private const int NarrowestShare = 3;

    /// <summary>The button of the reading mode bar takes at most this share of the width, so a narrow window keeps room for the name.</summary>
    private const int ReadBarButtonShare = 2;

    /// <summary>The edges of a bar's contents: one on each side.</summary>
    private const int ReadBarEdges = 2;

    /// <summary>The width of the status bar's dismiss button, in lines of the bar's height.</summary>
    private const int DismissWidthInLines = 4;

    /// <summary>Makes a number that changes whenever what the bars show changes, so they are only laid out again when needed.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="size">The window size in device pixels.</param>
    /// <param name="scale">The display scale.</param>
    /// <param name="sidebarVersion">The version of the side panel's scroll position and open bookmarks.</param>
    /// <returns>The number.</returns>
    internal static int Stamp(MainViewModel main, SurfaceSize size, double scale, int sidebarVersion)
    {
        var hash = default(HashCode);
        hash.Add(size.Width);
        hash.Add(size.Height);
        hash.Add(scale);
        hash.Add(sidebarVersion);
        var tabs = main.Tabs;
        hash.Add(tabs.Count);
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            hash.Add(RuntimeHelpers.GetHashCode(tab));
            hash.Add(RuntimeHelpers.GetHashCode(tab.Title));
            hash.Add(tab.HasUnsavedChanges);
        }

        if (main.SelectedTab is { } selected)
        {
            hash.Add(RuntimeHelpers.GetHashCode(selected));
            hash.Add(selected.CurrentPageIndex);
            hash.Add(selected.PageCount);
            hash.Add(selected.Zoom);
            hash.Add(selected.IsLoaded);
            hash.Add(selected.ShowsChrome);
            hash.Add(selected.SidebarVisible);
            hash.Add((int)selected.SidebarMode);
            hash.Add(RuntimeHelpers.GetHashCode(selected.Thumbnails));
            AddMarks(ref hash, selected);
            AddEditing(ref hash, selected);
            hash.Add(RuntimeHelpers.GetHashCode(selected.Outline));
            hash.Add(selected.SelectedOutlineItem is { } bookmark ? RuntimeHelpers.GetHashCode(bookmark) : 0);
        }

        if (main.StatusMessage is { } status)
        {
            hash.Add(RuntimeHelpers.GetHashCode(status));
        }

        return hash.ToHashCode();
    }

    /// <summary>Lays the bars out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="sidebar">The side panel's scroll position.</param>
    /// <param name="size">The window size in device pixels.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The part of the window left for the pages, in device pixels, and what the side panel lists.</returns>
    internal static BarLayout Build(List<ChromeItem> items, MainViewModel main, ChromeLook look, SidebarView sidebar, SurfaceSize size, ChromeActions actions)
    {
        var metrics = look.Metrics;
        items.Clear();

        // A presentation puts every bar away, and reading mode keeps one slim bar with the way back, as the old window did.
        if (main.SelectedTab is { ShowsChrome: false } fullView)
        {
            var barHeight = fullView.IsReading ? AddReadBar(items, fullView, look, size.Width, actions) : 0;
            return new(new(0, barHeight, size.Width, Math.Max(0, size.Height - barHeight)), sidebar with { Bounds = default });
        }

        AddTabs(items, main, look, size.Width, actions);
        var top = metrics.TabHeight;
        top += actions.UsesNativeMenu ? 0 : MenuBar.Add(items, main, look, top, size.Width, actions.ShowBarMenu);
        top += AddToolbar(items, main, look, top, size.Width, actions);
        if (main.SelectedTab is { IsLoaded: true, AnnotationsInUse.IsAnnotating: true } marking)
        {
            top += ChromeMarkBar.Add(items, marking.Annotations, look, top, size.Width, actions);
        }

        if (main.SelectedTab is { IsLoaded: true, MediaIfMade: { } media } && ChromeMediaBar.IsShown(media))
        {
            top += ChromeMediaBar.Add(items, media, look, top, size.Width);
        }

        if (main.SelectedTab is { IsLoaded: true } shown)
        {
            top += ChromeToolBars.Add(items, shown, look, top, size.Width, actions);
        }

        var statusTop = AddStatus(items, main, look, top, size);
        var between = new LayoutRect(0, top, size.Width, Math.Max(0, statusTop - top));
        if (main.SelectedTab is not { SidebarVisible: true, IsLoaded: true } tab)
        {
            return new(between, sidebar with { Bounds = default });
        }

        var panel = ChromeSidebar.Build(items, tab, sidebar, look, between, actions, main.SidebarFollowsPage);
        return new(new(panel.Width, top, size.Width - panel.Width, between.Height), panel.View);
    }

    /// <summary>Gets the style bar text is measured with. The colour is chosen when the text is drawn.</summary>
    /// <param name="metrics">The sizes.</param>
    /// <returns>The style.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextStyle Style(in ChromeMetrics metrics) => new(metrics.TextSize, false, 0);

    /// <summary>Adds the one bar reading mode keeps: the document's name and the button that leaves reading mode.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="tab">The tab read.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="windowWidth">The window width.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The height the bar takes.</returns>
    private static int AddReadBar(List<ChromeItem> items, DocumentTabViewModel tab, ChromeLook look, int windowWidth, ChromeActions actions)
    {
        var runs = look.Runs;
        var metrics = look.Metrics;
        var height = metrics.TabHeight;
        var style = Style(metrics);
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, new(0, 0, windowWidth, height), true, false));

        // A window the system gives no title bar keeps its own buttons at the right, and the bar's contents have the rest.
        var width = windowWidth - WindowFrame.ButtonsWidth(look);
        WindowFrame.AddButtons(items, look, windowWidth, actions);
        var buttonWidth = Math.Min(width / ReadBarButtonShare, metrics.TabWidth);
        var room = width - buttonWidth - (metrics.Padding * ReadBarEdges);
        items.Add(new(ChromeItemKind.Label, null, 0, ChromeText.Fit(tab.Title, style, room, runs), new(metrics.Padding, 0, room, height), true, false));

        var leave = ChromeText.Fit(UiText.Get("Leave Read Mode"), style, buttonWidth, runs);
        var bounds = new LayoutRect(width - buttonWidth - metrics.Gap, metrics.Gap, buttonWidth, height - (metrics.Gap * ReadBarEdges));
        items.Add(new(ChromeItemKind.Button, tab.ReadModeCommand, 0, leave, bounds, true, false));
        return height;
    }

    /// <summary>Adds the status message, with a button to put it away when it is a message and not an error of the document.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and sizes.</param>
    /// <param name="toolBottom">The bottom of the toolbars.</param>
    /// <param name="size">The window size.</param>
    /// <returns>The bottom of the available reading area.</returns>
    private static int AddStatus(List<ChromeItem> items, MainViewModel main, ChromeLook look, int toolBottom, SurfaceSize size)
    {
        if ((main.StatusMessage ?? main.SelectedTab?.ErrorMessage) is not { Length: > 0 } status)
        {
            return size.Height;
        }

        var metrics = look.Metrics;
        var top = Math.Max(toolBottom, size.Height - metrics.StatusHeight);
        var width = size.Width;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, new(0, top, width, metrics.StatusHeight), true, false));
        var dismissWidth = main.StatusMessage is null ? 0 : metrics.StatusHeight * DismissWidthInLines;
        var room = Math.Max(0, width - metrics.Padding - metrics.Padding - dismissWidth);
        var fitted = ChromeText.Fit(status, Style(metrics), room, look.Runs);
        items.Add(new(ChromeItemKind.Label, null, 0, fitted, new(metrics.Padding, top, room, metrics.StatusHeight), true, false));
        if (dismissWidth != 0)
        {
            var bounds = new LayoutRect(width - dismissWidth, top, dismissWidth, metrics.StatusHeight);
            items.Add(new(ChromeItemKind.Button, main.DismissStatusCommand, 0, UiText.Get("Dismiss"), bounds, true, false));
        }

        return top;
    }

    /// <summary>Adds what the edit bar shows to the stamp: whether the page's own content is being edited, whether something is picked, and whether there is an edit to undo or redo.</summary>
    /// <param name="hash">The stamp so far.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddEditing(ref HashCode hash, DocumentTabViewModel tab)
    {
        var edit = tab.ContentEditIfMade;
        hash.Add(edit is { IsEditing: true });
        if (edit is not { IsEditing: true })
        {
            return;
        }

        hash.Add(edit.Selected is not null);
        hash.Add(edit.TextEdit is not null);

        // A tab whose pages have not been managed has nothing to undo, and reading the page tools here would make them for every tab shown.
        hash.Add(tab.PagesInUse is { } pages && ((ICommand)pages.UndoCommand).CanExecute(null));
        hash.Add(tab.PagesInUse is { } managed && ((ICommand)managed.RedoCommand).CanExecute(null));
    }

    /// <summary>Adds what the marking bar and the notes, files and layers lists show to the stamp.</summary>
    /// <param name="hash">The stamp so far.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddMarks(ref HashCode hash, DocumentTabViewModel tab)
    {
        // Each of these view models is made the first time it is read, so one the side panel has not shown yet is left alone.
        if (tab.SidebarVisible)
        {
            hash.Add(tab.SidebarMode switch
            {
                ViewModels.Sidebar.SidebarMode.Annotations => tab.Annotations.VisibleItems.Count,
                ViewModels.Sidebar.SidebarMode.Attachments => tab.Attachments.Items.Count,
                ViewModels.Sidebar.SidebarMode.Layers => tab.Layers.Items.Count,
                _ => 0,
            });
        }

        // A tab whose annotation tools have not been used shows none of their state.
        if (tab.AnnotationsInUse is not { } marks)
        {
            return;
        }

        hash.Add(marks.IsAnnotating);
        hash.Add((int)marks.Tool);
        hash.Add(marks.Color);
        hash.Add(marks.LineWidth);
        hash.Add(marks.CanUndo);
        hash.Add(marks.CanRedo);
        hash.Add(marks.Selected is { } picked ? RuntimeHelpers.GetHashCode(picked) : 0);
    }

    /// <summary>Adds the tab strip.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    private static void AddTabs(List<ChromeItem> items, MainViewModel main, ChromeLook look, int width, ChromeActions actions)
    {
        var runs = look.Runs;
        var metrics = look.Metrics;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, new(0, 0, width, metrics.TabHeight), true, false));
        var tabs = main.Tabs;
        var closeWidth = metrics.TabHeight - metrics.Gap - metrics.Gap;
        var newTabWidth = metrics.TabHeight;

        // A window the system gives no title bar keeps its own buttons at the right of the strip, and the tabs have the rest.
        var room = width - newTabWidth - WindowFrame.ButtonsWidth(look);
        WindowFrame.AddButtons(items, look, width, actions);
        var tabWidth = tabs.Count == 0 ? 0 : Math.Min(metrics.TabWidth, Math.Max(Math.Max(closeWidth + metrics.Padding, metrics.TabWidth / NarrowestShare), room / tabs.Count));

        // With more tabs than fit at the narrowest width, the strip scrolls just far enough to keep the chosen tab in view.
        var chosen = main.SelectedTab is { } selectedTab ? tabs.IndexOf(selectedTab) : -1;
        var scroll = Math.Max(0, ((chosen + 1) * tabWidth) - room);
        var style = Style(metrics);
        var x = -scroll;
        for (var i = 0; i < tabs.Count; i++, x += tabWidth)
        {
            if (x + tabWidth <= 0 || x >= room)
            {
                continue;
            }

            var tab = tabs[i];
            var name = tab.HasUnsavedChanges ? string.Concat(tab.Title, UnsavedMark) : tab.Title;
            var textRoom = tabWidth - metrics.Padding - closeWidth - metrics.Gap;
            var selected = ReferenceEquals(tab, main.SelectedTab);
            items.Add(new(ChromeItemKind.Tab, actions.SelectTab, i, ChromeText.Fit(name, style, textRoom, runs), new(x, 0, tabWidth, metrics.TabHeight), true, selected) { Parameter = tab });
            var close = new LayoutRect(x + tabWidth - closeWidth - metrics.Gap, metrics.Gap, closeWidth, closeWidth);
            items.Add(new(ChromeItemKind.Button, main.CloseTabCommand, i, UiText.Get("Close tab"), close, true, false)
            {
                Parameter = tab,
                Icon = ChromeIcon.Close,
                HidesText = true,
                Help = Descriptions.CloseTab,
            });
        }

        var open = new LayoutRect(Math.Min(x, room), 0, newTabWidth, metrics.TabHeight);
        items.Add(new(ChromeItemKind.Button, main.OpenCommand, 0, UiText.Get("Open"), open, true, false) { Icon = ChromeIcon.Add, HidesText = true });
    }

    /// <summary>Adds the toolbar.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">The top of the toolbar.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The height the toolbar takes, which is more than one line when the window is too narrow for every button.</returns>
    private static int AddToolbar(List<ChromeItem> items, MainViewModel main, ChromeLook look, int top, int width, ChromeActions actions)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, default, true, false));
        var lines = FillToolbar(items, main, look, top, width, actions);

        var height = lines * metrics.ToolbarHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }

    /// <summary>Lays the toolbar's buttons out from the left, on as many lines as they need.</summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">The top of the toolbar.</param>
    /// <param name="width">The window width.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The lines the buttons took.</returns>
    private static int FillToolbar(List<ChromeItem> items, MainViewModel main, ChromeLook look, int top, int width, ChromeActions actions)
    {
        var metrics = look.Metrics;
        var row = new ChromeRow(items, look.Runs, metrics, metrics.Gap, top + metrics.Gap, metrics.ToolbarHeight - metrics.Gap - metrics.Gap)
        {
            Limit = width - metrics.Gap,
            ShowLabels = look.ShowLabels,
            Describe = ButtonHelp.Toolbar,
        };

        // A button whose command belongs to a document has none until a document is open, and is drawn faded.
        var tab = LoadedTab(main);
        row = row.Button(ChromeIcon.Open, UiText.Get("Open"), main.OpenCommand, true);
        row = row.Button(ChromeIcon.Save, UiText.Get("Save"), main.SelectedTab?.SaveCommand, main.SelectedTab is { HasUnsavedChanges: true });
        row = row.Button(ChromeIcon.Print, UiText.Get("Print"), tab?.PrintCommand, tab is not null);
        row = row.Divider();
        row = AddTools(row, tab, actions);
        row = row.Divider();
        row = AddNavigation(row, tab, actions);
        row = row.MenuButton(ChromeIcon.Dual, UiText.Get("View"), actions.ShowView, tab is not null);
        row = row.Divider();
        row = row.IconButton(ChromeIcon.ZoomOut, UiText.Get("Zoom out"), tab?.ZoomOutCommand, tab is not null);
        row = row.MenuButton(ChromeIcon.PageDown, tab?.ZoomText ?? "100%", actions.ShowZoom, tab is not null, Descriptions.ZoomMenu);
        row = row.IconButton(ChromeIcon.ZoomIn, UiText.Get("Zoom in"), tab?.ZoomInCommand, tab is not null);
        row = row.IconButton(ChromeIcon.FitWidth, UiText.Get("Fit width"), tab?.FitWidthCommand, tab is not null);
        row = row.IconButton(ChromeIcon.FitPage, UiText.Get("Fit page"), tab?.FitPageCommand, tab is not null);
        return row.Lines;
    }

    /// <summary>Gets the tab shown when its document is open.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The tab, or <see langword="null"/> when no tab is shown or its document is not open yet.</returns>
    private static DocumentTabViewModel? LoadedTab(MainViewModel main) => main.SelectedTab is { IsLoaded: true } tab ? tab : null;

    /// <summary>Adds the buttons that move about the document: back and forward along the places shown, the page before and the next, and the page to go to.</summary>
    /// <param name="row">The toolbar so far.</param>
    /// <param name="tab">The tab shown when its document is open, or <see langword="null"/>.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The toolbar with the buttons in it.</returns>
    private static ChromeRow AddNavigation(ChromeRow row, DocumentTabViewModel? tab, ChromeActions actions)
    {
        row = row.Button(ChromeIcon.Back, UiText.Get("Back"), tab?.GoBackCommand, tab is { CanGoBack: true });
        row = row.Button(ChromeIcon.Forward, UiText.Get("Forward"), tab?.GoForwardCommand, tab is { CanGoForward: true });
        row = row.Divider();
        row = row.IconButton(ChromeIcon.PageUp, UiText.Get("Previous"), tab?.PreviousPageCommand, tab is { CurrentPageIndex: > 0 });
        row = row.IconButton(ChromeIcon.PageDown, UiText.Get("Next"), tab?.NextPageCommand, tab is not null && tab.CurrentPageIndex < tab.PageCount - 1);
        return row.Button(PageText(tab), tab is null ? null : actions.GoToPage, tab is not null);
    }

    /// <summary>Adds the buttons of the tools that change the document or how it is read: marking, filling and signing, editing, finding, focus mode, read aloud and the side panel.</summary>
    /// <param name="row">The toolbar so far.</param>
    /// <param name="tab">The tab shown when its document is open, or <see langword="null"/>.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The toolbar with the buttons in it.</returns>
    private static ChromeRow AddTools(ChromeRow row, DocumentTabViewModel? tab, ChromeActions actions)
    {
        var open = tab is not null;
        row = row.Toggle(ChromeIcon.Highlight, UiText.Get("Annotate"), tab is { CanAnnotate: true } ? actions.ToggleAnnotate : null, tab is { AnnotationsInUse.IsAnnotating: true });
        row = row.Toggle(ChromeIcon.FillSign, UiText.Get("Fill & Sign"), tab?.ToggleFillAndSignCommand, tab is { FillAndSignIfMade.IsActive: true });
        row = row.Toggle(ChromeIcon.Cursor, UiText.Get("Edit"), tab?.ToggleContentEditingCommand, tab is { ContentEditIfMade.IsEditing: true });
        row = row.Toggle(ChromeIcon.Thumbnails, UiText.Get("Organise"), tab?.ToggleOrganisePagesCommand, tab is { PagesInUse.IsOrganising: true });
        row = row.Divider();
        row = row.Button(ChromeIcon.Search, UiText.Get("Find"), open ? actions.ShowFind : null, open);
        row = row.Toggle(ChromeIcon.Focus, UiText.Get("Focus"), tab?.ToggleFocusModeCommand, tab is { FocusModeIfMade.IsOn: true });
        row = row.Toggle(ChromeIcon.Speaker, UiText.Get("Read Aloud"), tab?.ToggleReadAloudCommand, tab is { ReadAloudIfMade.IsOpen: true });
        return row.Toggle(ChromeIcon.Sidebar, UiText.Get("Side panel"), tab?.ToggleSidebarCommand, tab is { SidebarVisible: true });
    }

    /// <summary>Says which page is shown.</summary>
    /// <param name="tab">The tab shown, or <see langword="null"/>.</param>
    /// <returns>The text, such as "Page 3 of 40".</returns>
    private static string PageText(DocumentTabViewModel? tab) =>
        tab is { PageCount: > 0 } ? UiText.Format("Page {0} of {1}", tab.CurrentPageIndex + 1, tab.PageCount) : UiText.Get("No pages");
}
