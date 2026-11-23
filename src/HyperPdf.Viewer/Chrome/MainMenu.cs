// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Builds the menus the menu bar and the toolbar share, and the menu of a tab.</summary>
internal static class MainMenu
{
    /// <summary>The recent documents listed.</summary>
    private const int RecentLimit = 12;

    /// <summary>The resource key for the Save menu entry.</summary>
    private const string SaveKey = "_Save";

    /// <summary>Builds the menu of one tab, which opens on a right click on it: close it, close the others, close all, save and reload.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> ForTab(MainViewModel main, DocumentTabViewModel tab) =>
    [
        MenuEntry.Item(UiText.Get("_Close"), "Ctrl+W", main.CloseTabCommand, tab, true),
        MenuEntry.Item(UiText.Get("Close _Other Tabs"), null, main.CloseOtherTabsCommand, tab, main.Tabs.Count > 1),
        MenuEntry.Item(UiText.Get("Close _All Tabs"), null, main.CloseAllTabsCommand, null, true),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get(SaveKey), null, tab.SaveCommand, null, tab.HasUnsavedChanges),
        MenuEntry.Item(UiText.Get("Re_load"), null, tab.ReloadCommand, null, tab.IsLoaded),
    ];

    /// <summary>Lists the actions for reading and navigating the current view.</summary>
    /// <param name="main">The window's view model.</param>
    /// <param name="tab">The open document.</param>
    /// <returns>The toolbar menu.</returns>
    internal static List<MenuEntry> ReadingView(MainViewModel main, DocumentTabViewModel tab)
    {
        List<MenuEntry> entries =
        [
            MenuEntry.Submenu(UiText.Get("Page _Layout and Zoom"), true, [..ToolMenus.Layout(tab)]),
            MenuEntry.Submenu(UiText.Get("_Go"), true, [..Go(tab)]),
        ];
        AddViewControls(entries, main, tab);
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Radio(UiText.Get("_Reading Mode"), tab.ReadModeCommand, null, tab.IsReading));
        entries.Add(MenuEntry.Radio(UiText.Get("Presen_tation"), tab.PresentCommand, null, tab.IsPresenting));
        entries.Add(MenuEntry.Radio(UiText.Get("Page _by Page"), tab.TogglePageByPageCommand, null, tab.IsPageByPage));
        entries.Add(MenuEntry.Radio(UiText.Get("_Auto-scroll"), tab.PageTools.ToggleAutoScrollCommand, null, tab.PageTools.IsAutoScrolling));
        entries.Add(MenuEntry.Separator());
        AddPointerTools(entries, tab);
        return entries;
    }

    /// <summary>Lists the recent documents.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Recent(MainViewModel main)
    {
        var entries = new List<MenuEntry>();
        var count = Math.Min(main.RecentDocuments.Count, RecentLimit);
        for (var i = 0; i < count; i++)
        {
            var recent = main.RecentDocuments[i];
            entries.Add(MenuEntry.Item(Path.GetFileName(recent.FilePath), null, main.OpenRecentCommand, recent, true).WithIcon("Document"));
        }

        return entries;
    }

    /// <summary>Lists the ways to change how the pages are shown.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> View(MainViewModel main, DocumentTabViewModel tab)
    {
        var entries = new List<MenuEntry>
        {
            MenuEntry.Item(UiText.Get("Zoom _In"), "Ctrl++", tab.ZoomInCommand, null, true),
            MenuEntry.Item(UiText.Get("Zoom _Out"), "Ctrl+-", tab.ZoomOutCommand, null, true),
            MenuEntry.Item(UiText.Get("Actual _Size"), "Ctrl+0", tab.ZoomResetCommand, null, true),
            MenuEntry.Item(UiText.Get("Fit _Width"), "Ctrl+2", tab.FitWidthCommand, null, true),
            MenuEntry.Item(UiText.Get("Fit _Page"), "Ctrl+1", tab.FitPageCommand, null, true),
        };
        AddViewControls(entries, main, tab);
        return entries;
    }

    /// <summary>Lists the ways to move through the document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Go(DocumentTabViewModel tab) =>
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

    /// <summary>Adds the three ways to use the pointer on the page.</summary>
    /// <param name="entries">The menu being built.</param>
    /// <param name="tab">The current document.</param>
    internal static void AddPointerTools(List<MenuEntry> entries, DocumentTabViewModel tab)
    {
        var pages = tab.PageTools;
        var marking = tab.AnnotationsInUse is { Tool: not AnnotationTool.Select };
        entries.Add(MenuEntry.Radio(UiText.Get("_Select Text"), pages.SetPageToolCommand, PageTool.SelectText, !marking && pages.PageTool == PageTool.SelectText));
        entries.Add(MenuEntry.Radio(UiText.Get("_Hand"), pages.SetPageToolCommand, PageTool.Hand, !marking && pages.PageTool == PageTool.Hand));
        entries.Add(MenuEntry.Radio(UiText.Get("_Zoom to Area"), pages.SetPageToolCommand, PageTool.ZoomArea, !marking && pages.PageTool == PageTool.ZoomArea));
    }

    /// <summary>Makes the entry of a marking tool, ticked while it is the tool in use.</summary>
    /// <param name="marks">The marking tools of the tab shown.</param>
    /// <param name="header">The entry's text.</param>
    /// <param name="tool">The tool.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static MenuEntry MarkTool(AnnotationsViewModel marks, string header, AnnotationTool tool) =>
        MenuEntry.Radio(header, marks.SetToolCommand, tool, marks.Tool == tool);

    /// <summary>Adds rotation and the switches that change the reading view.</summary>
    /// <param name="entries">The menu being built.</param>
    /// <param name="main">The window's view model.</param>
    /// <param name="tab">The current document.</param>
    private static void AddViewControls(List<MenuEntry> entries, MainViewModel main, DocumentTabViewModel tab)
    {
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("Rotate _Left"), null, tab.RotateLeftCommand, null, true));
        entries.Add(MenuEntry.Item(UiText.Get("Rotate _Right"), null, tab.RotateRightCommand, null, true));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Radio(UiText.Get("Page _Tone"), main.TogglePageToneCommand, null, main.PageToneEnabled));
        entries.Add(MenuEntry.Radio(UiText.Get("_Caret Mode"), tab.ToggleCaretModeCommand, null, tab.IsCaretMode));
        entries.Add(MenuEntry.Radio(UiText.Get("_Side Panel"), tab.ToggleSidebarCommand, null, tab.SidebarVisible));
        entries.Add(MenuEntry.Radio(UiText.Get("S_plit the View"), tab.SplitViewCommand, null, tab.IsSplitView));
    }
}
