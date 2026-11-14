// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Windowing.Rendering;

/// <summary>One corner of a quad in the layout every GPU backend's vertex shader reads: 20 bytes.</summary>
/// <param name="X">The x in frame pixels.</param>
/// <param name="Y">The y in frame pixels.</param>
/// <param name="U">The texture coordinate across, from 0 to 1.</param>
/// <param name="V">The texture coordinate down, from 0 to 1.</param>
/// <param name="Color">The premultiplied colour, with red in the low byte.</param>
[DebuggerDisplay("QuadVertex ({X}, {Y})")]
[StructLayout(LayoutKind.Sequential)]
public readonly record struct QuadVertex(float X, float Y, float U, float V, uint Color);
