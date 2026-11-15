// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The blend states of the eight render targets of a blend description, in the layout of the C array.</summary>
/// <param name="Target0">The blend state of target 0.</param>
/// <param name="Target1">The blend state of target 1.</param>
/// <param name="Target2">The blend state of target 2.</param>
/// <param name="Target3">The blend state of target 3.</param>
/// <param name="Target4">The blend state of target 4.</param>
/// <param name="Target5">The blend state of target 5.</param>
/// <param name="Target6">The blend state of target 6.</param>
/// <param name="Target7">The blend state of target 7.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct RenderTargetBlends(
    RenderTargetBlend Target0,
    RenderTargetBlend Target1,
    RenderTargetBlend Target2,
    RenderTargetBlend Target3,
    RenderTargetBlend Target4,
    RenderTargetBlend Target5,
    RenderTargetBlend Target6,
    RenderTargetBlend Target7);
