// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdf.TestAssets;

/// <summary>A free-form surface for a test model.</summary>
/// <param name="IsBezier"><see langword="true"/> for a Bezier surface, <see langword="false"/> for a B-spline surface.</param>
/// <param name="IsRational"><see langword="true"/> when control points carry a weight.</param>
/// <param name="DegreeU">The degree in the U direction.</param>
/// <param name="DegreeV">The degree in the V direction.</param>
/// <param name="KnotsU">The unique knots in the U direction, or the two bounds when <paramref name="UniformU"/> is set.</param>
/// <param name="MultiplicitiesU">How often each U knot repeats; empty when uniform.</param>
/// <param name="KnotsV">The unique knots in the V direction, or the two bounds when <paramref name="UniformV"/> is set.</param>
/// <param name="MultiplicitiesV">How often each V knot repeats; empty when uniform.</param>
/// <param name="UniformU"><see langword="true"/> to store only the bounds of the U knots.</param>
/// <param name="UniformV"><see langword="true"/> to store only the bounds of the V knots.</param>
/// <param name="CountU">The control points in the U direction.</param>
/// <param name="CountV">The control points in the V direction.</param>
/// <param name="Points">The control points as x, y, z and weight, with the V index changing fastest.</param>
/// <param name="Shading">The shader list index.</param>
/// <param name="IndependentKnots"><see langword="true"/> to set the independent knots flag, which the reader does not decode.</param>
public sealed record U3dSurfaceSpec(
    bool IsBezier,
    bool IsRational,
    uint DegreeU,
    uint DegreeV,
    float[] KnotsU,
    uint[] MultiplicitiesU,
    float[] KnotsV,
    uint[] MultiplicitiesV,
    bool UniformU,
    bool UniformV,
    uint CountU,
    uint CountV,
    Vector4[] Points,
    uint Shading,
    bool IndependentKnots);
