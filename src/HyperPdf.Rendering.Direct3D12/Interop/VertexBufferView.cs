// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_VERTEX_BUFFER_VIEW structure, in the field order and layout of the C structure.</summary>
/// <param name="Location">The GPU address of the buffer.</param>
/// <param name="Size">The size of the view in bytes.</param>
/// <param name="Stride">The bytes per vertex.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct VertexBufferView(
    ulong Location,
    uint Size,
    uint Stride);
