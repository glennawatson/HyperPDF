// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;

namespace HyperPdf.Viewer.Chrome;

/// <summary>A key, with the modifier keys held, and the view model command it runs.</summary>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifier keys that must be held, and no others.</param>
/// <param name="Command">Finds the command, or <see langword="null"/> when there is nothing for it to act on, such as a tab command with no tab open.</param>
[DebuggerDisplay("Shortcut: {Modifiers}+{Key}")]
internal sealed record Shortcut(ViewerKey Key, ViewerModifiers Modifiers, Func<MainViewModel, ICommand?> Command)
{
    /// <summary>Gets what finds the value the command runs with, or <see langword="null"/> for a command that takes nothing.</summary>
    internal Func<MainViewModel, object?>? Parameter { get; init; }
}
