// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>DXGI_SWAP_CHAIN_DESC, in the field order and layout of the C structure.</summary>
/// <param name="Width">The buffer width, or 0 for the window's.</param>
/// <param name="Height">The buffer height, or 0 for the window's.</param>
/// <param name="RefreshNumerator">The refresh rate numerator, or 0.</param>
/// <param name="RefreshDenominator">The refresh rate denominator, or 0.</param>
/// <param name="Format">The buffer format.</param>
/// <param name="ScanlineOrdering">The scanline ordering, 0 for unspecified.</param>
/// <param name="Scaling">The scaling, 0 for unspecified.</param>
/// <param name="SampleCount">The samples per pixel.</param>
/// <param name="SampleQuality">The sample quality.</param>
/// <param name="BufferUsage">How the buffers are used.</param>
/// <param name="BufferCount">The number of buffers.</param>
/// <param name="OutputWindow">The window handle.</param>
/// <param name="Windowed">Nonzero for a windowed swap chain.</param>
/// <param name="SwapEffect">How buffers are swapped.</param>
/// <param name="Flags">The swap chain flags.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SwapChainDescription(
    uint Width,
    uint Height,
    uint RefreshNumerator,
    uint RefreshDenominator,
    uint Format,
    uint ScanlineOrdering,
    uint Scaling,
    uint SampleCount,
    uint SampleQuality,
    uint BufferUsage,
    uint BufferCount,
    ulong OutputWindow,
    int Windowed,
    uint SwapEffect,
    uint Flags);
