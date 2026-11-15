// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_HEAP_PROPERTIES structure, in the field order and layout of the C structure.</summary>
/// <param name="Type">The heap type: default, upload or readback.</param>
/// <param name="CpuPageProperty">The CPU page property, 0 for the type's own.</param>
/// <param name="MemoryPoolPreference">The memory pool, 0 for the type's own.</param>
/// <param name="CreationNodeMask">The adapter node the memory is on.</param>
/// <param name="VisibleNodeMask">The adapter nodes that see the memory.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct HeapProperties(
    uint Type,
    uint CpuPageProperty,
    uint MemoryPoolPreference,
    uint CreationNodeMask,
    uint VisibleNodeMask);
