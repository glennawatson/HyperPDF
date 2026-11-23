// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.Viewer.Chrome;

/// <summary>The keys that run a command wherever the keyboard is in the window. Each one runs the same view model command as its menu entry.</summary>
internal static class Shortcuts
{
    /// <summary>Control alone.</summary>
    private const ViewerModifiers Ctrl = ViewerModifiers.Control;

    /// <summary>Control with Shift.</summary>
    private const ViewerModifiers CtrlShift = ViewerModifiers.Control | ViewerModifiers.Shift;

    /// <summary>No modifier key.</summary>
    private const ViewerModifiers Plain = ViewerModifiers.None;

    /// <summary>The keys, in no order: a key with given modifiers appears once.</summary>
    private static readonly Shortcut[] All =
    [
        new(ViewerKey.O, Ctrl, static m => m.OpenCommand),
        new(ViewerKey.N, Ctrl, static m => m.NewWindowCommand),
        new(ViewerKey.W, Ctrl, static m => m.SelectedTab is null ? null : m.CloseTabCommand) { Parameter = static m => m.SelectedTab },
        new(ViewerKey.F4, Ctrl, static m => m.SelectedTab is null ? null : m.CloseTabCommand) { Parameter = static m => m.SelectedTab },
        new(ViewerKey.Tab, Ctrl, static m => m.NextTabCommand),
        new(ViewerKey.Tab, CtrlShift, static m => m.PreviousTabCommand),
        new(ViewerKey.PageDown, Ctrl, static m => m.NextTabCommand),
        new(ViewerKey.PageUp, Ctrl, static m => m.PreviousTabCommand),
        new(ViewerKey.T, CtrlShift, static m => m.ReopenClosedTabCommand),
        new(ViewerKey.F, CtrlShift, static m => m.SearchFolderCommand),
        new(ViewerKey.A, CtrlShift, static m => m.ShowTabFinderCommand),
        new(ViewerKey.I, Ctrl, static m => m.TogglePageToneCommand),
        new(ViewerKey.Comma, Ctrl, static m => m.PreferencesCommand),
        new(ViewerKey.Enter, ViewerModifiers.Alt, static m => m.SelectedTab is null ? null : m.PropertiesCommand),
        new(ViewerKey.S, Ctrl, static m => m.SelectedTab?.SaveCommand),
        new(ViewerKey.S, CtrlShift, static m => m.SelectedTab?.SaveAsCommand),
        new(ViewerKey.P, Ctrl, static m => m.SelectedTab?.Printing.PrintCommand),
        new(ViewerKey.P, CtrlShift, static m => m.SelectedTab?.Printing.PrintWithSystemDialogCommand),
        new(ViewerKey.Z, Ctrl, UndoOf),
        new(ViewerKey.Y, Ctrl, RedoOf),
        new(ViewerKey.Z, CtrlShift, RedoOf),
        new(ViewerKey.E, Ctrl, static m => m.SelectedTab?.ContentEdit.ToggleEditingCommand),
        new(ViewerKey.F, Ctrl, static m => m.SelectedTab?.FindCommand),
        new(ViewerKey.F3, Plain, static m => m.SelectedTab?.Search.NextCommand),
        new(ViewerKey.F3, ViewerModifiers.Shift, static m => m.SelectedTab?.Search.PreviousCommand),
        new(ViewerKey.G, Ctrl, static m => m.SelectedTab?.Search.NextCommand),
        new(ViewerKey.G, CtrlShift, static m => m.SelectedTab?.Search.PreviousCommand),
        new(ViewerKey.Plus, Ctrl, static m => m.SelectedTab?.ZoomInCommand),
        new(ViewerKey.Minus, Ctrl, static m => m.SelectedTab?.ZoomOutCommand),
        new(ViewerKey.Digit0, Ctrl, static m => m.SelectedTab?.ZoomResetCommand),
        new(ViewerKey.Digit1, Ctrl, static m => m.SelectedTab?.FitPageCommand),
        new(ViewerKey.Digit2, Ctrl, static m => m.SelectedTab?.FitWidthCommand),
        new(ViewerKey.Digit4, Ctrl, static m => m.SelectedTab?.FocusMode.ToggleCommand),
        new(ViewerKey.Left, Ctrl, static m => m.SelectedTab?.RotateLeftCommand),
        new(ViewerKey.Right, Ctrl, static m => m.SelectedTab?.RotateRightCommand),
        new(ViewerKey.Left, ViewerModifiers.Alt, static m => m.SelectedTab?.GoBackCommand),
        new(ViewerKey.Right, ViewerModifiers.Alt, static m => m.SelectedTab?.GoForwardCommand),
        new(ViewerKey.Home, Ctrl, static m => m.SelectedTab?.FirstPageCommand),
        new(ViewerKey.End, Ctrl, static m => m.SelectedTab?.LastPageCommand),
        new(ViewerKey.F9, Plain, static m => m.SelectedTab?.ToggleSidebarCommand),
        new(ViewerKey.F7, Plain, static m => m.SelectedTab?.ToggleCaretModeCommand),
        new(ViewerKey.Y, CtrlShift, static m => m.SelectedTab?.ReadAloud.ToggleCommand),
        new(ViewerKey.V, CtrlShift, static m => m.SelectedTab?.ReadAloud.ReadThisPageCommand),
        new(ViewerKey.B, CtrlShift, static m => m.SelectedTab?.ReadAloud.ReadToEndCommand),
        new(ViewerKey.C, CtrlShift, static m => m.SelectedTab?.ReadAloud.PlayPauseCommand),
        new(ViewerKey.E, CtrlShift, static m => m.SelectedTab?.ReadAloud.CloseCommand),
        new(ViewerKey.Period, CtrlShift, static m => m.SelectedTab?.ReadAloud.NextCommand),
        new(ViewerKey.Comma, CtrlShift, static m => m.SelectedTab?.ReadAloud.PreviousCommand),
        new(ViewerKey.M, CtrlShift, static m => m.SelectedTab?.Measure.ToggleCommand),
        new(ViewerKey.F5, Plain, static m => m.SelectedTab?.ReloadCommand),
        new(ViewerKey.F5, ViewerModifiers.Shift, static m => m.SelectedTab?.PresentCommand),
        new(ViewerKey.H, Ctrl, static m => m.SelectedTab?.ReadModeCommand),
        new(ViewerKey.H, CtrlShift, static m => m.SelectedTab?.PageTools.ToggleAutoScrollCommand),
        new(ViewerKey.Escape, Plain, LeaveFullView),
        new(ViewerKey.Up, CtrlShift, MoveSelectionUp),
        new(ViewerKey.Down, CtrlShift, MoveSelectionDown),
        new(ViewerKey.Right, CtrlShift, NestSelection),
        new(ViewerKey.Left, CtrlShift, UnnestSelection),
        new(ViewerKey.Delete, Plain, RemoveSelection),
        new(ViewerKey.F2, Plain, RenameSelection),
    ];

    /// <summary>Determines whether a key with given modifiers is in the table, whether or not it has anything to act on now.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key is bound.</returns>
    internal static bool IsBound(ViewerKey key, ViewerModifiers modifiers)
    {
        foreach (var shortcut in All)
        {
            if (shortcut.Key == key && shortcut.Modifiers == modifiers)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds what a key runs.</summary>
    /// <param name="main">The main view model.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns>The command and its value as a menu entry, or <see langword="null"/> when the key runs nothing now.</returns>
    internal static MenuEntry? Find(MainViewModel main, ViewerKey key, ViewerModifiers modifiers)
    {
        foreach (var shortcut in All)
        {
            if (shortcut.Key == key && shortcut.Modifiers == modifiers && shortcut.Command(main) is { } command)
            {
                return MenuEntry.Item(string.Empty, null, command, shortcut.Parameter?.Invoke(main), true);
            }
        }

        return null;
    }

    /// <summary>Gets what undo runs: the page edit's undo while page objects are being edited, and the annotations' otherwise.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> with no tab.</returns>
    internal static ReactiveCommand<RxVoid, RxVoid>? UndoOf(MainViewModel main) =>
        main.SelectedTab is { ContentEdit.IsEditing: true } tab ? tab.Pages.UndoCommand : main.SelectedTab?.Annotations.UndoCommand;

    /// <summary>Gets what redo runs: the page edit's redo while page objects are being edited, and the annotations' otherwise.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> with no tab.</returns>
    internal static ReactiveCommand<RxVoid, RxVoid>? RedoOf(MainViewModel main) =>
        main.SelectedTab is { ContentEdit.IsEditing: true } tab ? tab.Pages.RedoCommand : main.SelectedTab?.Annotations.RedoCommand;

    /// <summary>Gets what Ctrl+Shift+Up runs: moves the chosen bookmark, tag or page one place up, in whichever list the side panel shows.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when the side panel shows no list that can be put in order.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? MoveSelectionUp(MainViewModel main) => main.SelectedTab is { SidebarVisible: true } tab
        ? tab.SidebarMode switch
        {
            SidebarMode.Outline => tab.OutlineEditing.MoveUpCommand,
            SidebarMode.Tags => tab.Tags.MoveUpCommand,
            SidebarMode.Thumbnails when !tab.ContentEdit.IsEditing => tab.Pages.MoveEarlierCommand,
            _ => null,
        }
        : null;

    /// <summary>Gets what Ctrl+Shift+Down runs: moves the chosen bookmark, tag or page one place down.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when the side panel shows no list that can be put in order.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? MoveSelectionDown(MainViewModel main) => main.SelectedTab is { SidebarVisible: true } tab
        ? tab.SidebarMode switch
        {
            SidebarMode.Outline => tab.OutlineEditing.MoveDownCommand,
            SidebarMode.Tags => tab.Tags.MoveDownCommand,
            SidebarMode.Thumbnails when !tab.ContentEdit.IsEditing => tab.Pages.MoveLaterCommand,
            _ => null,
        }
        : null;

    /// <summary>Gets what Ctrl+Shift+Right runs: puts the chosen bookmark or tag inside the one above it.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when the side panel shows no tree.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? NestSelection(MainViewModel main) => main.SelectedTab is { SidebarVisible: true } tab
        ? tab.SidebarMode switch
        {
            SidebarMode.Outline => tab.OutlineEditing.IndentCommand,
            SidebarMode.Tags => tab.Tags.NestCommand,
            _ => null,
        }
        : null;

    /// <summary>Gets what Ctrl+Shift+Left runs: brings the chosen bookmark or tag out of the one it is inside.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when the side panel shows no tree.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? UnnestSelection(MainViewModel main) => main.SelectedTab is { SidebarVisible: true } tab
        ? tab.SidebarMode switch
        {
            SidebarMode.Outline => tab.OutlineEditing.OutdentCommand,
            SidebarMode.Tags => tab.Tags.UnnestCommand,
            _ => null,
        }
        : null;

    /// <summary>
    /// Gets what Delete runs: removes the chosen bookmark, tag or page. The page's own Delete, which removes a chosen mark or object, comes first
    /// whenever one is chosen, so this runs only when nothing on the page is.
    /// </summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when the key belongs to the page.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? RemoveSelection(MainViewModel main) => main.SelectedTab is { SidebarVisible: true } tab && !PageHasTheKey(tab)
        ? tab.SidebarMode switch
        {
            SidebarMode.Outline => tab.OutlineEditing.RemoveCommand,
            SidebarMode.Tags => tab.Tags.RemoveCommand,
            SidebarMode.Thumbnails => tab.Pages.DeleteCommand,
            _ => null,
        }
        : null;

    /// <summary>Gets what F2 runs: renames the chosen bookmark, when nothing on the page is chosen to be edited with the key.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when the key belongs to the page.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? RenameSelection(MainViewModel main) =>
        main.SelectedTab is { SidebarVisible: true, SidebarMode: SidebarMode.Outline } tab && !PageHasTheKey(tab) ? tab.OutlineEditing.RenameCommand : null;

    /// <summary>Determines whether the page has a chosen mark or object that Delete and F2 act on, or is being edited.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns><see langword="true"/> when the page takes the key.</returns>
    private static bool PageHasTheKey(DocumentTabViewModel tab) => tab.Annotations.Selected is not null || tab.ContentEdit.IsEditing;

    /// <summary>Gets what Escape runs: it ends a presentation or reading mode, whose bars are hidden, and does nothing otherwise.</summary>
    /// <param name="main">The main view model.</param>
    /// <returns>The command, or <see langword="null"/> when neither is on.</returns>
    private static ReactiveCommand<RxVoid, RxVoid>? LeaveFullView(MainViewModel main)
    {
        if (main.SelectedTab is not { } tab)
        {
            return null;
        }

        if (tab.IsPresenting)
        {
            return tab.StopPresentingCommand;
        }

        return tab.IsReading ? tab.ReadModeCommand : null;
    }
}
