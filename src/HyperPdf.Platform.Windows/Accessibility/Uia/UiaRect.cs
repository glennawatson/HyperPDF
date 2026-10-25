// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>A rectangle as UI Automation passes it: the left and top edge, then the width and height, in screen pixels.</summary>
/// <param name="Left">Pixels from the left of the screen.</param>
/// <param name="Top">Pixels from the top of the screen.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("UiaRect: {Left}, {Top}, {Width} x {Height}")]
internal readonly record struct UiaRect(double Left, double Top, double Width, double Height);
