// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_COMMAND_QUEUE_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="Type">The queue type, 0 for direct.</param>
/// <param name="Priority">The priority, 0 for normal.</param>
/// <param name="Flags">The queue flags.</param>
/// <param name="NodeMask">The adapter node.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CommandQueueDescription(
    uint Type,
    int Priority,
    uint Flags,
    uint NodeMask);
