// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native DXGI_SAMPLE_DESC structure, in the field order and layout of the C structure.</summary>
/// <param name="Count">The samples per pixel.</param>
/// <param name="Quality">The sample quality.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SampleDescription(
    uint Count,
    uint Quality);
