// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_RASTERIZER_DESC, in the field order and layout of the C structure.</summary>
/// <param name="FillMode">The fill mode.</param>
/// <param name="CullMode">The cull mode.</param>
/// <param name="FrontCounterClockwise">Nonzero when counter clockwise triangles face front.</param>
/// <param name="DepthBias">The depth bias.</param>
/// <param name="DepthBiasClamp">The depth bias clamp.</param>
/// <param name="SlopeScaledDepthBias">The slope scaled depth bias.</param>
/// <param name="DepthClipEnable">Nonzero to clip by depth.</param>
/// <param name="ScissorEnable">Nonzero to use the scissor rectangle.</param>
/// <param name="MultisampleEnable">Nonzero for multisample lines.</param>
/// <param name="AntialiasedLineEnable">Nonzero for antialiased lines.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RasterizerDescription(
    uint FillMode,
    uint CullMode,
    int FrontCounterClockwise,
    int DepthBias,
    float DepthBiasClamp,
    float SlopeScaledDepthBias,
    int DepthClipEnable,
    int ScissorEnable,
    int MultisampleEnable,
    int AntialiasedLineEnable);
