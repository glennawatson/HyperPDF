// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>A 3 x 3 matrix, row by row, for colour conversions.</summary>
/// <param name="A">The first row, first column.</param>
/// <param name="B">The first row, second column.</param>
/// <param name="C">The first row, third column.</param>
/// <param name="D">The second row, first column.</param>
/// <param name="E">The second row, second column.</param>
/// <param name="F">The second row, third column.</param>
/// <param name="G">The third row, first column.</param>
/// <param name="H">The third row, second column.</param>
/// <param name="I">The third row, third column.</param>
internal readonly record struct Matrix3(float A, float B, float C, float D, float E, float F, float G, float H, float I)
{
    /// <summary>The smallest determinant treated as invertible.</summary>
    private const float MinDeterminant = 1e-6F;

    /// <summary>The offset of the second group of three numbers.</summary>
    private const int SecondGroup = 3;

    /// <summary>The offset of the third group of three numbers.</summary>
    private const int ThirdGroup = 6;

    /// <summary>The offset of the second number in a group.</summary>
    private const int SecondInGroup = 1;

    /// <summary>The offset of the third number in a group.</summary>
    private const int ThirdInGroup = 2;

    /// <summary>Creates a matrix from nine numbers listed column by column, as a PDF CalRGB /Matrix is.</summary>
    /// <param name="v">The nine numbers.</param>
    /// <returns>The matrix.</returns>
    internal static Matrix3 FromColumns(ReadOnlySpan<float> v) => new(
        v[0],
        v[SecondGroup],
        v[ThirdGroup],
        v[SecondInGroup],
        v[SecondGroup + SecondInGroup],
        v[ThirdGroup + SecondInGroup],
        v[ThirdInGroup],
        v[SecondGroup + ThirdInGroup],
        v[ThirdGroup + ThirdInGroup]);

    /// <summary>Gets the product of this matrix and another: the result applies <paramref name="other"/> first.</summary>
    /// <param name="other">The matrix applied first.</param>
    /// <returns>The product.</returns>
    internal Matrix3 Multiply(in Matrix3 other) => new(
        (A * other.A) + (B * other.D) + (C * other.G),
        (A * other.B) + (B * other.E) + (C * other.H),
        (A * other.C) + (B * other.F) + (C * other.I),
        (D * other.A) + (E * other.D) + (F * other.G),
        (D * other.B) + (E * other.E) + (F * other.H),
        (D * other.C) + (E * other.F) + (F * other.I),
        (G * other.A) + (H * other.D) + (I * other.G),
        (G * other.B) + (H * other.E) + (I * other.H),
        (G * other.C) + (H * other.F) + (I * other.I));

    /// <summary>Gets the inverse; a singular matrix has none, so it gives the zero matrix, which turns a colour black instead of failing.</summary>
    /// <returns>The inverse.</returns>
    internal Matrix3 Inverse()
    {
        var determinant = (A * ((E * I) - (F * H))) - (B * ((I * D) - (F * G))) + (C * ((D * H) - (E * G)));
        return MathF.Abs(determinant) < MinDeterminant
            ? default
            : new(
                ((E * I) - (F * H)) / determinant,
                -((B * I) - (C * H)) / determinant,
                ((B * F) - (C * E)) / determinant,
                -((D * I) - (F * G)) / determinant,
                ((A * I) - (C * G)) / determinant,
                -((A * F) - (C * D)) / determinant,
                ((D * H) - (E * G)) / determinant,
                -((A * H) - (B * G)) / determinant,
                ((A * E) - (B * D)) / determinant);
    }

    /// <summary>Multiplies the matrix by a column vector.</summary>
    /// <param name="x">The first component.</param>
    /// <param name="y">The second component.</param>
    /// <param name="z">The third component.</param>
    /// <returns>The three result components.</returns>
    internal Float3 Transform(float x, float y, float z) =>
        new((A * x) + (B * y) + (C * z), (D * x) + (E * y) + (F * z), (G * x) + (H * y) + (I * z));
}
