// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>One corner of a face: indices into the mesh's arrays, or -1 where the corner has no such value.</summary>
/// <param name="Position">The index of the position.</param>
/// <param name="Normal">The index of the normal.</param>
/// <param name="Diffuse">The index of the diffuse colour.</param>
/// <param name="TextureCoordinate">The index of the first layer's texture coordinate.</param>
[DebuggerDisplay("U3dCorner: {Position}")]
public readonly record struct U3dCorner(int Position, int Normal, int Diffuse, int TextureCoordinate);
