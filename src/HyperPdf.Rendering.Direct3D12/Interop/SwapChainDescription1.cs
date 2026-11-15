// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native DXGI_SWAP_CHAIN_DESC1 structure, in the field order and layout of the C structure.</summary>
/// <param name="Width">The width in pixels, or 0 for the window's.</param>
/// <param name="Height">The height in pixels, or 0 for the window's.</param>
/// <param name="Format">The DXGI format.</param>
/// <param name="Stereo">Whether the buffers are stereo.</param>
/// <param name="SampleCount">The samples per pixel.</param>
/// <param name="SampleQuality">The sample quality.</param>
/// <param name="BufferUsage">How the buffers are used.</param>
/// <param name="BufferCount">The number of buffers.</param>
/// <param name="Scaling">How the buffers scale to the window.</param>
/// <param name="SwapEffect">How buffers are swapped.</param>
/// <param name="AlphaMode">The alpha mode.</param>
/// <param name="Flags">The swap chain flags.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SwapChainDescription1(
    uint Width,
    uint Height,
    uint Format,
    uint Stereo,
    uint SampleCount,
    uint SampleQuality,
    uint BufferUsage,
    uint BufferCount,
    uint Scaling,
    uint SwapEffect,
    uint AlphaMode,
    uint Flags);
