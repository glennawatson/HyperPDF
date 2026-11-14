// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_MAPPED_SUBRESOURCE, in the field order and layout of the C structure.</summary>
/// <param name="Data">The address of the mapped memory.</param>
/// <param name="RowPitch">The bytes per row.</param>
/// <param name="DepthPitch">The bytes per slice.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct MappedSubresource(ulong Data, uint RowPitch, uint DepthPitch);
