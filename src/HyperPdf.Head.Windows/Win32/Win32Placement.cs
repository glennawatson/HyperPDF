// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Windows.Win32;

/// <summary>Where a window was and how it looked before it filled the screen, so it can be put back.</summary>
/// <param name="Style">The window style.</param>
/// <param name="X">The left edge of the window on the screen.</param>
/// <param name="Y">The top edge of the window on the screen.</param>
/// <param name="Width">The width of the window.</param>
/// <param name="Height">The height of the window.</param>
[DebuggerDisplay("Win32Placement: {Width} x {Height} at {X}, {Y}")]
internal readonly record struct Win32Placement(long Style, int X, int Y, int Width, int Height);
