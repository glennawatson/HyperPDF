// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.ViewModels.Sidebar;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>
/// Builds the menu shown by a right-click on a page in the side panel. It holds everything that can be done to the chosen
/// pages, grouped the same way as the bar that organises pages: choosing, adding, changing, saving and removing.
/// </summary>
public static class PageContextMenus
{
    /// <summary>The name of the icon of an entry that chooses pages: a ticked box.</summary>
    private const string Ticked = "CheckOn";

    /// <summary>The name of the icon of an entry that adds pages.</summary>
    private const string Adds = "Add";

    /// <summary>Lists what can be done to the chosen pages, for the page under the pointer.</summary>
    /// <param name="tab">The tab the page is in.</param>
    /// <param name="page">The page under the pointer.</param>
    /// <returns>The menu items.</returns>
    public static List<MenuEntry> ForPage(DocumentTabViewModel tab, ThumbnailItemViewModel page)
    {
        ArgumentNullException.ThrowIfNull(tab);
        ArgumentNullException.ThrowIfNull(page);
        var pages = tab.Pages;
        var chosen = Array.BinarySearch(pages.SelectedPages, page.PageIndex) >= 0;
        return
        [
            MenuEntry.Item(UiText.Get("Select Only This Page"), null, pages.SelectOnlyCommand, page, true).WithIcon(Ticked),
            MenuEntry.Item(chosen ? UiText.Get("Take This Page out of the Selection") : UiText.Get("Add This Page to the Selection"), null, pages.TogglePageCommand, page, true)
                .WithIcon(chosen ? "CheckOff" : Ticked),
            Item("Select _Every Page", pages.SelectAllCommand, Ticked),
            MenuEntry.Separator(),
            Item("Add a _Blank Page After", pages.InsertBlankPageCommand, Adds),
            Item("_Insert Pages from Files…", pages.InsertFilesCommand, Adds),
            Item("Insert _Pictures as Pages…", pages.InsertImagesCommand, Adds),
            MenuEntry.Separator(),
            Item("Turn _Left", pages.RotateCounterclockwiseCommand, "RotateLeft"),
            Item("Turn _Right", pages.RotateClockwiseCommand, "RotateRight"),
            Item("_Duplicate", pages.DuplicateCommand, "Copy"),
            Item("Move E_arlier", pages.MoveEarlierCommand, "PageUp"),
            Item("Move La_ter", pages.MoveLaterCommand, "PageDown"),
            Item("Re_verse the Order", pages.ReverseCommand, "Reverse"),
            Item("Re_place with Pages from Files…", pages.ReplaceCommand, "Replace"),
            MenuEntry.Separator(),
            Item("E_xtract to a New File…", pages.ExtractCommand, "Save"),
            Item("_Save Each as Its Own File…", pages.SplitPagesCommand, "Split"),
            MenuEntry.Separator(),
            Item("Dele_te", pages.DeleteCommand, "Close"),
            MenuEntry.Separator(),
            MenuEntry.Item(pages.UndoText, null, pages.UndoCommand, null, pages.CanUndo).WithIcon("Undo"),
            MenuEntry.Item(pages.RedoText, null, pages.RedoCommand, null, pages.CanRedo).WithIcon("Redo"),
        ];
    }

    /// <summary>Makes an entry that runs a command with no value, greyed out while the command cannot run.</summary>
    /// <param name="header">The entry's words in English, which are looked up in the language shown.</param>
    /// <param name="command">The command.</param>
    /// <param name="icon">The name of the entry's icon.</param>
    /// <returns>The entry.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MenuEntry Item(string header, ICommand command, string icon) =>
        MenuEntry.Item(UiText.Get(header), null, command, null, command.CanExecute(null)).WithIcon(icon);
}
