// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Input;

/// <summary>A pointer move over the page canvas, in the terms every render head can supply.</summary>
/// <param name="Position">Where the pointer is on the canvas.</param>
/// <param name="InScroller">Where the pointer is in the scrolling area, which does not move as the pages scroll.</param>
/// <param name="LeftDown">Whether the left button is down.</param>
[DebuggerDisplay("ViewerMove: ({Position.X}, {Position.Y})")]
public readonly record struct ViewerMove(CanvasPoint Position, CanvasPoint InScroller, bool LeftDown);
