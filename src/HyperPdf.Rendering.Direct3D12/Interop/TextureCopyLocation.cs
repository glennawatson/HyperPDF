// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_TEXTURE_COPY_LOCATION, with its union laid out as the placed footprint structure, in the field order and layout of the C structure.</summary>
/// <param name="Resource">The ID3D12Resource.</param>
/// <param name="Type">A subresource index or a placed footprint.</param>
/// <param name="Offset">The offset of the footprint in the buffer, or the subresource index in the low bits.</param>
/// <param name="Format">The DXGI format of the footprint.</param>
/// <param name="Width">The width of the footprint.</param>
/// <param name="Height">The height of the footprint.</param>
/// <param name="Depth">The depth of the footprint.</param>
/// <param name="RowPitch">The bytes per row of the footprint.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TextureCopyLocation(
    ulong Resource,
    uint Type,
    ulong Offset,
    uint Format,
    uint Width,
    uint Height,
    uint Depth,
    uint RowPitch);
