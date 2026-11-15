// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_BLEND_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="AlphaToCoverage">Whether alpha to coverage is on.</param>
/// <param name="Independent">Whether every target blends on its own.</param>
/// <param name="Targets">The blend state of the eight targets.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct BlendDescription(
    uint AlphaToCoverage,
    uint Independent,
    RenderTargetBlends Targets);
