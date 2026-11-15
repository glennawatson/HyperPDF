// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_ROOT_SIGNATURE_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="ParameterCount">The number of root parameters.</param>
/// <param name="Parameters">The root parameters.</param>
/// <param name="SamplerCount">The number of static samplers.</param>
/// <param name="Samplers">The static samplers.</param>
/// <param name="Flags">The root signature flags.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RootSignatureDescription(
    uint ParameterCount,
    ulong Parameters,
    uint SamplerCount,
    ulong Samplers,
    uint Flags);
