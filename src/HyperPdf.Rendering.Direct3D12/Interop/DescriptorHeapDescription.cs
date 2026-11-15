// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_DESCRIPTOR_HEAP_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="Type">The kind of descriptors.</param>
/// <param name="Count">The number of descriptors.</param>
/// <param name="Flags">Whether shaders see the heap.</param>
/// <param name="NodeMask">The adapter node.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct DescriptorHeapDescription(
    uint Type,
    uint Count,
    uint Flags,
    uint NodeMask);
