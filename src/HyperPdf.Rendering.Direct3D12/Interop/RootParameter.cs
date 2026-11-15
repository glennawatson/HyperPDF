// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_ROOT_PARAMETER, with its union laid out as the two fields a table and a block of constants use structure, in the field order and layout of the C structure.</summary>
/// <param name="Type">Descriptor table or 32 bit constants.</param>
/// <param name="Padding">The bytes that align the union.</param>
/// <param name="First">The range count of a table, or the shader register of constants.</param>
/// <param name="Second">The register space of constants, unused by a table.</param>
/// <param name="Third">The range pointer of a table, or the constant count.</param>
/// <param name="Visibility">The shader stages that see the parameter.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RootParameter(
    uint Type,
    uint Padding,
    uint First,
    uint Second,
    ulong Third,
    uint Visibility);
