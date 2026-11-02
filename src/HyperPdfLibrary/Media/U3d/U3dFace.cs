// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A triangle of a mesh.</summary>
/// <param name="Shading">The index of the shader list the face uses.</param>
/// <param name="A">The first corner.</param>
/// <param name="B">The second corner.</param>
/// <param name="C">The third corner.</param>
[DebuggerDisplay("U3dFace: shading {Shading}")]
public readonly record struct U3dFace(int Shading, U3dCorner A, U3dCorner B, U3dCorner C);
