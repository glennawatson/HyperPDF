// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Viewer.Chrome;

namespace HyperPdf.Windowing.Tests;

/// <summary>Checks that the window's shortcut keys include the ones the Avalonia app had.</summary>
public sealed class ShortcutTableTests
{
    /// <summary>The shortcuts for modes and reloading, which the first native window left out, are bound.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(ViewerKey.F5, ViewerModifiers.None)]
    [Arguments(ViewerKey.F5, ViewerModifiers.Shift)]
    [Arguments(ViewerKey.H, ViewerModifiers.Control)]
    [Arguments(ViewerKey.H, ViewerModifiers.Control | ViewerModifiers.Shift)]
    [Arguments(ViewerKey.M, ViewerModifiers.Control | ViewerModifiers.Shift)]
    [Arguments(ViewerKey.Escape, ViewerModifiers.None)]
    [Arguments(ViewerKey.Delete, ViewerModifiers.None)]
    [Arguments(ViewerKey.F2, ViewerModifiers.None)]
    [Arguments(ViewerKey.Up, ViewerModifiers.Control | ViewerModifiers.Shift)]
    [Arguments(ViewerKey.Down, ViewerModifiers.Control | ViewerModifiers.Shift)]
    [Arguments(ViewerKey.Left, ViewerModifiers.Control | ViewerModifiers.Shift)]
    [Arguments(ViewerKey.Right, ViewerModifiers.Control | ViewerModifiers.Shift)]
    public async Task RestoredShortcutsAreBound(ViewerKey key, ViewerModifiers modifiers) =>
        await Assert.That(Shortcuts.IsBound(key, modifiers)).IsTrue();

    /// <summary>A key nothing is bound to is not in the table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnboundKeyIsNotInTheTable() =>
        await Assert.That(Shortcuts.IsBound(ViewerKey.H, ViewerModifiers.Alt)).IsFalse();
}
