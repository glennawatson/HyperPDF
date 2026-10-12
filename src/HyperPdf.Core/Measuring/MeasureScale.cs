// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;

namespace HyperPdf.Core.Measuring;

/// <summary>
/// How lengths on the page relate to the real world, written the way drawings state it: "1 cm = 2 m". The default
/// measures the page itself, at true size.
/// </summary>
/// <param name="PaperLength">The length on paper.</param>
/// <param name="PaperUnit">The unit of the length on paper, such as mm, cm, in or pt.</param>
/// <param name="RealLength">The length it stands for.</param>
/// <param name="RealUnit">The unit of the real length, such as mm, m, km, in, ft or mi.</param>
[DebuggerDisplay("MeasureScale: {ToString()}")]
public readonly record struct MeasureScale(double PaperLength, string PaperUnit, double RealLength, string RealUnit)
{
    /// <summary>PDF points in an inch.</summary>
    private const double PointsPerInch = 72;

    /// <summary>Each unit's size in inches.</summary>
    private static readonly FrozenDictionary<string, double> Inches = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
    {
        ["pt"] = 1 / PointsPerInch,
        ["mm"] = 1 / 25.4,
        ["cm"] = 1 / 2.54,
        ["m"] = 100 / 2.54,
        ["km"] = 100_000 / 2.54,
        ["in"] = 1,
        ["ft"] = 12,
        ["yd"] = 36,
        ["mi"] = 63_360,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets true size in millimetres.</summary>
    public static MeasureScale Metric => new(1, "mm", 1, "mm");

    /// <summary>Gets true size in inches.</summary>
    public static MeasureScale Imperial => new(1, "in", 1, "in");

    /// <summary>
    /// Gets the real length one PDF point stands for, in <see cref="RealUnit"/>: a point is 1/72 inch on paper,
    /// converted to the paper unit, then scaled by the drawing's ratio.
    /// </summary>
    public double RealPerPoint => Inches["pt"] / Inches[PaperUnit] * (RealLength / PaperLength);

    /// <summary>Reads a scale written as "1 cm = 2 m", "1:100" (same unit both sides, millimetres) or "1 in = 10 ft".</summary>
    /// <param name="text">The text.</param>
    /// <param name="scale">The scale.</param>
    /// <returns><see langword="true"/> when the text is a scale.</returns>
    public static bool TryParse(string? text, out MeasureScale scale)
    {
        scale = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var ratio = text.Split(':');
        if (ratio.Length == 2 && TryNumber(ratio[0], out var paperRatio) && TryNumber(ratio[1], out var realRatio))
        {
            scale = new(paperRatio, "mm", realRatio, "mm");
            return true;
        }

        var sides = text.Split('=');
        if (sides.Length != 2 || !TryLength(sides[0], out var paper, out var paperUnit) || !TryLength(sides[1], out var real, out var realUnit))
        {
            return false;
        }

        scale = new(paper, paperUnit, real, realUnit);
        return true;
    }

    /// <summary>Converts a length in PDF points to the real unit.</summary>
    /// <param name="points">The length in points.</param>
    /// <returns>The real length.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public double ToReal(double points) => points * RealPerPoint;

    /// <inheritdoc/>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{PaperLength:0.###} {PaperUnit} = {RealLength:0.###} {RealUnit}");

    /// <summary>Reads a number written with a full stop or a comma as the decimal mark.</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when it is a positive number.</returns>
    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;

    /// <summary>Reads a length with its unit, such as "2 m" or "1in".</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The number.</param>
    /// <param name="unit">The unit.</param>
    /// <returns><see langword="true"/> when it is a positive length in a known unit.</returns>
    private static bool TryLength(string text, out double value, out string unit)
    {
        var trimmed = text.Trim();
        var split = 0;
        while (split < trimmed.Length && (char.IsAsciiDigit(trimmed[split]) || trimmed[split] is '.' or ','))
        {
            split++;
        }

        unit = trimmed[split..].Trim().ToLowerInvariant();
        return TryNumber(trimmed[..split], out value) && Inches.ContainsKey(unit);
    }
}
