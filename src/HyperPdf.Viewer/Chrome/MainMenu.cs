// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Builds the main menu from the commands of the view models.</summary>
internal static class MainMenu
{
    /// <summary>The recent documents listed.</summary>
    private const int RecentLimit = 12;

    /// <summary>The resource key for the Save menu entry.</summary>
    private const string SaveKey = "_Save";

    /// <summary>Builds the menu for the state the window is in.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Build(MainViewModel main)
    {
        var tab = main.SelectedTab;
        var entries = new List<MenuEntry>
        {
            MenuEntry.Item(UiText.Get("_Open…"), "Ctrl+O", main.OpenCommand, null, true),
            MenuEntry.Submenu(UiText.Get("Open _Recent"), main.RecentDocuments.Count > 0, [..Recent(main)]),
            MenuEntry.Item(UiText.Get("Reopen _Closed Tab"), null, main.ReopenClosedTabCommand, null, true),
        };
        if (tab is null)
        {
            AddAppEntries(entries, main);
            return entries;
        }

        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get(SaveKey), "Ctrl+S", tab.SaveCommand, null, tab.HasUnsavedChanges));
        entries.Add(MenuEntry.Item(UiText.Get("_Print…"), "Ctrl+P", tab.Printing.PrintCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Document"), tab.IsLoaded, [..Document(main, tab)]));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Submenu(UiText.Get("_View"), tab.IsLoaded, [..View(main, tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Go"), tab.IsLoaded, [..Go(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("Page _Layout and Zoom"), tab.IsLoaded, [..ToolMenus.Layout(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Reading Aids"), tab.IsLoaded, [..ToolMenus.Reading(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("T_ools"), tab.IsLoaded, [..Tools(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Measure, Sign and Edit"), tab.IsLoaded, [..ToolMenus.PageWork(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Pages"), tab.IsLoaded, [..ToolMenus.Pages(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("Add _to Pages"), tab.IsLoaded, [..ToolMenus.Decorations(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Bookmarks"), tab.IsLoaded, [..ToolMenus.Bookmarks(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("Signatures and Ta_gs"), tab.IsLoaded, [..ToolMenus.Trust(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Tabs"), true, [..Tabs(main, tab)]));
        AddAppEntries(entries, main);
        return entries;
    }

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

    /// <summary>Lists the current document's file and output actions.</summary>
    /// <param name="main">The window's view model.</param>
    /// <param name="tab">The open document.</param>
    /// <returns>The toolbar menu.</returns>
    internal static List<MenuEntry> Document(MainViewModel main, DocumentTabViewModel tab)
    {
        var entries = new List<MenuEntry>();
        AddDocumentEntries(entries, main, tab);
        ICommand folder = main.ShowInFolderCommand;
        entries.Add(MenuEntry.Item(UiText.Get("Show the Document in Its F_older"), null, folder, null, folder.CanExecute(null)));
        return entries;
    }

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

    /// <summary>Adds the file actions shared by the main and Document menus.</summary>
    /// <param name="entries">The menu being built.</param>
    /// <param name="main">The window's view model.</param>
    /// <param name="tab">The current document.</param>
    private static void AddDocumentEntries(List<MenuEntry> entries, MainViewModel main, DocumentTabViewModel tab)
    {
        entries.Add(MenuEntry.Item(UiText.Get(SaveKey), "Ctrl+S", tab.SaveCommand, null, tab.HasUnsavedChanges));
        entries.Add(MenuEntry.Item(UiText.Get("Save _As…"), null, tab.SaveAsCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Item(UiText.Get("Re_load"), null, tab.ReloadCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Submenu(UiText.Get("Save a Cop_y"), tab.IsLoaded, [..ToolMenus.Copies(tab)]));
        entries.Add(MenuEntry.Item(UiText.Get("_Print…"), "Ctrl+P", tab.Printing.PrintCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Item(UiText.Get("Print with the System _Dialog…"), null, tab.Printing.PrintWithSystemDialogCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Item(UiText.Get("Document Propert_ies…"), null, main.PropertiesCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Item(UiText.Get("_Compare with Another Document…"), null, tab.Pages.Compare.CompareCommand, null, tab.IsLoaded));
        entries.Add(MenuEntry.Item(UiText.Get("Remove _Hidden Information…"), null, tab.Pages.Sanitize.ShowCommand, null, tab.IsLoaded));
    }

    /// <summary>Adds the entries about the app itself, which are there with or without a document.</summary>
    /// <param name="entries">Receives the entries.</param>
    /// <param name="main">The main view model.</param>
    private static void AddAppEntries(List<MenuEntry> entries, MainViewModel main)
    {
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Submenu(UiText.Get("_Window"), true, [..ToolMenus.Window(main)]));
        entries.Add(MenuEntry.Item(UiText.Get("Pre_ferences…"), "Ctrl+,", main.PreferencesCommand, null, true));
        entries.Add(MenuEntry.Item(UiText.Get("_Licences…"), null, main.LicencesCommand, null, true));
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
            entries.Add(MenuEntry.Item(Path.GetFileName(recent.FilePath), null, main.OpenRecentCommand, recent, true).WithIcon("Document"));
        }

        return entries;
    }

    /// <summary>Lists the ways to change how the pages are shown.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> View(MainViewModel main, DocumentTabViewModel tab)
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

    /// <summary>Lists what the pointer does on the page: the page tools, and the marking tools that need no typed text or picked file.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries, with the tool in use ticked.</returns>
    private static List<MenuEntry> Tools(DocumentTabViewModel tab)
    {
        var marks = tab.Annotations;
        var entries = new List<MenuEntry>();
        AddPointerTools(entries, tab);
        entries.Add(MenuEntry.Separator());
        entries.Add(MarkTool(marks, UiText.Get("H_ighlight"), AnnotationTool.Highlight));
        entries.Add(MarkTool(marks, UiText.Get("_Underline"), AnnotationTool.Underline));
        entries.Add(MarkTool(marks, UiText.Get("Stri_ke Out"), AnnotationTool.StrikeOut));
        entries.Add(MarkTool(marks, UiText.Get("_Draw"), AnnotationTool.Draw));
        entries.Add(MarkTool(marks, UiText.Get("_Note"), AnnotationTool.Note));
        entries.Add(MarkTool(marks, UiText.Get("_Rectangle"), AnnotationTool.Rectangle));
        entries.Add(MarkTool(marks, UiText.Get("_Ellipse"), AnnotationTool.Ellipse));
        entries.Add(MarkTool(marks, UiText.Get("_Arrow"), AnnotationTool.Arrow));
        entries.Add(MarkTool(marks, UiText.Get("_Line"), AnnotationTool.Line));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("U_ndo"), null, marks.UndoCommand, null, true));
        entries.Add(MenuEntry.Item(UiText.Get("Re_do"), null, marks.RedoCommand, null, true));
        return entries;
    }

    /// <summary>Adds the three ways to use the pointer on the page.</summary>
    /// <param name="entries">The menu being built.</param>
    /// <param name="tab">The current document.</param>
    private static void AddPointerTools(List<MenuEntry> entries, DocumentTabViewModel tab)
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
    private static MenuEntry MarkTool(AnnotationsViewModel marks, string header, AnnotationTool tool) =>
        MenuEntry.Radio(header, marks.SetToolCommand, tool, marks.Tool == tool);

    /// <summary>Lists what can be done with the tabs.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> Tabs(MainViewModel main, DocumentTabViewModel tab) =>
    [
        MenuEntry.Item(UiText.Get("_Next Tab"), "Ctrl+Tab", main.NextTabCommand, null, main.Tabs.Count > 1),
        MenuEntry.Item(UiText.Get("_Previous Tab"), null, main.PreviousTabCommand, null, main.Tabs.Count > 1),
        MenuEntry.Item(UiText.Get("_Go to a Document…"), "Ctrl+Shift+A", main.ShowTabFinderCommand, null, main.Tabs.Count > 1),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get("_Close Tab"), "Ctrl+W", main.CloseTabCommand, tab, true),
        MenuEntry.Item(UiText.Get("Close _Other Tabs"), null, main.CloseOtherTabsCommand, tab, main.Tabs.Count > 1),
        MenuEntry.Item(UiText.Get("Close _All Tabs"), null, main.CloseAllTabsCommand, null, true),
    ];
}
