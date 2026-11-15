// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_SUBRESOURCE_DATA, in the field order and layout of the C structure.</summary>
/// <param name="Data">The address of the first contents.</param>
/// <param name="RowPitch">The bytes per row of a texture.</param>
/// <param name="SlicePitch">The bytes per slice of a 3D texture.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SubresourceData(ulong Data, uint RowPitch, uint SlicePitch);
