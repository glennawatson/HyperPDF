// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Windows;

/// <summary>One entry of a menu the operating system draws, as the viewer words and wires it.</summary>
/// <param name="Title">The words, without an access mark; empty for a separator.</param>
/// <param name="Shortcut">The shortcut shown, such as "Ctrl+Shift+S", or <see langword="null"/>.</param>
/// <param name="IsEnabled">Whether it can be chosen.</param>
/// <param name="IsChecked">Whether it shows a tick.</param>
/// <param name="IsSeparator">Whether it is a line between groups of entries.</param>
/// <param name="Children">The entries of its submenu; empty when it has none.</param>
/// <param name="Run">What choosing it does, or <see langword="null"/> when it only opens a submenu or does nothing.</param>
[DebuggerDisplay("NativeMenuItem: {Title}")]
public sealed record NativeMenuItem(string Title, string? Shortcut, bool IsEnabled, bool IsChecked, bool IsSeparator, NativeMenuItem[] Children, Action? Run)
{
    /// <summary>Gets a line between groups of entries.</summary>
    public static NativeMenuItem Separator { get; } = new(string.Empty, null, true, false, true, [], null);
}
