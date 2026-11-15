// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The formats of the eight render targets of a pipeline state, in the layout of the C array.</summary>
/// <param name="Target0">The DXGI format of target 0, or 0 for none.</param>
/// <param name="Target1">The DXGI format of target 1, or 0 for none.</param>
/// <param name="Target2">The DXGI format of target 2, or 0 for none.</param>
/// <param name="Target3">The DXGI format of target 3, or 0 for none.</param>
/// <param name="Target4">The DXGI format of target 4, or 0 for none.</param>
/// <param name="Target5">The DXGI format of target 5, or 0 for none.</param>
/// <param name="Target6">The DXGI format of target 6, or 0 for none.</param>
/// <param name="Target7">The DXGI format of target 7, or 0 for none.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RenderTargetFormats(
    uint Target0,
    uint Target1,
    uint Target2,
    uint Target3,
    uint Target4,
    uint Target5,
    uint Target6,
    uint Target7);
