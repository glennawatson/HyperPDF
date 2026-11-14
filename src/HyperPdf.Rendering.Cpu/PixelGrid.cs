// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>The shape of a BGRA pixel buffer.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Stride">The bytes per line.</param>
[DebuggerDisplay("PixelGrid {Width} x {Height}, stride {Stride}")]
public readonly record struct PixelGrid(int Width, int Height, int Stride);
