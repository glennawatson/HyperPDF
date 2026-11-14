// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Windows;

/// <summary>One event from a platform window, in a shape every head can fill without allocating.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="X">The pointer x, or the new width for a resize, in device pixels.</param>
/// <param name="Y">The pointer y, or the new height for a resize, in device pixels.</param>
/// <param name="Code">The platform key code, or the pointer button number.</param>
/// <param name="Modifiers">The platform modifier bits.</param>
[DebuggerDisplay("WindowEvent {Kind} ({X}, {Y}) code {Code}")]
public readonly record struct WindowEvent(WindowEventKind Kind, int X, int Y, uint Code, uint Modifiers);
