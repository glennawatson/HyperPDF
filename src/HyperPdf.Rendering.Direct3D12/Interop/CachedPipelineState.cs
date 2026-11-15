// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_CACHED_PIPELINE_STATE, unused and zero structure, in the field order and layout of the C structure.</summary>
/// <param name="Blob">The cached blob.</param>
/// <param name="Size">The blob size.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CachedPipelineState(
    ulong Blob,
    ulong Size);
