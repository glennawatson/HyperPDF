// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A lit texture shader: a material, and the textures drawn over it.</summary>
/// <param name="Name">The shader's name.</param>
/// <param name="Attributes">The flags: 1 lighting enabled, 2 alpha test enabled, 4 use vertex colour.</param>
/// <param name="Material">The name of the material the shader uses.</param>
/// <param name="Textures">The names of the textures of its active layers, in layer order.</param>
[DebuggerDisplay("U3dShader: {Name}")]
public sealed record U3dShader(string Name, uint Attributes, string Material, string[] Textures)
{
    /// <summary>The attribute flag for lighting.</summary>
    private const uint LightingFlag = 1;

    /// <summary>The attribute flag for vertex colour.</summary>
    private const uint VertexColorFlag = 4;

    /// <summary>Gets a value indicating whether the shader lights the surface.</summary>
    public bool IsLit => (Attributes & LightingFlag) != 0;

    /// <summary>Gets a value indicating whether the shader multiplies in each vertex's own colour.</summary>
    public bool UsesVertexColor => (Attributes & VertexColorFlag) != 0;
}
