// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One press of a control of the bars, by the pointer, a keyboard shortcut or an assistive technology.</summary>
/// <param name="Command">What the control does.</param>
/// <param name="Index">Which tab, tool or row the control stands for.</param>
/// <param name="Bounds">Where the control is, in device pixels. Empty for a keyboard shortcut.</param>
[DebuggerDisplay("ChromePress {Command} {Index}")]
public readonly record struct ChromePress(ChromeCommand Command, int Index, LayoutRect Bounds);
