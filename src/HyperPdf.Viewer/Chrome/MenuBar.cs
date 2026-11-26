// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The menu bar under the tabs: File, Edit, View, Document, Comment, Forms, Tools, Window and Help. A menu opens when its name is
/// pressed, with Alt and the underlined letter of its name, or with F10 for File. Left and Right move to the menu beside it. A menu
/// never opens because the pointer passes over the bar, so nothing appears that was not asked for.
/// </summary>
internal static class MenuBar
{
    /// <summary>The place of the File menu.</summary>
    internal const int FileMenu = 0;

    /// <summary>The place of the Edit menu.</summary>
    private const int EditMenu = 1;

    /// <summary>The place of the View menu.</summary>
    private const int ViewMenu = 2;

    /// <summary>The place of the Document menu.</summary>
    private const int DocumentMenu = 3;

    /// <summary>The place of the Comment menu.</summary>
    private const int CommentMenu = 4;

    /// <summary>The place of the Forms menu.</summary>
    private const int FormsMenu = 5;

    /// <summary>The place of the Window menu.</summary>
    private const int WindowMenu = 7;

    /// <summary>The place of the Help menu.</summary>
    private const int HelpMenu = 8;

    /// <summary>The mark before a menu name's access letter.</summary>
    private const char AccessMark = '_';

    /// <summary>The menus' names in English, in their order on the bar, with the underline before each one's access letter.</summary>
    private static readonly string[] Titles = ["_File", "_Edit", "_View", "_Document", "_Comment", "F_orms", "_Tools", "_Window", "_Help"];

    /// <summary>Each menu's place, boxed once, as the value its name on the bar passes when pressed.</summary>
    private static readonly object[] Places = [0, 1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>The letter of each letter key, to match against a menu name's access letter.</summary>
    private static readonly Dictionary<ViewerKey, char> Letters = new()
    {
        [ViewerKey.A] = 'A',
        [ViewerKey.B] = 'B',
        [ViewerKey.C] = 'C',
        [ViewerKey.D] = 'D',
        [ViewerKey.E] = 'E',
        [ViewerKey.F] = 'F',
        [ViewerKey.G] = 'G',
        [ViewerKey.H] = 'H',
        [ViewerKey.I] = 'I',
        [ViewerKey.M] = 'M',
        [ViewerKey.N] = 'N',
        [ViewerKey.O] = 'O',
        [ViewerKey.P] = 'P',
        [ViewerKey.S] = 'S',
        [ViewerKey.T] = 'T',
        [ViewerKey.V] = 'V',
        [ViewerKey.W] = 'W',
        [ViewerKey.Y] = 'Y',
        [ViewerKey.Z] = 'Z',
    };

    /// <summary>
    /// Adds the bar. Each menu's name is a button that opens it. A menu that needs a document is faded until one is open, and keeps
    /// its place, so the names never move.
    /// </summary>
    /// <param name="items">Receives the controls.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="look">The text measurer and the sizes.</param>
    /// <param name="top">The top of the bar.</param>
    /// <param name="width">The window width.</param>
    /// <param name="open">The command that opens the menu whose place it is given.</param>
    /// <returns>The height the bar takes, which is more than one line when the window is too narrow for every name.</returns>
    internal static int Add(List<ChromeItem> items, MainViewModel main, ChromeLook look, int top, int width, ICommand open)
    {
        var metrics = look.Metrics;
        var panel = items.Count;
        items.Add(new(ChromeItemKind.Panel, null, 0, string.Empty, default, true, false));
        var row = new ChromeRow(items, look.Runs, metrics, metrics.Gap, top + metrics.Gap, metrics.RowHeight - metrics.Gap - metrics.Gap) { Limit = width - metrics.Gap };
        for (var i = 0; i < Titles.Length; i++)
        {
            row = row.Button(ChromeText.WithoutAccessMark(UiText.Get(Titles[i])), open, CanOpen(main, i));
            items[^1] = items[^1] with { Index = i, Parameter = Places[i], Trait = ChromeItemTrait.OpensMore };
        }

        var height = row.Lines * metrics.RowHeight;
        items[panel] = items[panel] with { Bounds = new(0, top, width, height) };
        return height;
    }

    /// <summary>Gets the menus' names in the language shown, without access marks, for a menu bar the operating system draws.</summary>
    /// <returns>The names, in their order on the bar.</returns>
    internal static string[] Names()
    {
        var names = new string[Titles.Length];
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = ChromeText.WithoutAccessMark(UiText.Get(Titles[i]));
        }

        return names;
    }

    /// <summary>Tells whether a menu can open now. File, Edit, Window and Help always can; the others need an open document.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="index">The menu's place on the bar.</param>
    /// <returns><see langword="true"/> when it can open.</returns>
    internal static bool CanOpen(MainViewModel main, int index) =>
        index is FileMenu or EditMenu or WindowMenu or HelpMenu || main.SelectedTab is { IsLoaded: true };

    /// <summary>Builds the entries of a menu for the state the window is in.</summary>
    /// <param name="index">The menu's place on the bar.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The entries, empty for a menu that needs a document when none is open.</returns>
    internal static List<MenuEntry> Build(int index, MainViewModel main, ChromeActions actions) => index switch
    {
        FileMenu => MenuBarMenus.File(main, actions),
        EditMenu => MenuBarMenus.Edit(main),
        WindowMenu => MenuBarMenus.Window(main, actions),
        HelpMenu => MenuBarMenus.Help(main),
        _ => main.SelectedTab is { IsLoaded: true } tab ? ForDocument(index, main, tab, actions) : [],
    };

    /// <summary>Finds the menu beside one, passing over the menus that cannot open now.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="index">The menu's place on the bar.</param>
    /// <param name="step">1 for the menu to the right, -1 for the one to the left.</param>
    /// <returns>The place of the menu beside it, which goes round from the last to the first.</returns>
    internal static int Beside(MainViewModel main, int index, int step)
    {
        var next = index;
        for (var tries = 0; tries < Titles.Length; tries++)
        {
            next = (next + step + Titles.Length) % Titles.Length;
            if (CanOpen(main, next))
            {
                return next;
            }
        }

        return index;
    }

    /// <summary>Finds the menu whose access letter is the letter of a key, as Alt and that letter opens it.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The menu's place on the bar, or -1 when no menu's name has the key's letter as its access letter.</returns>
    internal static int ForKey(ViewerKey key)
    {
        if (!Letters.TryGetValue(key, out var letter))
        {
            return -1;
        }

        // The access letter is the one after the underline in the name in the language shown, so it follows the translation.
        for (var i = 0; i < Titles.Length; i++)
        {
            var title = UiText.Get(Titles[i]);
            var mark = title.IndexOf(AccessMark, StringComparison.Ordinal);
            if (mark >= 0 && mark + 1 < title.Length && char.ToUpperInvariant(title[mark + 1]) == letter)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds where a menu opens: under its name on the bar.</summary>
    /// <param name="bars">The window's controls.</param>
    /// <param name="open">The command the names run.</param>
    /// <param name="index">The menu's place on the bar.</param>
    /// <returns>The bottom left corner of its name, or the top left corner of the window when the bar is not shown.</returns>
    internal static ScreenPoint Under(List<ChromeItem> bars, ICommand open, int index)
    {
        foreach (var item in bars)
        {
            if (ReferenceEquals(item.Command, open) && item.Index == index)
            {
                return new((int)item.Bounds.X, (int)item.Bounds.Bottom);
            }
        }

        return default;
    }

    /// <summary>Builds the entries of a menu that is about the open document.</summary>
    /// <param name="index">The menu's place on the bar.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="tab">The tab shown.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The entries.</returns>
    private static List<MenuEntry> ForDocument(int index, MainViewModel main, DocumentTabViewModel tab, ChromeActions actions) => index switch
    {
        ViewMenu => MenuBarMenus.View(main, tab),
        DocumentMenu => MenuBarMenus.Document(main, tab),
        CommentMenu => MenuBarMenus.Comment(tab, actions),
        FormsMenu => MenuBarMenus.Forms(tab),
        _ => MenuBarMenus.Tools(tab),
    };
}
