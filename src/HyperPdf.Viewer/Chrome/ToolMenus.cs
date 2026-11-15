// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.Core.Layout;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The submenus of the main menu that reach the document's tools: copies, pages, bookmarks, reading aids, signing and the tools
/// that work on the page. Every entry runs a view model command, so a tool works the same from the menu, a key or a screen reader.
/// </summary>
internal static class ToolMenus
{
    /// <summary>The zoom levels offered, in percent.</summary>
    private static readonly string[] ZoomLevels = ["50", "75", "100", "125", "150", "200", "400"];

    /// <summary>Lists the kinds of copy that can be saved.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Copies(DocumentTabViewModel tab)
    {
        var copies = tab.Copies;
        return
        [
            Copy(copies.SaveCopy),
            Copy(copies.Optimize),
            Copy(copies.Archive),
            Copy(copies.Pdf20),
            Copy(copies.Flatten),
            Copy(copies.ExportPages),
            MenuEntry.Separator(),
            Copy(copies.Protect),
            Copy(copies.Unprotect),
            Copy(copies.Redact),
        ];
    }

    /// <summary>Lists what can be done to the pages of the document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Pages(DocumentTabViewModel tab)
    {
        var pages = tab.Pages;
        return
        [
            Item("Turn the Page _Right", pages.RotateClockwiseCommand),
            Item("Turn the Page _Left", pages.RotateCounterclockwiseCommand),
            Item("Move the Page _Earlier", pages.MoveEarlierCommand),
            Item("Move the Page La_ter", pages.MoveLaterCommand),
            Item("_Duplicate the Page", pages.DuplicateCommand),
            MenuEntry.Separator(),
            Item("E_xtract Pages…", pages.ExtractCommand),
            Item("_Insert Pages from Files…", pages.InsertFilesCommand),
            Item("Insert _Pictures as Pages…", pages.InsertImagesCommand),
            Item("_Merge Files…", pages.MergeFilesCommand),
        ];
    }

    /// <summary>Lists what can be done to the bookmarks of the document.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Bookmarks(DocumentTabViewModel tab)
    {
        var bookmarks = tab.OutlineEditing;
        return
        [
            Item("_Add a Bookmark Here…", bookmarks.AddCommand),
            Item("_Rename the Bookmark…", bookmarks.RenameCommand),
            Item("Re_move the Bookmark", bookmarks.RemoveCommand),
            MenuEntry.Separator(),
            Item("Move _Up", bookmarks.MoveUpCommand),
            Item("Move _Down", bookmarks.MoveDownCommand),
            Item("Move _In", bookmarks.IndentCommand),
            Item("Move _Out", bookmarks.OutdentCommand),
        ];
    }

    /// <summary>Lists the reading aids: read aloud, focus mode, reading mode, slow scrolling and text recognition.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Reading(DocumentTabViewModel tab)
    {
        var aloud = tab.ReadAloud;
        var focus = tab.FocusMode;
        var scroll = tab.PageTools;
        var text = tab.TextRecognition;
        return
        [
            MenuEntry.Radio(UiText.Get("Read _Aloud"), aloud.ToggleCommand, null, aloud.IsOpen),
            Item("_Play or Pause", aloud.PlayPauseCommand),
            Item("Read This _Page", aloud.ReadThisPageCommand),
            Item("Read to the _End", aloud.ReadToEndCommand),
            Item("Sentence _Before", aloud.PreviousCommand),
            Item("_Next Sentence", aloud.NextCommand),
            Item("Get a Better _Voice…", aloud.DownloadVoiceCommand),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("_Focus Mode"), focus.ToggleCommand, null, focus.IsOn),
            Item("Larger Focus Text", focus.LargerCommand),
            Item("Smaller Focus Text", focus.SmallerCommand),
            MenuEntry.Radio(UiText.Get("_Reading Mode"), tab.ReadModeCommand, null, tab.IsReading),
            MenuEntry.Radio(UiText.Get("Presen_tation"), tab.PresentCommand, null, tab.IsPresenting),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("_Scroll Slowly by Itself"), scroll.ToggleAutoScrollCommand, null, scroll.IsAutoScrolling),
            Item("Scroll Faster", scroll.FasterAutoScrollCommand),
            Item("Scroll Slower", scroll.SlowerAutoScrollCommand),
            MenuEntry.Separator(),
            Item("Recognise _Text in Scanned Pages", text.RecognizeCommand),
            Item("Choose the Language to Recognise…", text.ChooseLanguageCommand),
            Item("Stop Recognising", text.StopCommand),
            MenuEntry.Separator(),
            Item("Next Place in the Article", tab.Parts.NextPlaceCommand),
            Item("Previous Place in the Article", tab.Parts.PreviousPlaceCommand),
        ];
    }

    /// <summary>Lists the page layouts, the scroll directions and the zoom levels.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries, with the ones in use ticked.</returns>
    internal static List<MenuEntry> Layout(DocumentTabViewModel tab)
    {
        var entries = new List<MenuEntry>
        {
            MenuEntry.Radio(UiText.Get("_One Page Wide"), tab.SetLayoutCommand, nameof(PageLayoutMode.Single), tab.LayoutMode == PageLayoutMode.Single),
            MenuEntry.Radio(UiText.Get("_Two Pages Wide"), tab.SetLayoutCommand, nameof(PageLayoutMode.Dual), tab.LayoutMode == PageLayoutMode.Dual),
            MenuEntry.Radio(UiText.Get("Two Pages Wide with a _Cover"), tab.SetLayoutCommand, nameof(PageLayoutMode.DualCover), tab.LayoutMode == PageLayoutMode.DualCover),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("Scroll _Down the Pages"), tab.SetScrollModeCommand, nameof(ScrollMode.Vertical), tab.ScrollMode == ScrollMode.Vertical),
            MenuEntry.Radio(UiText.Get("Scroll _Across the Pages"), tab.SetScrollModeCommand, nameof(ScrollMode.Horizontal), tab.ScrollMode == ScrollMode.Horizontal),
            MenuEntry.Radio(UiText.Get("_Wrap the Pages"), tab.SetScrollModeCommand, nameof(ScrollMode.Wrapped), tab.ScrollMode == ScrollMode.Wrapped),
            MenuEntry.Separator(),
        };
        foreach (var level in ZoomLevels)
        {
            entries.Add(MenuEntry.Item(UiText.Format("Zoom to {0}%", level), null, tab.SetZoomCommand, level, true));
        }

        return entries;
    }

    /// <summary>Lists the tools that work on the page besides marking: measuring, filling and signing, changing the page's own content and taking a picture of an area.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> PageWork(DocumentTabViewModel tab)
    {
        var measure = tab.Measure;
        var sign = tab.FillAndSign;
        var edit = tab.ContentEdit;
        var marks = tab.Annotations;
        return
        [
            MenuEntry.Radio(UiText.Get("_Measure"), measure.ToggleCommand, null, measure.IsOn),
            Item("Keep the Measurement as a Mark", measure.KeepCommand),
            Item("Clear the Measurement", measure.ClearCommand),
            MenuEntry.Separator(),
            MenuEntry.Radio(UiText.Get("_Fill and Sign"), sign.ToggleCommand, null, sign.IsActive),
            Item("Place My _Signature…", sign.SignatureCommand),
            Item("Place My _Initials…", sign.InitialsCommand),
            Item("Put the Mark Down", sign.PlaceCommand),
            Item("Make the Mark Bigger", sign.BiggerCommand),
            Item("Make the Mark Smaller", sign.SmallerCommand),
            Item("Remove the Mark", sign.RemoveMarkCommand),
            Item("Stop Placing", sign.CancelPlacementCommand),
            MenuEntry.Separator(),
            Item("_Edit the Page's Own Content", edit.ToggleEditingCommand),
            Item("Change the Picked Text…", edit.EditPickedTextCommand),
            Item("Remove the Picked Object", edit.RemovePickedCommand),
            MenuEntry.Separator(),
            Item("Stamp from a _Picture…", marks.PictureStampCommand),
            Item("Stamp with My Own _Words…", marks.CustomStampCommand),
            Item("Make the Typed Text Larger", marks.GrowTextCommand),
            Item("Make the Typed Text Smaller", marks.ShrinkTextCommand),
            Item("Show Every Note Again", marks.ClearFilterCommand),
            MenuEntry.Separator(),
            Item("Take a Picture of an _Area", tab.PageTools.ToggleSnapshotToolCommand),
            Item("Show Every _Layer as the Document Sets It", tab.Layers.ResetCommand),
        ];
    }

    /// <summary>Lists what can be done with signatures and with the document's accessibility tags.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Trust(DocumentTabViewModel tab)
    {
        var signatures = tab.Signatures;
        return
        [
            Item("_Check the Signatures", signatures.CheckCommand),
            Item("_Sign with a Certificate…", signatures.SignWithCertificateCommand),
            Copy(tab.Copies.ExternalSign),
            Copy(tab.Copies.Validation),
            MenuEntry.Separator(),
            Item("Check the _Accessibility Tags", tab.Tags.CheckCommand),
            Item("_Edit the Selected Tag…", tab.Tags.EditCommand),
            Item("Write the Tag Changes to the Document", tab.Tags.ApplyCommand),
        ];
    }

    /// <summary>Lists what the window can do that is not about one document.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The entries.</returns>
    internal static List<MenuEntry> Window(MainViewModel main) =>
    [
        MenuEntry.Item(UiText.Get("_New Window"), "Ctrl+N", main.NewWindowCommand, null, true),
        Item("Convert _Pictures to a PDF…", main.ConvertPicturesCommand),
        Item("Search a _Folder of Documents…", main.SearchFolderCommand),
        Item("Show the Document in Its F_older", main.ShowInFolderCommand),
        Item("Forget the Recent Documents", main.ClearRecentCommand),
    ];

    /// <summary>Makes the entry of one kind of copy, which opens its window.</summary>
    /// <typeparam name="T">The view model of the copy.</typeparam>
    /// <param name="action">The copy.</param>
    /// <returns>The entry, greyed while the copy cannot be made.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Copy<T>(TabCopyAction<T> action)
        where T : class, ICopyWindowRequest =>
        MenuEntry.Item(action.MenuText, null, action.RunCommand, null, action.CanRun);

    /// <summary>Makes an entry that runs a command, greyed while the command cannot run.</summary>
    /// <param name="header">The entry's text in English, which is looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string header, ICommand command) => MenuEntry.Item(UiText.Get(header), null, command, null, command.CanExecute(null));
}
