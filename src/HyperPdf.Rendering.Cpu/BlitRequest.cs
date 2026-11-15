// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Where a source image lands in a frame.</summary>
/// <param name="X">The destination left edge in frame pixels.</param>
/// <param name="Y">The destination top edge in frame pixels.</param>
/// <param name="Width">The destination width in frame pixels.</param>
/// <param name="Height">The destination height in frame pixels.</param>
/// <param name="Smooth">Whether to sample bilinearly when scaling.</param>
/// <param name="Opaque">Whether the source is known to have no transparent pixel, so its rows are copied without being looked at first.</param>
[DebuggerDisplay("BlitRequest ({X}, {Y}) {Width} x {Height}, smooth {Smooth}")]
public readonly record struct BlitRequest(int X, int Y, int Width, int Height, bool Smooth, bool Opaque);
