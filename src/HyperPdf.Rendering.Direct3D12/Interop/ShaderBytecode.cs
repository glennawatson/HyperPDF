// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D12.Interop;

/// <summary>The native D3D12_SHADER_BYTECODE structure, in the field order and layout of the C structure.</summary>
/// <param name="Code">The bytecode.</param>
/// <param name="Length">The bytecode length.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ShaderBytecode(
    ulong Code,
    ulong Length);
