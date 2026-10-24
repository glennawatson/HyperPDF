// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Converts lengths between <see cref="PdfUnit"/> units and points.</summary>
public static class PdfUnits
{
    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72F;

    /// <summary>The millimetres in an inch.</summary>
    private const float MillimetersPerInch = 25.4F;

    /// <summary>The millimetres in a centimetre.</summary>
    private const float MillimetersPerCentimeter = 10F;

    /// <summary>The points in a pica.</summary>
    private const float PointsPerPica = 12F;

    /// <summary>Converts a length to points.</summary>
    /// <param name="value">The length.</param>
    /// <param name="unit">The unit of <paramref name="value"/>.</param>
    /// <returns>The length in points.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unit"/> is not a defined unit.</exception>
    public static float ToPoints(float value, PdfUnit unit) => unit switch
    {
        PdfUnit.Point => value,
        PdfUnit.Inch => value * PointsPerInch,
        PdfUnit.Millimeter => value * PointsPerInch / MillimetersPerInch,
        PdfUnit.Centimeter => value * MillimetersPerCentimeter * PointsPerInch / MillimetersPerInch,
        PdfUnit.Pica => value * PointsPerPica,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "The unit is not defined."),
    };

    /// <summary>Converts a length in points to another unit.</summary>
    /// <param name="points">The length in points.</param>
    /// <param name="unit">The unit to convert to.</param>
    /// <returns>The length in <paramref name="unit"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="unit"/> is not a defined unit.</exception>
    public static float FromPoints(float points, PdfUnit unit) => unit switch
    {
        PdfUnit.Point => points,
        PdfUnit.Inch => points / PointsPerInch,
        PdfUnit.Millimeter => points * MillimetersPerInch / PointsPerInch,
        PdfUnit.Centimeter => points * MillimetersPerInch / PointsPerInch / MillimetersPerCentimeter,
        PdfUnit.Pica => points / PointsPerPica,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "The unit is not defined."),
    };
}
