// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.TestAssets;

/// <summary>A triangle mesh for a test model.</summary>
/// <param name="Name">The mesh's name, which is also its model resource's name.</param>
/// <param name="Positions">The positions.</param>
/// <param name="Normals">The normals; empty for a mesh without normals.</param>
/// <param name="DiffuseColors">The diffuse colours.</param>
/// <param name="TextureCoordinates">The texture coordinates.</param>
/// <param name="Faces">The faces.</param>
/// <param name="Shadings">The shading descriptions.</param>
/// <param name="FinalResolution">The position count the declaration gives for the finished mesh; a value above the position count makes the mesh declare resolution updates.</param>
public sealed record U3dMeshSpec(
    string Name,
    Vector3[] Positions,
    Vector3[] Normals,
    Vector4[] DiffuseColors,
    Vector4[] TextureCoordinates,
    U3dFaceSpec[] Faces,
    U3dShadingSpec[] Shadings,
    uint FinalResolution);
