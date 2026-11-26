// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Windows;

/// <summary>The words of the application menu an operating system shows first on its menu bar, in the language shown.</summary>
/// <param name="Hide">The entry that hides the application.</param>
/// <param name="HideOthers">The entry that hides every other application.</param>
/// <param name="ShowAll">The entry that shows every application again.</param>
/// <param name="Quit">The entry that closes the application, asking first about unsaved changes.</param>
[DebuggerDisplay("NativeAppMenu: {Quit}")]
public sealed record NativeAppMenu(string Hide, string HideOthers, string ShowAll, string Quit);
