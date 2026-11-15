// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_DEPTH_STENCILOP_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="StencilFail">The operation when the stencil test fails.</param>
/// <param name="DepthFail">The operation when the depth test fails.</param>
/// <param name="Pass">The operation when both pass.</param>
/// <param name="Function">The stencil comparison.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DepthStencilOperation(
    uint StencilFail,
    uint DepthFail,
    uint Pass,
    uint Function);
