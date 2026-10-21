// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Input;

/// <summary>A pointer press on the page canvas, in the terms every render head can supply.</summary>
/// <param name="Position">Where the pointer is on the canvas.</param>
/// <param name="InScroller">Where the pointer is in the scrolling area, which does not move as the pages scroll.</param>
/// <param name="Buttons">The buttons that are down.</param>
/// <param name="Modifiers">The modifier keys held.</param>
/// <param name="ClickCount">How many clicks in a row this press is.</param>
/// <param name="Handled">Whether something else has already used the press.</param>
[DebuggerDisplay("ViewerPress: ({Position.X}, {Position.Y}) {Buttons}")]
public readonly record struct ViewerPress(CanvasPoint Position, CanvasPoint InScroller, ViewerButtons Buttons, ViewerModifiers Modifiers, int ClickCount, bool Handled);
