// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;

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
        new(ViewerKey.Z, Ctrl, static m => m.SelectedTab?.Annotations.UndoCommand),
        new(ViewerKey.Y, Ctrl, static m => m.SelectedTab?.Annotations.RedoCommand),
        new(ViewerKey.Z, CtrlShift, static m => m.SelectedTab?.Annotations.RedoCommand),
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
    ];

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
}
