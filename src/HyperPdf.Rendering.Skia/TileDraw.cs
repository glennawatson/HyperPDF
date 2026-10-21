// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdf.Rendering.Skia;

/// <summary>One retained GPU tile and where it is drawn.</summary>
/// <param name="Tile">The prepared tile.</param>
/// <param name="Bounds">The destination in the canvas's local coordinates.</param>
/// <param name="Smooth">Whether the destination scales a preview or an earlier zoom level.</param>
[DebuggerDisplay("TileDraw: {Bounds}, smooth {Smooth}")]
public readonly record struct TileDraw(GpuPreparedRenderSurface Tile, SKRect Bounds, bool Smooth);
