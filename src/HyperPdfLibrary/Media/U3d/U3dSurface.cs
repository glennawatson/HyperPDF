// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A free-form surface: Bezier or B-spline, rational or not, as ECMA-363 clause 11 stores it.</summary>
/// <param name="IsBezier"><see langword="true"/> for a Bezier surface and <see langword="false"/> for a B-spline surface.</param>
/// <param name="DegreeU">The degree in the U direction.</param>
/// <param name="DegreeV">The degree in the V direction.</param>
/// <param name="KnotsU">The knots in the U direction, with repeats written out.</param>
/// <param name="KnotsV">The knots in the V direction, with repeats written out.</param>
/// <param name="CountU">The control points in the U direction.</param>
/// <param name="CountV">The control points in the V direction.</param>
/// <param name="Points">The control points as x, y, z and weight, with the V index changing fastest.</param>
/// <param name="Shading">The index of the shader list the surface uses.</param>
[DebuggerDisplay("U3dSurface: {CountU}x{CountV} degree {DegreeU},{DegreeV}")]
internal sealed record U3dSurface(
    bool IsBezier,
    int DegreeU,
    int DegreeV,
    double[] KnotsU,
    double[] KnotsV,
    int CountU,
    int CountV,
    Vector4[] Points,
    int Shading);
