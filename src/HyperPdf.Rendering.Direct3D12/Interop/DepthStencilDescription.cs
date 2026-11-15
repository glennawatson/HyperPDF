// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_DEPTH_STENCIL_DESC, which a frame of flat quads leaves off structure, in the field order and layout of the C structure.</summary>
/// <param name="DepthEnable">Whether depth testing is on.</param>
/// <param name="DepthWriteMask">Whether depth is written.</param>
/// <param name="DepthFunction">The depth comparison.</param>
/// <param name="StencilEnable">Whether stencil testing is on.</param>
/// <param name="StencilReadMask">The stencil read mask.</param>
/// <param name="StencilWriteMask">The stencil write mask.</param>
/// <param name="Front">The front face operations.</param>
/// <param name="Back">The back face operations.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DepthStencilDescription(
    uint DepthEnable,
    uint DepthWriteMask,
    uint DepthFunction,
    uint StencilEnable,
    byte StencilReadMask,
    byte StencilWriteMask,
    DepthStencilOperation Front,
    DepthStencilOperation Back);
