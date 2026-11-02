// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>What a CLOD mesh declaration says about the mesh at full resolution (ECMA-363 9.6.1.1).</summary>
/// <param name="Name">The mesh's name.</param>
/// <param name="ExcludesNormals"><see langword="true"/> when faces carry no normal index.</param>
/// <param name="Faces">The number of faces the finished mesh has.</param>
/// <param name="Positions">The number of positions the finished mesh has.</param>
/// <param name="Normals">The number of normals the finished mesh has.</param>
/// <param name="DiffuseColors">The number of diffuse colours the finished mesh has.</param>
/// <param name="SpecularColors">The number of specular colours the finished mesh has.</param>
/// <param name="TextureCoordinates">The number of texture coordinates the finished mesh has.</param>
/// <param name="Shadings">The shading descriptions.</param>
/// <param name="MinimumResolution">The positions in the base mesh.</param>
/// <param name="FinalResolution">The positions at full resolution.</param>
[DebuggerDisplay("U3dMeshDeclaration: {Name} {Faces} faces")]
internal sealed record U3dMeshDeclaration(
    string Name,
    bool ExcludesNormals,
    uint Faces,
    uint Positions,
    uint Normals,
    uint DiffuseColors,
    uint SpecularColors,
    uint TextureCoordinates,
    U3dShadingList[] Shadings,
    uint MinimumResolution,
    uint FinalResolution);
