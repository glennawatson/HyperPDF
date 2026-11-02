// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A material resource: how a surface answers light.</summary>
/// <param name="Name">The resource's name.</param>
/// <param name="Attributes">The flags that say which colours are used: 1 ambient, 2 diffuse, 4 specular, 8 emissive, 16 reflectivity, 32 opacity.</param>
/// <param name="Ambient">The ambient colour.</param>
/// <param name="Diffuse">The diffuse colour.</param>
/// <param name="Specular">The specular colour.</param>
/// <param name="Emissive">The emissive colour.</param>
/// <param name="Reflectivity">How shiny the surface is.</param>
/// <param name="Opacity">How opaque the surface is, from 0 to 1.</param>
[DebuggerDisplay("U3dMaterial: {Name}")]
public sealed record U3dMaterial(string Name, uint Attributes, Vector3 Ambient, Vector3 Diffuse, Vector3 Specular, Vector3 Emissive, float Reflectivity, float Opacity)
{
    /// <summary>The attribute flag for the diffuse colour.</summary>
    private const uint DiffuseFlag = 2;

    /// <summary>The attribute flag for opacity.</summary>
    private const uint OpacityFlag = 32;

    /// <summary>The grey of the material a surface uses when its shader names none the file defines.</summary>
    private const float DefaultGrey = 0.75F;

    /// <summary>Gets a value indicating whether the material's diffuse colour is used.</summary>
    public bool UsesDiffuse => (Attributes & DiffuseFlag) != 0;

    /// <summary>Gets a value indicating whether the material's opacity is used.</summary>
    public bool UsesOpacity => (Attributes & OpacityFlag) != 0;

    /// <summary>Gets the material a surface uses when its shader names none the file defines.</summary>
    internal static U3dMaterial Default { get; } = new(string.Empty, DiffuseFlag, new(DefaultGrey), new(DefaultGrey), Vector3.Zero, Vector3.Zero, 0, 1);
}
