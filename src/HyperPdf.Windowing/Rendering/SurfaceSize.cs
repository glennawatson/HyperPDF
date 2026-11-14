// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Rendering;

/// <summary>The size of a drawing area in device pixels.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
[DebuggerDisplay("{Width} x {Height}")]
public readonly record struct SurfaceSize(int Width, int Height)
{
    /// <summary>Gets a value indicating whether either side is zero or less, so nothing can be drawn.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
