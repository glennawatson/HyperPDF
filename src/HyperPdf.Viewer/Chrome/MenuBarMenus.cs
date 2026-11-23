// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Text;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// Builds the menus of the menu bar: File, Edit, View, Document, Comment, Forms, Tools, Window and Help. Each holds what a desktop
/// PDF editor keeps under that name, and every entry runs a view model command.
/// </summary>
internal static class MenuBarMenus
{
    /// <summary>The words of the entry that undoes.</summary>
    private const string UndoKey = "U_ndo";

    /// <summary>The words of the entry that redoes.</summary>
    private const string RedoKey = "Re_do";

    /// <summary>Lists what is done with files: opening, saving, printing, the document's details and closing.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> File(MainViewModel main, ChromeActions actions)
    {
        List<MenuEntry> entries =
        [
            MenuEntry.Item(UiText.Get("_Open…"), "Ctrl+O", main.OpenCommand, null, true),
            MenuEntry.Submenu(UiText.Get("Open _Recent"), main.RecentDocuments.Count > 0, [.. MainMenu.Recent(main)]),
            MenuEntry.Item(UiText.Get("Reopen _Closed Tab"), null, main.ReopenClosedTabCommand, null, true),
            MenuEntry.Item(UiText.Get("_New Window"), "Ctrl+N", main.NewWindowCommand, null, true),
            Item("Convert _Pictures to a PDF…", main.ConvertPicturesCommand),
        ];
        if (main.SelectedTab is { } tab)
        {
            AddDocumentFile(entries, main, tab);
        }

        entries.Add(MenuEntry.Separator());
        entries.Add(Item("Forget the Recent Documents", main.ClearRecentCommand));
        entries.Add(MenuEntry.Item(UiText.Get("Close the _Window"), null, actions.CloseWindow, null, true).WithIcon("Close"));
        return entries;
    }

    /// <summary>Lists undo and redo, finding, the modes that change the document, and the app's preferences.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Edit(MainViewModel main)
    {
        List<MenuEntry> entries = [];
        if (main.SelectedTab is { IsLoaded: true } tab)
        {
            AddUndo(entries, main);
            entries.Add(MenuEntry.Item(UiText.Get("F_ind…"), "Ctrl+F", tab.FindCommand, null, true));
            entries.Add(MenuEntry.Item(UiText.Get("Find and _Replace in the Pages…"), null, tab.FindAndReplaceCommand, null, true).WithIcon("Replace"));
            entries.Add(MenuEntry.Separator());
            entries.Add(MenuEntry.Radio(UiText.Get("_Edit the Page's Own Content"), tab.ContentEdit.ToggleEditingCommand, null, tab.ContentEditIfMade is { IsEditing: true }));
            entries.Add(MenuEntry.Radio(UiText.Get("_Organise Pages"), tab.ToggleOrganisePagesCommand, null, tab.PagesInUse is { IsOrganising: true }));
            entries.Add(MenuEntry.Radio(UiText.Get("_Fill and Sign"), tab.ToggleFillAndSignCommand, null, tab.FillAndSignIfMade is { IsActive: true }));
            entries.Add(MenuEntry.Separator());
        }

        entries.Add(Item("Search a _Folder of Documents…", main.SearchFolderCommand));
        entries.Add(MenuEntry.Item(UiText.Get("Pre_ferences…"), "Ctrl+,", main.PreferencesCommand, null, true));
        return entries;
    }

    /// <summary>Lists how the pages are shown: zoom, layout, moving about, turning, the reading aids and the reading views.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> View(MainViewModel main, DocumentTabViewModel tab)
    {
        var entries = MainMenu.View(main, tab);
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Submenu(UiText.Get("Page _Layout and Zoom"), true, [.. ToolMenus.Layout(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Go"), true, [.. MainMenu.Go(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("_Reading Aids"), true, [.. ToolMenus.Reading(tab)]));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Radio(UiText.Get("_Reading Mode"), tab.ReadModeCommand, null, tab.IsReading));
        entries.Add(MenuEntry.Radio(UiText.Get("Presen_tation"), tab.PresentCommand, null, tab.IsPresenting));
        entries.Add(MenuEntry.Radio(UiText.Get("Page _by Page"), tab.TogglePageByPageCommand, null, tab.IsPageByPage));
        entries.Add(MenuEntry.Radio(UiText.Get("_Auto-scroll"), tab.PageTools.ToggleAutoScrollCommand, null, tab.PageTools.IsAutoScrolling));
        return entries;
    }

    /// <summary>Lists what is done to the document as a whole: its pages, what is added to them, its bookmarks, and comparing and cleaning it.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Document(MainViewModel main, DocumentTabViewModel tab) =>
    [
        MenuEntry.Radio(UiText.Get("_Organise Pages"), tab.ToggleOrganisePagesCommand, null, tab.PagesInUse is { IsOrganising: true }),
        MenuEntry.Separator(),
        MenuEntry.Submenu(UiText.Get("_Pages"), true, [.. ToolMenus.Pages(tab)]),
        MenuEntry.Submenu(UiText.Get("Add _to Pages"), true, [.. ToolMenus.Decorations(tab)]),
        MenuEntry.Submenu(UiText.Get("_Bookmarks"), true, [.. ToolMenus.Bookmarks(tab)]),
        MenuEntry.Separator(),
        MenuEntry.Item(UiText.Get("_Compare with Another Document…"), null, tab.Pages.Compare.CompareCommand, null, true),
        MenuEntry.Item(UiText.Get("Remove _Hidden Information…"), null, tab.Pages.Sanitize.ShowCommand, null, true),
        Item("Attach a _File…", tab.Attachments.AttachFilesCommand).WithIcon("Attachment"),
        MenuEntry.Item(UiText.Get("Document Propert_ies…"), "Alt+Enter", main.PropertiesCommand, null, true),
        MenuEntry.Item(UiText.Get("Change the Document _Details…"), null, tab.EditDocumentInfoCommand, null, true).WithIcon("Text"),
    ];

    /// <summary>Lists the marking tools, the notes list and the marks' undo and redo.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <param name="actions">The commands of the window's own controls, one of which shows the marking bar.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Comment(DocumentTabViewModel tab, ChromeActions actions)
    {
        var marks = tab.Annotations;
        return
        [
            MenuEntry.Radio(UiText.Get("_Annotate"), actions.ToggleAnnotate, null, marks.IsAnnotating),
            MenuEntry.Separator(),
            MainMenu.MarkTool(marks, UiText.Get("H_ighlight"), AnnotationTool.Highlight),
            MainMenu.MarkTool(marks, UiText.Get("_Underline"), AnnotationTool.Underline),
            MainMenu.MarkTool(marks, UiText.Get("Stri_ke Out"), AnnotationTool.StrikeOut),
            MainMenu.MarkTool(marks, UiText.Get("_Draw"), AnnotationTool.Draw),
            MainMenu.MarkTool(marks, UiText.Get("_Note"), AnnotationTool.Note),
            MainMenu.MarkTool(marks, UiText.Get("_Rectangle"), AnnotationTool.Rectangle),
            MainMenu.MarkTool(marks, UiText.Get("_Ellipse"), AnnotationTool.Ellipse),
            MainMenu.MarkTool(marks, UiText.Get("_Arrow"), AnnotationTool.Arrow),
            MainMenu.MarkTool(marks, UiText.Get("_Line"), AnnotationTool.Line),
            MenuEntry.Submenu(UiText.Get("_More Marking Tools"), true, [.. ToolMenus.MoreMarks(marks)]),
            MenuEntry.Separator(),
            Item("Filter and _Sort the Notes…", marks.ShowFilterCommand).WithIcon("Filter"),
            Item("Show Every Note Again", marks.ClearFilterCommand).WithIcon("Note"),
            MenuEntry.Separator(),
            MenuEntry.Item(UiText.Get(UndoKey), null, marks.UndoCommand, null, marks.CanUndo),
            MenuEntry.Item(UiText.Get(RedoKey), null, marks.RedoCommand, null, marks.CanRedo),
        ];
    }

    /// <summary>Lists filling and signing, and the form fields that can be added and changed.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Forms(DocumentTabViewModel tab)
    {
        var entries = ToolMenus.Signing(tab);
        entries.Add(MenuEntry.Separator());
        entries.AddRange(ToolMenus.FormFields(tab));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("_Load Answers from a File…"), null, tab.ImportFormDataCommand, null, true).WithIcon("Open"));
        entries.Add(MenuEntry.Item(UiText.Get("Save the Answers as _FDF…"), null, tab.ExportFormDataCommand, FormDataFormat.Fdf, true).WithIcon("Save"));
        entries.Add(MenuEntry.Item(UiText.Get("Save the Answers as _XFDF…"), null, tab.ExportFormDataCommand, FormDataFormat.Xfdf, true).WithIcon("Save"));
        return entries;
    }

    /// <summary>Lists what the pointer does, measuring, taking a picture of an area, recognising text, changing the page's content, and signatures and tags.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Tools(DocumentTabViewModel tab)
    {
        List<MenuEntry> entries = [];
        MainMenu.AddPointerTools(entries, tab);
        entries.Add(MenuEntry.Separator());
        entries.AddRange(ToolMenus.Measuring(tab));
        entries.Add(MenuEntry.Radio(UiText.Get("_Snapshot"), tab.PageTools.ToggleSnapshotToolCommand, null, tab.PageTools.IsSnapshotTool));
        entries.Add(Item("Recognise _Text", tab.TextRecognition.RecognizeCommand));
        entries.Add(Item("Recognise Text in Another _Language…", tab.TextRecognition.ChooseLanguageCommand));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Submenu(UiText.Get("_Edit the Page's Own Content"), true, [.. ToolMenus.ContentEditing(tab)]));
        entries.Add(MenuEntry.Submenu(UiText.Get("Signatures and Ta_gs"), true, [.. ToolMenus.Trust(tab)]));
        entries.Add(Item("Show Every _Layer as the Document Sets It", tab.Layers.ResetCommand));
        return entries;
    }

    /// <summary>Lists new windows, moving between tabs, and every open tab, with the one shown ticked.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="actions">The commands of the window's own controls, one of which shows a tab.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Window(MainViewModel main, ChromeActions actions)
    {
        var several = main.Tabs.Count > 1;
        List<MenuEntry> entries =
        [
            MenuEntry.Item(UiText.Get("_New Window"), "Ctrl+N", main.NewWindowCommand, null, true),
            MenuEntry.Separator(),
            MenuEntry.Item(UiText.Get("_Next Tab"), "Ctrl+Tab", main.NextTabCommand, null, several),
            MenuEntry.Item(UiText.Get("_Previous Tab"), null, main.PreviousTabCommand, null, several),
            MenuEntry.Item(UiText.Get("_Go to a Document…"), "Ctrl+Shift+A", main.ShowTabFinderCommand, null, several),
        ];
        if (main.Tabs.Count == 0)
        {
            return entries;
        }

        entries.Add(MenuEntry.Separator());
        foreach (var open in main.Tabs)
        {
            entries.Add(MenuEntry.Radio(open.Title, actions.SelectTab, open, ReferenceEquals(open, main.SelectedTab)));
        }

        return entries;
    }

    /// <summary>Lists the help there is: what the app is and the licences of what it is made with.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Help(MainViewModel main) =>
    [
        MenuEntry.Item(UiText.Get("About and _Licences…"), null, main.LicencesCommand, null, true),
    ];

    /// <summary>Adds what the File menu does with the open document: saving, printing, its folder and closing tabs.</summary>
    /// <param name="entries">The menu being built.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    private static void AddDocumentFile(List<MenuEntry> entries, MainViewModel main, DocumentTabViewModel tab)
    {
        var loaded = tab.IsLoaded;
        ICommand folder = main.ShowInFolderCommand;
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("_Save"), "Ctrl+S", tab.SaveCommand, null, tab.HasUnsavedChanges));
        entries.Add(MenuEntry.Item(UiText.Get("Save _As…"), null, tab.SaveAsCommand, null, loaded));
        entries.Add(MenuEntry.Submenu(UiText.Get("Save a Cop_y"), loaded, loaded ? [.. ToolMenus.Copies(tab)] : []));
        entries.Add(MenuEntry.Submenu(UiText.Get("E_xport the Text"), loaded, [.. TextExports(tab)]));
        entries.Add(MenuEntry.Item(UiText.Get("Re_load"), null, tab.ReloadCommand, null, loaded));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("_Print…"), "Ctrl+P", tab.Printing.PrintCommand, null, loaded));
        entries.Add(MenuEntry.Item(UiText.Get("Print with the System _Dialog…"), null, tab.Printing.PrintWithSystemDialogCommand, null, loaded));
        entries.Add(MenuEntry.Separator());
        entries.Add(MenuEntry.Item(UiText.Get("Show the Document in Its F_older"), null, folder, null, folder.CanExecute(null)));
        entries.Add(MenuEntry.Item(UiText.Get("_Close Tab"), "Ctrl+W", main.CloseTabCommand, tab, true));
        entries.Add(MenuEntry.Item(UiText.Get("Close _Other Tabs"), null, main.CloseOtherTabsCommand, tab, main.Tabs.Count > 1));
        entries.Add(MenuEntry.Item(UiText.Get("Close _All Tabs"), null, main.CloseAllTabsCommand, null, true));
    }

    /// <summary>Lists the kinds of file the document's text can be saved as.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> TextExports(DocumentTabViewModel tab) =>
    [
        MenuEntry.Item(UiText.Get("As _Plain Text…"), null, tab.ExportTextCommand, DocumentTextFormat.PlainText, true).WithIcon("Text"),
        MenuEntry.Item(UiText.Get("As _Markdown…"), null, tab.ExportTextCommand, DocumentTextFormat.Markdown, true).WithIcon("Text"),
        MenuEntry.Item(UiText.Get("As _HTML…"), null, tab.ExportTextCommand, DocumentTextFormat.Html, true).WithIcon("Text"),
    ];

    /// <summary>Adds undo and redo, which act on what Ctrl+Z and Ctrl+Shift+Z act on now.</summary>
    /// <param name="entries">The menu being built.</param>
    /// <param name="main">The main view model.</param>
    private static void AddUndo(List<MenuEntry> entries, MainViewModel main)
    {
        if (Shortcuts.UndoOf(main) is not { } undo || Shortcuts.RedoOf(main) is not { } redo)
        {
            return;
        }

        entries.Add(MenuEntry.Item(UiText.Get(UndoKey), "Ctrl+Z", undo, null, ((ICommand)undo).CanExecute(null)));
        entries.Add(MenuEntry.Item(UiText.Get(RedoKey), "Ctrl+Shift+Z", redo, null, ((ICommand)redo).CanExecute(null)));
        entries.Add(MenuEntry.Separator());
    }

    /// <summary>Makes an entry that runs a command, greyed while the command cannot run.</summary>
    /// <param name="header">The entry's text in English, which is looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string header, ICommand command) => MenuEntry.Item(UiText.Get(header), null, command, null, command.CanExecute(null));
}
