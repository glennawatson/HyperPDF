// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_RESOURCE_BARRIER for a transition of one resource structure, in the field order and layout of the C structure.</summary>
/// <param name="Type">The barrier type, 0 for a transition.</param>
/// <param name="Flags">The barrier flags.</param>
/// <param name="Resource">The ID3D12Resource.</param>
/// <param name="Subresource">The subresource, or every one.</param>
/// <param name="StateBefore">The state the resource is in.</param>
/// <param name="StateAfter">The state the resource goes to.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ResourceBarrier(
    uint Type,
    uint Flags,
    ulong Resource,
    uint Subresource,
    uint StateBefore,
    uint StateAfter);
