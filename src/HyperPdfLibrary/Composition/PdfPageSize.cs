// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Composition;

/// <summary>The size of a sheet of paper, upright, in points. Named sizes follow ISO 216 and the common North American sizes.</summary>
/// <param name="Width">The width in points when the sheet is upright.</param>
/// <param name="Height">The height in points when the sheet is upright.</param>
[DebuggerDisplay("PdfPageSize: {Width} x {Height} pt")]
public readonly record struct PdfPageSize(float Width, float Height)
{
    /// <summary>The numbers a size is made of: a width and a height.</summary>
    private const int Dimensions = 2;

    /// <summary>The width and height in millimetres of each named ISO size, in the order of <see cref="IsoSize"/>.</summary>
    private static readonly float[] IsoMillimeters = [841F, 1189F, 594F, 841F, 420F, 594F, 297F, 420F, 210F, 297F, 148F, 210F, 105F, 148F, 74F, 105F, 52F, 74F, 250F, 353F, 176F, 250F, 125F, 176F];

    /// <summary>The width and height in inches of each named North American size, in the order of <see cref="AmericanSize"/>.</summary>
    private static readonly float[] AmericanInches = [8.5F, 11F, 8.5F, 14F, 11F, 17F, 7.25F, 10.5F, 5.5F, 8.5F];

    /// <summary>The named ISO sizes, in the order of their rows in the table.</summary>
    private enum IsoSize
    {
        /// <summary>ISO A0.</summary>
        A0 = 0,

        /// <summary>ISO A1.</summary>
        A1 = 1,

        /// <summary>ISO A2.</summary>
        A2 = 2,

        /// <summary>ISO A3.</summary>
        A3 = 3,

        /// <summary>ISO A4.</summary>
        A4 = 4,

        /// <summary>ISO A5.</summary>
        A5 = 5,

        /// <summary>ISO A6.</summary>
        A6 = 6,

        /// <summary>ISO A7.</summary>
        A7 = 7,

        /// <summary>ISO A8.</summary>
        A8 = 8,

        /// <summary>ISO B4.</summary>
        B4 = 9,

        /// <summary>ISO B5.</summary>
        B5 = 10,

        /// <summary>ISO B6.</summary>
        B6 = 11,
    }

    /// <summary>The named North American sizes, in the order of their rows in the table.</summary>
    private enum AmericanSize
    {
        /// <summary>US Letter.</summary>
        Letter = 0,

        /// <summary>US Legal.</summary>
        Legal = 1,

        /// <summary>US Tabloid.</summary>
        Tabloid = 2,

        /// <summary>US Executive.</summary>
        Executive = 3,

        /// <summary>US Statement.</summary>
        Statement = 4,
    }

    /// <summary>Gets ISO A0, 841 by 1189 mm.</summary>
    public static PdfPageSize A0 => FromIso(IsoSize.A0);

    /// <summary>Gets ISO A1, 594 by 841 mm.</summary>
    public static PdfPageSize A1 => FromIso(IsoSize.A1);

    /// <summary>Gets ISO A2, 420 by 594 mm.</summary>
    public static PdfPageSize A2 => FromIso(IsoSize.A2);

    /// <summary>Gets ISO A3, 297 by 420 mm.</summary>
    public static PdfPageSize A3 => FromIso(IsoSize.A3);

    /// <summary>Gets ISO A4, 210 by 297 mm.</summary>
    public static PdfPageSize A4 => FromIso(IsoSize.A4);

    /// <summary>Gets ISO A5, 148 by 210 mm.</summary>
    public static PdfPageSize A5 => FromIso(IsoSize.A5);

    /// <summary>Gets ISO A6, 105 by 148 mm.</summary>
    public static PdfPageSize A6 => FromIso(IsoSize.A6);

    /// <summary>Gets ISO A7, 74 by 105 mm.</summary>
    public static PdfPageSize A7 => FromIso(IsoSize.A7);

    /// <summary>Gets ISO A8, 52 by 74 mm.</summary>
    public static PdfPageSize A8 => FromIso(IsoSize.A8);

    /// <summary>Gets ISO B4, 250 by 353 mm.</summary>
    public static PdfPageSize B4 => FromIso(IsoSize.B4);

    /// <summary>Gets ISO B5, 176 by 250 mm.</summary>
    public static PdfPageSize B5 => FromIso(IsoSize.B5);

    /// <summary>Gets ISO B6, 125 by 176 mm.</summary>
    public static PdfPageSize B6 => FromIso(IsoSize.B6);

    /// <summary>Gets US Letter, 8.5 by 11 inches.</summary>
    public static PdfPageSize Letter => FromAmerican(AmericanSize.Letter);

    /// <summary>Gets US Legal, 8.5 by 14 inches.</summary>
    public static PdfPageSize Legal => FromAmerican(AmericanSize.Legal);

    /// <summary>Gets US Tabloid, 11 by 17 inches.</summary>
    public static PdfPageSize Tabloid => FromAmerican(AmericanSize.Tabloid);

    /// <summary>Gets US Executive, 7.25 by 10.5 inches.</summary>
    public static PdfPageSize Executive => FromAmerican(AmericanSize.Executive);

    /// <summary>Gets US Statement, 5.5 by 8.5 inches.</summary>
    public static PdfPageSize Statement => FromAmerican(AmericanSize.Statement);

    /// <summary>Makes a size from millimetres.</summary>
    /// <param name="width">The width in millimetres.</param>
    /// <param name="height">The height in millimetres.</param>
    /// <returns>The size.</returns>
    public static PdfPageSize FromMillimeters(float width, float height) =>
        new(PdfUnits.ToPoints(width, PdfUnit.Millimeter), PdfUnits.ToPoints(height, PdfUnit.Millimeter));

    /// <summary>Makes a size from inches.</summary>
    /// <param name="width">The width in inches.</param>
    /// <param name="height">The height in inches.</param>
    /// <returns>The size.</returns>
    public static PdfPageSize FromInches(float width, float height) =>
        new(PdfUnits.ToPoints(width, PdfUnit.Inch), PdfUnits.ToPoints(height, PdfUnit.Inch));

    /// <summary>Gets a named ISO size.</summary>
    /// <param name="size">The size.</param>
    /// <returns>The size in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfPageSize FromIso(IsoSize size) => FromMillimeters(IsoMillimeters[(int)size * Dimensions], IsoMillimeters[((int)size * Dimensions) + 1]);

    /// <summary>Gets a named North American size.</summary>
    /// <param name="size">The size.</param>
    /// <returns>The size in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfPageSize FromAmerican(AmericanSize size) => FromInches(AmericanInches[(int)size * Dimensions], AmericanInches[((int)size * Dimensions) + 1]);
}
