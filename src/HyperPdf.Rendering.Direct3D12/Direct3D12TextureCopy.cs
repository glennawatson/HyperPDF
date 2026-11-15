// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Direct3D12;

/// <summary>Where a copy of pixels goes: a texture and the shape of the pixels.</summary>
/// <param name="Texture">The ID3D12Resource, in the copy destination state when <paramref name="IsNew"/> and in the pixel shader state otherwise.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Stride">The bytes per line of the pixels.</param>
/// <param name="IsNew">True for a texture that never held pixels.</param>
[DebuggerDisplay("Direct3D12TextureCopy {Width} x {Height}")]
internal readonly record struct Direct3D12TextureCopy(ulong Texture, int Width, int Height, int Stride, bool IsNew);
