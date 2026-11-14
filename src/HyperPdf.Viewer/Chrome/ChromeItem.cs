// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One control of the window, laid out: a tab, a button, a menu row, a label.</summary>
/// <param name="Kind">How it is drawn.</param>
/// <param name="Command">What it does when pressed.</param>
/// <param name="Index">Which tab, menu entry or list row it stands for.</param>
/// <param name="Text">The text shown, already cut to fit.</param>
/// <param name="Bounds">Where it is in the window, in device pixels.</param>
/// <param name="Enabled">Whether it can be pressed.</param>
/// <param name="Selected">Whether it is the chosen one of its group, such as the tab shown or a ticked menu entry.</param>
[DebuggerDisplay("ChromeItem: {Kind} {Command} '{Text}'")]
public sealed record ChromeItem(ChromeItemKind Kind, ChromeCommand Command, int Index, string Text, LayoutRect Bounds, bool Enabled, bool Selected);
