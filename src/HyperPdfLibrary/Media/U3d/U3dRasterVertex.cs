// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A triangle corner on its way to the screen: its clip-space position, its lit colour and its texture coordinate.</summary>
/// <param name="Clip">The position after the view and projection transforms, before the divide by W.</param>
/// <param name="Color">The colour the corner's lighting gives.</param>
/// <param name="Uv">The texture coordinate.</param>
[DebuggerDisplay("U3dRasterVertex: {Clip}")]
internal readonly record struct U3dRasterVertex(Vector4 Clip, Vector3 Color, Vector2 Uv)
{
    /// <summary>Gets the vertex a fraction of the way from this vertex to another.</summary>
    /// <param name="other">The other vertex.</param>
    /// <param name="fraction">The fraction, from 0 to 1.</param>
    /// <returns>The vertex between them.</returns>
    internal U3dRasterVertex Lerp(in U3dRasterVertex other, float fraction) =>
        new(Vector4.Lerp(Clip, other.Clip, fraction), Vector3.Lerp(Color, other.Color, fraction), Vector2.Lerp(Uv, other.Uv, fraction));
}
