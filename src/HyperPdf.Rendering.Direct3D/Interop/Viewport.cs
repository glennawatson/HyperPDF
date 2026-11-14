// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_VIEWPORT, in the field order and layout of the C structure.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="MinDepth">The nearest depth.</param>
/// <param name="MaxDepth">The farthest depth.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct Viewport(float X, float Y, float Width, float Height, float MinDepth, float MaxDepth);
