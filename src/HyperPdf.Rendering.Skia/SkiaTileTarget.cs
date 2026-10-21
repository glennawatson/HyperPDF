// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdf.Rendering.Skia;

/// <summary>What a render head hands a tile batch to draw into: the canvas and, when the head has one, its GPU context.</summary>
/// <param name="Canvas">The canvas to draw on.</param>
/// <param name="Gpu">The graphics context, or <see langword="null"/> when the canvas is a raster one.</param>
/// <param name="Retirement">The queue that releases GPU images on the render thread, or <see langword="null"/> without a GPU.</param>
/// <param name="ColorType">The color type the surface presents.</param>
[DebuggerDisplay("SkiaTileTarget: {ColorType}, GPU {Gpu != null}")]
public readonly record struct SkiaTileTarget(SKCanvas Canvas, GRContext? Gpu, SkiaGpuImageRetirement? Retirement, SKColorType ColorType);
