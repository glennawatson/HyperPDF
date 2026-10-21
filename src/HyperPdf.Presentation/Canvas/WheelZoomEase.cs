// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>A wheel zoom easing from one zoom to another over the window's frames.</summary>
/// <param name="IsRunning">Whether it is still easing.</param>
/// <param name="From">The zoom it started from.</param>
/// <param name="To">The zoom it ends at.</param>
/// <param name="Started">When it started, as a <see cref="Stopwatch"/> timestamp.</param>
/// <param name="Anchor">The point in viewport coordinates that stays still while the zoom eases.</param>
[DebuggerDisplay("WheelZoomEase: {From} to {To}, running={IsRunning}")]
public readonly record struct WheelZoomEase(bool IsRunning, double From, double To, long Started, CanvasPoint Anchor);
