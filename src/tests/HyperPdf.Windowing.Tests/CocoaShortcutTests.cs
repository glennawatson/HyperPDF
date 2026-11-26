// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Head.MacOS.Cocoa;

namespace HyperPdf.Windowing.Tests;

/// <summary>Menu item shortcuts as the macOS menu bar shows them. The tests skip on other operating systems.</summary>
public sealed class CocoaShortcutTests
{
    /// <summary>The Command key's modifier flag.</summary>
    private const ulong CommandKey = 1 << 20;

    /// <summary>The Shift key's modifier flag.</summary>
    private const ulong ShiftKey = 1 << 17;

    /// <summary>An item with no shortcut has an empty key, never a missing one, because AppKit is handed the key as a string.</summary>
    /// <param name="gesture">The shortcut as the viewer writes it.</param>
    /// <returns>A task that completes when the test finishes.</returns>
    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("Ctrl+Insert")]
    public async Task NoShortcutHasAnEmptyKey(string? gesture)
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("The Cocoa head only builds menus on macOS.");
            return;
        }

        var shortcut = CocoaShortcut.From(gesture);

        await Assert.That(shortcut).IsEqualTo(CocoaShortcut.None);
        await Assert.That(shortcut.Key).IsEqualTo(string.Empty);
    }

    /// <summary>A Control shortcut shows with Command, and its letter in lower case.</summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Test]
    public async Task ControlShortcutShowsWithCommand()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Skip.Test("The Cocoa head only builds menus on macOS.");
            return;
        }

        var shortcut = CocoaShortcut.From("Ctrl+Shift+Z");

        await Assert.That(shortcut.Key).IsEqualTo("z");
        await Assert.That(shortcut.Modifiers).IsEqualTo(CommandKey | ShiftKey);
    }
}
