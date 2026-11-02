// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>How a face is coloured: the colours it answers light with, how opaque it is, and the texture drawn over it.</summary>
/// <param name="Ambient">The colour under ambient light.</param>
/// <param name="Diffuse">The colour under direct light.</param>
/// <param name="Emissive">The colour the surface gives off.</param>
/// <param name="Opacity">How opaque the surface is, from 0 to 1.</param>
/// <param name="Texture">The first texture layer, or <see langword="null"/>.</param>
/// <param name="VertexColor"><see langword="true"/> when a corner's own diffuse colour replaces the material's.</param>
[DebuggerDisplay("U3dSurfaceStyle: {Diffuse}")]
internal sealed record U3dSurfaceStyle(Vector3 Ambient, Vector3 Diffuse, Vector3 Emissive, float Opacity, U3dTexture? Texture, bool VertexColor)
{
    /// <summary>Gets the style of a face whose shader the file does not name.</summary>
    internal static U3dSurfaceStyle Default { get; } = Of(U3dMaterial.Default, null, false);

    /// <summary>Makes the style a material and a texture give.</summary>
    /// <param name="material">The material.</param>
    /// <param name="texture">The texture of the shader's first layer, or <see langword="null"/>.</param>
    /// <param name="vertexColor">Whether the shader uses each corner's own colour.</param>
    /// <returns>The style.</returns>
    internal static U3dSurfaceStyle Of(U3dMaterial material, U3dTexture? texture, bool vertexColor) =>
        new(material.Ambient, material.Diffuse, material.Emissive, material.UsesOpacity ? material.Opacity : 1, texture, vertexColor);
}
