// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_STREAM_OUTPUT_DESC, unused and zero structure, in the field order and layout of the C structure.</summary>
/// <param name="Declarations">The declarations.</param>
/// <param name="DeclarationCount">The declaration count.</param>
/// <param name="Strides">The strides.</param>
/// <param name="StrideCount">The stride count.</param>
/// <param name="RasterizedStream">The rasterised stream.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct StreamOutputDescription(
    ulong Declarations,
    uint DeclarationCount,
    ulong Strides,
    uint StrideCount,
    uint RasterizedStream);
