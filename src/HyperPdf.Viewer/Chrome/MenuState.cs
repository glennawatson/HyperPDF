// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Dialogs;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a menu shows: the entries on screen, the menus above them, the scroll offset and the entry the keyboard is on.</summary>
/// <param name="Shown">The entries shown.</param>
/// <param name="Parents">The menus above the one shown, the nearest last.</param>
/// <param name="Cursor">The entry the keyboard is on, or less than <see cref="MenuOverlays.BackIndex"/> for none.</param>
/// <param name="First">The first entry scrolled into view.</param>
/// <param name="FollowCursor">Whether the scroll offset follows the keyboard cursor rather than the mouse wheel.</param>
/// <param name="Chosen">The entry that was chosen, or <see langword="null"/> while none was.</param>
/// <param name="Done">Whether the menu was closed, by a choice, by going back from the top menu, or by being sent away.</param>
/// <param name="Switches">Whether the menu is one of the menu bar's, which Left and Right leave for the menu beside it.</param>
/// <param name="Step">The menu asked for when it closed: 1 for the one to the right, -1 for the one to the left, 0 for none.</param>
[DebuggerDisplay("MenuState: {Shown.Length} entries, cursor {Cursor}, done {Done}")]
internal sealed record MenuState(MenuEntry[] Shown, MenuEntry[][] Parents, int Cursor, int First, bool FollowCursor, MenuEntry? Chosen, bool Done, bool Switches, int Step);
