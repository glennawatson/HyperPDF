// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_RASTERIZER_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="FillMode">Solid or wireframe.</param>
/// <param name="CullMode">Which faces are culled.</param>
/// <param name="FrontCounterClockwise">Which winding is the front.</param>
/// <param name="DepthBias">The depth bias.</param>
/// <param name="DepthBiasClamp">The depth bias limit.</param>
/// <param name="SlopeScaledDepthBias">The slope scaled bias.</param>
/// <param name="DepthClipEnable">Whether depth is clipped.</param>
/// <param name="MultisampleEnable">Whether multisampling is on.</param>
/// <param name="AntialiasedLineEnable">Whether lines are smoothed.</param>
/// <param name="ForcedSampleCount">The forced sample count.</param>
/// <param name="ConservativeRaster">Whether conservative rasterisation is on.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RasterizerDescription(
    uint FillMode,
    uint CullMode,
    uint FrontCounterClockwise,
    int DepthBias,
    float DepthBiasClamp,
    float SlopeScaledDepthBias,
    uint DepthClipEnable,
    uint MultisampleEnable,
    uint AntialiasedLineEnable,
    uint ForcedSampleCount,
    uint ConservativeRaster);
