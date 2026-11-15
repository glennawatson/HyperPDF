// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Windows.Accessibility;

/// <summary>A UiaRect: the left, top, width and height in screen coordinates.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("UiaRect ({Left}, {Top}) {Width} x {Height}")]
internal readonly record struct UiaRect(double Left, double Top, double Width, double Height);
