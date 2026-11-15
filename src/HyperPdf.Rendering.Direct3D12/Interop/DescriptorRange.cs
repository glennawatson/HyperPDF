// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_DESCRIPTOR_RANGE structure, in the field order and layout of the C structure.</summary>
/// <param name="RangeType">Shader resources, samplers, constants or unordered access.</param>
/// <param name="Count">The descriptors in the range.</param>
/// <param name="BaseRegister">The first shader register.</param>
/// <param name="Space">The register space.</param>
/// <param name="Offset">The offset in the table, or the value that appends.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DescriptorRange(
    uint RangeType,
    uint Count,
    uint BaseRegister,
    uint Space,
    uint Offset);
