// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>What a free-form object declaration says (ECMA-363 11.5).</summary>
/// <param name="Name">The object's name.</param>
/// <param name="Attributes">The precision flags: 1 double knots, 2 double weights, 4 double curve control points.</param>
/// <param name="Surfaces">The number of surfaces.</param>
/// <param name="Faces">The number of faces, which are counted and not trimmed.</param>
/// <param name="Edges">The number of edges.</param>
/// <param name="Shadings">The shading descriptions.</param>
[DebuggerDisplay("U3dFreeFormDeclaration: {Name} {Surfaces} surfaces")]
internal sealed record U3dFreeFormDeclaration(string Name, uint Attributes, uint Surfaces, uint Faces, uint Edges, U3dShadingList[] Shadings)
{
    /// <summary>The attribute flag for 64-bit knots.</summary>
    private const uint DoubleKnotsFlag = 1;

    /// <summary>The attribute flag for 64-bit weights.</summary>
    private const uint DoubleWeightsFlag = 2;

    /// <summary>Gets a value indicating whether knots are 64-bit.</summary>
    internal bool DoubleKnots => (Attributes & DoubleKnotsFlag) != 0;

    /// <summary>Gets a value indicating whether weights are 64-bit.</summary>
    internal bool DoubleWeights => (Attributes & DoubleWeightsFlag) != 0;
}
