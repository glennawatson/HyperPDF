// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Describes the width policy of a single column in a <see cref="PdfFlowTable"/>: automatic content sizing, a fixed point
/// width, or a proportional (relative/star) share of the remaining available width.
/// </summary>
/// <param name="Kind">How the column width is interpreted.</param>
/// <param name="Value">The width in points for <see cref="PdfTableColumnWidthKind.Fixed"/>, or the weight for <see cref="PdfTableColumnWidthKind.Proportional"/>.</param>
[DebuggerDisplay("PdfTableColumnWidth: {Kind} ({Value})")]
public readonly record struct PdfTableColumnWidth(PdfTableColumnWidthKind Kind, float Value)
{
    /// <summary>Gets a column width that sizes the column to its content.</summary>
    public static PdfTableColumnWidth Auto => new(PdfTableColumnWidthKind.Auto, 0F);

    /// <summary>Creates a fixed column width in PDF points.</summary>
    /// <param name="points">The column width in points; must be finite and greater than zero.</param>
    /// <returns>A fixed <see cref="PdfTableColumnWidth"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="points"/> is zero, negative, infinite or <see cref="float.NaN"/>.</exception>
    public static PdfTableColumnWidth Fixed(float points)
    {
        ValidatePositiveFinite(points, nameof(points));
        return new(PdfTableColumnWidthKind.Fixed, points);
    }

    /// <summary>
    /// Creates a proportional (star/relative) column width that receives <c>weight / totalWeight</c> of the space
    /// remaining after fixed and automatic columns. Weights may be expressed as fractions (such as <c>0.2f</c> and
    /// <c>0.8f</c>), percentages (such as <c>20f</c> and <c>80f</c>), or star ratios (such as <c>1f</c> and <c>3f</c>).
    /// </summary>
    /// <param name="weight">The relative weight; must be finite and greater than zero.</param>
    /// <returns>A proportional <see cref="PdfTableColumnWidth"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is zero, negative, infinite or <see cref="float.NaN"/>.</exception>
    public static PdfTableColumnWidth Proportional(float weight)
    {
        ValidatePositiveFinite(weight, nameof(weight));
        return new(PdfTableColumnWidthKind.Proportional, weight);
    }

    /// <summary>
    /// Creates a relative column width that receives <c>weight / totalWeight</c> of the space remaining after fixed and
    /// automatic columns. Equivalent to <see cref="Proportional(float)"/>.
    /// </summary>
    /// <param name="weight">The relative weight or percentage; must be finite and greater than zero.</param>
    /// <returns>A proportional <see cref="PdfTableColumnWidth"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="weight"/> is zero, negative, infinite or <see cref="float.NaN"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfTableColumnWidth Relative(float weight) => Proportional(weight);

    /// <summary>Validates this column width specification.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The kind or value is invalid.</exception>
    internal void Validate()
    {
        switch (Kind)
        {
            case PdfTableColumnWidthKind.Auto:
            {
                return;
            }

            case PdfTableColumnWidthKind.Fixed or PdfTableColumnWidthKind.Proportional:
            {
                ValidatePositiveFinite(Value, nameof(Value));
                return;
            }

            default:
            {
                throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unknown table column width kind.");
            }
        }
    }

    /// <summary>Validates that a float value is finite and strictly positive.</summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not finite and positive.</exception>
    private static void ValidatePositiveFinite(float value, string paramName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0F)
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The column width value must be a finite positive number.");
        }
    }
}
