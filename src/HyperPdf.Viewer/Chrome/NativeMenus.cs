// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Turns the entries of the menu bar's menus into the entries of a menu bar the operating system draws.</summary>
internal static class NativeMenus
{
    /// <summary>Hands the menus to a window whose operating system draws the menu bar.</summary>
    /// <param name="native">The window.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Show(INativeMenuBar native, MainViewModel main, ChromeActions actions) =>
        native.SetMenus(
            MenuBar.Names(),
            index => Build(index, main, actions),
            new(UiText.Get("Hide HyperPDF"), UiText.Get("Hide Others"), UiText.Get("Show All"), UiText.Get("Quit HyperPDF")));

    /// <summary>Makes the entries of one menu for the state the window is in, as the menu bar the viewer draws would show them.</summary>
    /// <param name="index">The menu's place on the bar.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="actions">The commands of the window's own controls.</param>
    /// <returns>The entries; none for a menu that needs a document when none is open.</returns>
    internal static NativeMenuItem[] Build(int index, MainViewModel main, ChromeActions actions) =>
        MenuBar.CanOpen(main, index) ? Convert(MenuBar.Build(index, main, actions)) : [];

    /// <summary>Turns a list of entries into native entries.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The native entries, in the same order.</returns>
    private static NativeMenuItem[] Convert(IReadOnlyList<MenuEntry> entries)
    {
        var items = new NativeMenuItem[entries.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = Convert(entries[i]);
        }

        return items;
    }

    /// <summary>Turns one entry into a native entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The native entry.</returns>
    private static NativeMenuItem Convert(MenuEntry entry)
    {
        if (entry.Kind == MenuEntryKind.Separator)
        {
            return NativeMenuItem.Separator;
        }

        var title = ChromeText.WithoutAccessMark(entry.Header);
        if (entry.Kind == MenuEntryKind.Submenu)
        {
            return new(title, null, entry.IsEnabled, false, false, Convert(entry.Children), null);
        }

        var run = entry.Command is { } command ? Runner(command, entry.Parameter) : null;
        return new(title, entry.Gesture, entry.IsEnabled, entry.IsChecked, false, [], run);
    }

    /// <summary>Makes what choosing an entry does: runs its command when the command can run.</summary>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The command parameter.</param>
    /// <returns>The action.</returns>
    private static Action Runner(ICommand command, object? parameter) => () =>
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    };
}
