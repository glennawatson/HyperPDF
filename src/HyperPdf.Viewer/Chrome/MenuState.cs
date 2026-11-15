// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.ViewModels.Dialogs;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a menu shows: the entries on screen, the menus above them, and the entry the keyboard is on.</summary>
/// <param name="Shown">The entries shown.</param>
/// <param name="Parents">The menus above the one shown, the nearest last.</param>
/// <param name="Cursor">The entry the keyboard is on, or less than <see cref="MenuOverlays.BackIndex"/> for none.</param>
/// <param name="Chosen">The entry that was chosen, or <see langword="null"/> while none was.</param>
/// <param name="Done">Whether the menu was closed, by a choice, by going back from the top menu, or by being sent away.</param>
[DebuggerDisplay("MenuState: {Shown.Count} entries, cursor {Cursor}, done {Done}")]
internal sealed record MenuState(IReadOnlyList<MenuEntry> Shown, IReadOnlyList<MenuEntry>[] Parents, int Cursor, MenuEntry? Chosen, bool Done);
