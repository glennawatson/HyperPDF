// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Builds the main menu from the commands of the view models.</summary>
internal static class MainMenu
{
    /// <summary>The recent documents listed.</summary>
    private const int RecentLimit = 12;

    /// <summary>Builds the menu for the state the window is in.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Build(MainViewModel main)
    {
        var tab = main.SelectedTab;
        var entries = new List<MenuEntry>
        {
            MenuEntry.Item(UiText.Get("_Open…"), "Ctrl+O", main.OpenCommand, null, true),
            MenuEntry.Submenu(UiText.Get("Open _Recent"), main.RecentDocuments.Count > 0, Recent(main)),
            MenuEntry.Item(UiText.Get("Reopen _Closed Tab"), null, main.ReopenClosedTabCommand, null, true),
        };
        if (tab is null)
        {
            return entries;
        }

        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("_Save"), "Ctrl+S", tab.SaveCommand, null, tab.HasUnsavedChanges));
        entries.Add(MenuEntry.Item(UiText.Get("Save _As…"), null, tab.SaveAsCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Item(UiText.Get("Re_load"), null, tab.ReloadCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Submenu(UiText.Get("_View"), tab.IsLoaded, View(main, tab)));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Go"), tab.IsLoaded, Go(tab)));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Tabs"), true, Tabs(main, tab)));
        return entries;
    }

    /// <summary>Lists the recent documents.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Recent(MainViewModel main)
    {
        var entries = new List<MenuEntry>();
        var count = Math.Min(main.RecentDocuments.Count, RecentLimit);
        for (var i = 0; i < count; i++)
        {
            var recent = main.RecentDocuments[i];
            entries.Add(MenuEntry.Item(Path.GetFileName(recent.FilePath), null, main.OpenRecentCommand, recent, true));
        }

        return entries;
    }

    /// <summary>Lists the ways to change how the pages are shown.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> View(MainViewModel main, DocumentTabViewModel tab) =>
    [
        MenuEntry.Item(UiText.Get("Zoom _In"), "Ctrl++", tab.ZoomInCommand, null, true),
        MenuEntry.Item(UiText.Get("Zoom _Out"), "Ctrl+-", tab.ZoomOutCommand, null, true),
        MenuEntry.Item(UiText.Get("Actual _Size"), null, tab.ZoomResetCommand, null, true),
        MenuEntry.Item(UiText.Get("Fit _Width"), null, tab.FitWidthCommand, null, true),
        MenuEntry.Item(UiText.Get("Fit _Page"), null, tab.FitPageCommand, null, true),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get("Rotate _Left"), null, tab.RotateLeftCommand, null, true),
        MenuEntry.Item(UiText.Get("Rotate _Right"), null, tab.RotateRightCommand, null, true),
        MenuEntry.Separator(),
        MenuEntry.Radio(UiText.Get("Page _Tone"), main.TogglePageToneCommand, null, main.PageToneEnabled),
        MenuEntry.Radio(UiText.Get("_Caret Mode"), tab.ToggleCaretModeCommand, null, tab.IsCaretMode),
        MenuEntry.Radio(UiText.Get("_Bookmarks"), tab.ToggleSidebarCommand, null, tab.SidebarVisible && tab.Outline.Count > 0),
    ];

    /// <summary>Lists the ways to move through the document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Go(DocumentTabViewModel tab) =>
    [
        MenuEntry.Item(UiText.Get("_First Page"), "Ctrl+Home", tab.FirstPageCommand, null, tab.CurrentPageIndex > 0),
        MenuEntry.Item(UiText.Get("_Previous Page"), null, tab.PreviousPageCommand, null, tab.CurrentPageIndex > 0),
        MenuEntry.Item(UiText.Get("_Next Page"), null, tab.NextPageCommand, null, tab.CurrentPageIndex < tab.PageCount - 1),
        MenuEntry.Item(UiText.Get("_Last Page"), "Ctrl+End", tab.LastPageCommand, null, tab.CurrentPageIndex < tab.PageCount - 1),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get("_Back"), null, tab.GoBackCommand, null, tab.CanGoBack),
        MenuEntry.Item(UiText.Get("For_ward"), null, tab.GoForwardCommand, null, tab.CanGoForward),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get("F_ind…"), "Ctrl+F", tab.FindCommand, null, true),
    ];

    /// <summary>Lists what can be done with the tabs.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Tabs(MainViewModel main, DocumentTabViewModel tab) =>
    [
        MenuEntry.Item(UiText.Get("_Next Tab"), "Ctrl+Tab", main.NextTabCommand, null, main.Tabs.Count > 1),
        MenuEntry.Item(UiText.Get("_Previous Tab"), null, main.PreviousTabCommand, null, main.Tabs.Count > 1),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get("_Close Tab"), "Ctrl+W", main.CloseTabCommand, tab, true),
        MenuEntry.Item(UiText.Get("Close _Other Tabs"), null, main.CloseOtherTabsCommand, tab, main.Tabs.Count > 1),
        MenuEntry.Item(UiText.Get("Close _All Tabs"), null, main.CloseAllTabsCommand, null, true),
    ];
}
