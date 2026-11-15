// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_RESOURCE_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="Dimension">Buffer or two dimensional texture.</param>
/// <param name="Alignment">The alignment, 0 for the default.</param>
/// <param name="Width">The width in pixels, or the size of a buffer in bytes.</param>
/// <param name="Height">The height in pixels, 1 for a buffer.</param>
/// <param name="DepthOrArraySize">The depth or array size, 1 here.</param>
/// <param name="MipLevels">The mip levels, 1 here.</param>
/// <param name="Format">The DXGI format.</param>
/// <param name="SampleCount">The samples per pixel.</param>
/// <param name="SampleQuality">The sample quality.</param>
/// <param name="Layout">The texture layout, row major for a buffer.</param>
/// <param name="Flags">The resource flags.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ResourceDescription(
    uint Dimension,
    ulong Alignment,
    ulong Width,
    uint Height,
    ushort DepthOrArraySize,
    ushort MipLevels,
    uint Format,
    uint SampleCount,
    uint SampleQuality,
    uint Layout,
    uint Flags);
