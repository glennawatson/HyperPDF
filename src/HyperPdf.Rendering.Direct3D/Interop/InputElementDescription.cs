// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Rendering.Direct3D.Interop;

/// <summary>D3D11_INPUT_ELEMENT_DESC, in the field order and layout of the C structure.</summary>
/// <param name="SemanticName">The null terminated ASCII semantic name.</param>
/// <param name="SemanticIndex">The semantic index.</param>
/// <param name="Format">The element format.</param>
/// <param name="InputSlot">The vertex buffer slot.</param>
/// <param name="AlignedByteOffset">The offset of the element in a vertex.</param>
/// <param name="InputSlotClass">0 for per vertex data.</param>
/// <param name="InstanceDataStepRate">The instances drawn per element, which is 0 when nothing is instanced.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct InputElementDescription(ulong SemanticName, uint SemanticIndex, uint Format, uint InputSlot, uint AlignedByteOffset, uint InputSlotClass, uint InstanceDataStepRate);
