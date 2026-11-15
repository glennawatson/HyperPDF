// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_BUFFER_DESC, in the field order and layout of the C structure.</summary>
/// <param name="ByteWidth">The size in bytes.</param>
/// <param name="Usage">How the buffer is read and written.</param>
/// <param name="BindFlags">How the buffer is bound to the pipeline.</param>
/// <param name="CpuAccessFlags">How the CPU may access the buffer.</param>
/// <param name="MiscFlags">Other flags.</param>
/// <param name="StructureByteStride">The element size of a structured buffer, or 0.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct BufferDescription(uint ByteWidth, uint Usage, uint BindFlags, uint CpuAccessFlags, uint MiscFlags, uint StructureByteStride);
