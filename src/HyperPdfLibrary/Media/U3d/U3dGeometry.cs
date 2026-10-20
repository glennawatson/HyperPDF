// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The size of a mesh, point set or line set at full resolution, as its declaration gives it. The shapes themselves are not decoded.</summary>
/// <param name="Name">The generator's name.</param>
/// <param name="Kind">The block type that declared it.</param>
/// <param name="Primitives">The faces of a mesh, the points of a point set or the lines of a line set.</param>
/// <param name="Positions">The positions.</param>
/// <param name="Normals">The normals.</param>
/// <param name="TextureCoordinates">The texture coordinates.</param>
/// <param name="ShadingLists">The shading descriptions, which stand for the materials the shape uses.</param>
[DebuggerDisplay("U3dGeometry: {Kind} {Name}")]
public sealed record U3dGeometry(string Name, U3dBlockType Kind, uint Primitives, uint Positions, uint Normals, uint TextureCoordinates, uint ShadingLists);
