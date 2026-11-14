// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Receives a finished CPU frame so a platform head can copy it to its window.</summary>
/// <param name="pixels">The premultiplied BGRA frame.</param>
/// <param name="size">The frame size in pixels.</param>
/// <param name="stride">The bytes per line.</param>
public delegate void FramePresented(ReadOnlySpan<byte> pixels, SurfaceSize size, int stride);
