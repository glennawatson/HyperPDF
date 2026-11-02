// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>One block of a U3D file.</summary>
/// <param name="Type">The block type.</param>
/// <param name="Data">The data section, without padding.</param>
/// <param name="Meta">The meta data section, without padding.</param>
[DebuggerDisplay("U3dBlock: {Type} {Data.Length} bytes")]
internal readonly record struct U3dBlock(uint Type, ReadOnlyMemory<byte> Data, ReadOnlyMemory<byte> Meta);
