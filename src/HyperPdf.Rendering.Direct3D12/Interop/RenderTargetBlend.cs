// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_RENDER_TARGET_BLEND_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="BlendEnable">Whether blending is on.</param>
/// <param name="LogicOpEnable">Whether a logic operation is on.</param>
/// <param name="Source">The source blend factor.</param>
/// <param name="Destination">The destination blend factor.</param>
/// <param name="Operation">The colour blend operation.</param>
/// <param name="SourceAlpha">The source alpha blend factor.</param>
/// <param name="DestinationAlpha">The destination alpha blend factor.</param>
/// <param name="OperationAlpha">The alpha blend operation.</param>
/// <param name="LogicOperation">The logic operation.</param>
/// <param name="WriteMask">The channels that are written.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RenderTargetBlend(
    uint BlendEnable,
    uint LogicOpEnable,
    uint Source,
    uint Destination,
    uint Operation,
    uint SourceAlpha,
    uint DestinationAlpha,
    uint OperationAlpha,
    uint LogicOperation,
    uint WriteMask);
