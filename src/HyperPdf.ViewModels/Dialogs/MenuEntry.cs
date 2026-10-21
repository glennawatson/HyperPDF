// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>One entry of a context menu, worded and wired by the view models; a render head only draws it.</summary>
/// <param name="Kind">What kind of entry it is.</param>
/// <param name="Header">The words, with an underscore before the access key.</param>
/// <param name="Gesture">The shortcut shown, such as "Ctrl+C", or <see langword="null"/>.</param>
/// <param name="Command">The command it runs, or <see langword="null"/>.</param>
/// <param name="Parameter">The command parameter, or <see langword="null"/>.</param>
/// <param name="IsEnabled">Whether it can be chosen.</param>
/// <param name="Swatch">A colour shown beside the words as 0xRRGGBB, or <see langword="null"/>.</param>
/// <param name="IsChecked">Whether a radio item is the ticked one.</param>
/// <param name="Children">The entries of a submenu.</param>
[DebuggerDisplay("MenuEntry: {Kind} {Header}")]
public sealed record MenuEntry(
    MenuEntryKind Kind,
    string Header,
    string? Gesture,
    ICommand? Command,
    object? Parameter,
    bool IsEnabled,
    uint? Swatch,
    bool IsChecked,
    IReadOnlyList<MenuEntry> Children)
{
    /// <summary>Creates a line between groups of items.</summary>
    /// <returns>The entry.</returns>
    public static MenuEntry Separator() => new(MenuEntryKind.Separator, string.Empty, null, null, null, true, null, false, []);

    /// <summary>Creates an item that runs a command.</summary>
    /// <param name="header">The words, with an underscore before the access key.</param>
    /// <param name="gesture">The shortcut shown, or <see langword="null"/>.</param>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The command parameter, or <see langword="null"/>.</param>
    /// <param name="isEnabled">Whether it can be chosen.</param>
    /// <returns>The entry.</returns>
    public static MenuEntry Item(string header, string? gesture, ICommand command, object? parameter, bool isEnabled) =>
        new(MenuEntryKind.Item, header, gesture, command, parameter, isEnabled, null, false, []);

    /// <summary>Creates an item that names the thing the menu is about and cannot be chosen.</summary>
    /// <param name="header">The words.</param>
    /// <returns>The entry.</returns>
    public static MenuEntry Title(string header) =>
        new(MenuEntryKind.Item, header, null, null, null, false, null, false, []);

    /// <summary>Creates an item that opens a menu of its own.</summary>
    /// <param name="header">The words, with an underscore before the access key.</param>
    /// <param name="isEnabled">Whether it can be opened.</param>
    /// <param name="children">The entries of the menu.</param>
    /// <returns>The entry.</returns>
    public static MenuEntry Submenu(string header, bool isEnabled, IReadOnlyList<MenuEntry> children) =>
        new(MenuEntryKind.Submenu, header, null, null, null, isEnabled, null, false, children);

    /// <summary>Creates an item in a group of which one is ticked.</summary>
    /// <param name="header">The words.</param>
    /// <param name="command">The command.</param>
    /// <param name="parameter">The command parameter.</param>
    /// <param name="isChecked">Whether it is the ticked one.</param>
    /// <returns>The entry.</returns>
    public static MenuEntry Radio(string header, ICommand command, object? parameter, bool isChecked) =>
        new(MenuEntryKind.RadioItem, header, null, command, parameter, true, null, isChecked, []);

    /// <summary>Gets a copy of an item that shows a colour beside its words.</summary>
    /// <param name="swatch">The colour as 0xRRGGBB.</param>
    /// <returns>The entry.</returns>
    public MenuEntry WithSwatch(uint swatch) => this with { Swatch = swatch };
}
