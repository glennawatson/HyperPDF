// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The adaptive template pixels of a generic or refinement region, as offsets from the pixel being decoded.</summary>
/// <param name="X1">The column offset of the first pixel.</param>
/// <param name="Y1">The row offset of the first pixel.</param>
/// <param name="X2">The column offset of the second pixel.</param>
/// <param name="Y2">The row offset of the second pixel.</param>
/// <param name="X3">The column offset of the third pixel.</param>
/// <param name="Y3">The row offset of the third pixel.</param>
/// <param name="X4">The column offset of the fourth pixel.</param>
/// <param name="Y4">The row offset of the fourth pixel.</param>
internal readonly record struct Jbig2AtPixels(sbyte X1, sbyte Y1, sbyte X2, sbyte Y2, sbyte X3, sbyte Y3, sbyte X4, sbyte Y4)
{
    /// <summary>The second pixel's index.</summary>
    private const int Second = 1;

    /// <summary>The third pixel's index.</summary>
    private const int Third = 2;

    /// <summary>The nominal column offset of the first pixel in generic templates 0 and 1.</summary>
    private const int WideX = 3;

    /// <summary>The nominal column offset of the first pixel in generic templates 2 and 3.</summary>
    private const int NarrowX = 2;

    /// <summary>The nominal column offset of the second pixel in generic template 0.</summary>
    private const int SecondX = -3;

    /// <summary>The row offset two rows up.</summary>
    private const int TwoUp = -2;

    /// <summary>The nominal column offset of the third pixel in generic template 0.</summary>
    private const int ThirdX = 2;

    /// <summary>The nominal column offset of the fourth pixel in generic template 0.</summary>
    private const int FourthX = -2;

    /// <summary>Gets the nominal pixels of generic template 0 (T.88 figure 3).</summary>
    internal static Jbig2AtPixels Template0 { get; } = new(WideX, -1, SecondX, -1, ThirdX, TwoUp, FourthX, TwoUp);

    /// <summary>Gets the nominal pixel of generic template 1 (T.88 figure 4).</summary>
    internal static Jbig2AtPixels Template1 { get; } = new(WideX, -1, 0, 0, 0, 0, 0, 0);

    /// <summary>Gets the nominal pixel of generic templates 2 and 3 (T.88 figures 5 and 6).</summary>
    internal static Jbig2AtPixels Template2 { get; } = new(NarrowX, -1, 0, 0, 0, 0, 0, 0);

    /// <summary>Gets the nominal pixels of refinement template 0 (T.88 figure 12).</summary>
    internal static Jbig2AtPixels Refinement { get; } = new(-1, -1, -1, -1, 0, 0, 0, 0);

    /// <summary>Gets the nominal pixels of a generic template.</summary>
    /// <param name="template">The template, 0 to 3.</param>
    /// <returns>The pixels.</returns>
    internal static Jbig2AtPixels ForTemplate(int template) => template switch
    {
        0 => Template0,
        1 => Template1,
        _ => Template2,
    };

    /// <summary>Gets the column offset of a pixel.</summary>
    /// <param name="index">The pixel, 0 to 3.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int X(int index) => index switch
    {
        0 => X1,
        Second => X2,
        Third => X3,
        _ => X4,
    };

    /// <summary>Gets the row offset of a pixel.</summary>
    /// <param name="index">The pixel, 0 to 3.</param>
    /// <returns>The offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Y(int index) => index switch
    {
        0 => Y1,
        Second => Y2,
        Third => Y3,
        _ => Y4,
    };

    /// <summary>Determines whether the pixels used by a generic template are its nominal ones (figures 3 to 6), which the fast row loops assume.</summary>
    /// <param name="template">The template, 0 to 3.</param>
    /// <returns><see langword="true"/> when the template's pixels are nominal.</returns>
    internal bool IsNominal(int template) => template == 0 ? this == Template0 : X1 == ForTemplate(template).X1 && Y1 == -1;
}
