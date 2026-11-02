// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A light resource.</summary>
/// <param name="Name">The resource's name.</param>
/// <param name="Attributes">The flags: 1 enabled, 2 specular, 4 spot decay.</param>
/// <param name="Kind">The kind of light.</param>
/// <param name="Color">The red, green and blue of the light.</param>
/// <param name="Attenuation">The constant, linear and quadratic attenuation factors.</param>
/// <param name="SpotAngle">The angle of a spot light's cone.</param>
/// <param name="Intensity">The factor the light's effect is multiplied by.</param>
[DebuggerDisplay("U3dLight: {Name} {Kind}")]
public sealed record U3dLight(string Name, uint Attributes, U3dLightKind Kind, Vector3 Color, Vector3 Attenuation, float SpotAngle, float Intensity)
{
    /// <summary>Gets a value indicating whether the light is switched on.</summary>
    public bool IsEnabled => (Attributes & 1) != 0;
}
