// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_GRAPHICS_PIPELINE_STATE_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="RootSignature">The ID3D12RootSignature.</param>
/// <param name="Vertex">The vertex shader.</param>
/// <param name="Pixel">The pixel shader.</param>
/// <param name="Domain">The domain shader, none.</param>
/// <param name="Hull">The hull shader, none.</param>
/// <param name="Geometry">The geometry shader, none.</param>
/// <param name="StreamOutput">The stream output, none.</param>
/// <param name="Blend">The blend state.</param>
/// <param name="SampleMask">The samples that are written.</param>
/// <param name="Rasterizer">The rasteriser state.</param>
/// <param name="DepthStencil">The depth and stencil state.</param>
/// <param name="InputLayout">The vertex layout.</param>
/// <param name="StripCutValue">The strip cut value, none.</param>
/// <param name="TopologyType">The kind of primitive.</param>
/// <param name="TargetCount">The number of render targets.</param>
/// <param name="TargetFormats">The formats of the targets.</param>
/// <param name="DepthFormat">The depth format, none.</param>
/// <param name="Sample">The sample description.</param>
/// <param name="NodeMask">The adapter node.</param>
/// <param name="Cached">The cached state, none.</param>
/// <param name="Flags">The pipeline flags.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct GraphicsPipelineDescription(
    ulong RootSignature,
    ShaderBytecode Vertex,
    ShaderBytecode Pixel,
    ShaderBytecode Domain,
    ShaderBytecode Hull,
    ShaderBytecode Geometry,
    StreamOutputDescription StreamOutput,
    BlendDescription Blend,
    uint SampleMask,
    RasterizerDescription Rasterizer,
    DepthStencilDescription DepthStencil,
    InputLayoutDescription InputLayout,
    uint StripCutValue,
    uint TopologyType,
    uint TargetCount,
    RenderTargetFormats TargetFormats,
    uint DepthFormat,
    SampleDescription Sample,
    uint NodeMask,
    CachedPipelineState Cached,
    uint Flags);
