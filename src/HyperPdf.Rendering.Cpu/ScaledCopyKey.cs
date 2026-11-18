// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Cpu;

/// <summary>Names one smooth scaling of a surface: the surface and the size it is drawn at.</summary>
/// <param name="Surface">The surface.</param>
/// <param name="Width">The width it is drawn at, in pixels.</param>
/// <param name="Height">The height it is drawn at, in pixels.</param>
[DebuggerDisplay("ScaledCopyKey {Width} x {Height}")]
internal readonly record struct ScaledCopyKey(CpuPixelSurface Surface, int Width, int Height);
