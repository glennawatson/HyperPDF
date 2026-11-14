// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_RENDER_TARGET_BLEND_DESC, in the field order and layout of the C structure.</summary>
/// <param name="BlendEnable">Nonzero to blend.</param>
/// <param name="SourceBlend">The source colour factor.</param>
/// <param name="DestinationBlend">The destination colour factor.</param>
/// <param name="BlendOperation">The colour operation.</param>
/// <param name="SourceBlendAlpha">The source alpha factor.</param>
/// <param name="DestinationBlendAlpha">The destination alpha factor.</param>
/// <param name="BlendOperationAlpha">The alpha operation.</param>
/// <param name="WriteMask">The channels written, in the low byte.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct TargetBlendDescription(
    int BlendEnable,
    uint SourceBlend,
    uint DestinationBlend,
    uint BlendOperation,
    uint SourceBlendAlpha,
    uint DestinationBlendAlpha,
    uint BlendOperationAlpha,
    uint WriteMask);
