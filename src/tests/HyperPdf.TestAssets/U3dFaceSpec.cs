// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>A face of a test mesh. Each array holds the three corners' indices, or is empty where the face has none.</summary>
/// <param name="Shading">The shader list index.</param>
/// <param name="Positions">The position indices.</param>
/// <param name="Normals">The normal indices.</param>
/// <param name="Diffuse">The diffuse colour indices.</param>
/// <param name="TextureCoordinates">The first texture layer's coordinate indices.</param>
public sealed record U3dFaceSpec(uint Shading, uint[] Positions, uint[] Normals, uint[] Diffuse, uint[] TextureCoordinates);
