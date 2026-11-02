// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A triangle mesh decoded from a CLOD mesh's base mesh, or made by tessellating a free-form surface.</summary>
/// <param name="Name">The name of the model resource.</param>
/// <param name="Positions">The positions.</param>
/// <param name="Normals">The normals; empty when the mesh has none.</param>
/// <param name="DiffuseColors">The diffuse colours with their alpha.</param>
/// <param name="TextureCoordinates">The texture coordinates; the first two components are U and V.</param>
/// <param name="Faces">The triangles.</param>
/// <param name="IsComplete"><see langword="false"/> when the file holds resolution updates that were not applied, so the mesh is its base mesh only.</param>
[DebuggerDisplay("U3dMesh: {Name} {Faces.Length} faces")]
public sealed record U3dMesh(
    string Name,
    Vector3[] Positions,
    Vector3[] Normals,
    Vector4[] DiffuseColors,
    Vector4[] TextureCoordinates,
    U3dFace[] Faces,
    bool IsComplete);
