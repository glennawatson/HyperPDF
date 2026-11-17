// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>The native <c>D3D11_RECT</c>: the part of the frame drawing may reach.</summary>
/// <param name="Left">The first column.</param>
/// <param name="Top">The first row.</param>
/// <param name="Right">One past the last column.</param>
/// <param name="Bottom">One past the last row.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ScissorRect(int Left, int Top, int Right, int Bottom);
