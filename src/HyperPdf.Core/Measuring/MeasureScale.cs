// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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

    /// <summary>The characters of a number read on the stack.</summary>
    private const int StackNumberChars = 64;

    /// <summary>Each unit's size in inches.</summary>
    private static readonly Dictionary<string, UnitFactor> Inches = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pt"] = new("pt", 1 / PointsPerInch),
        ["mm"] = new("mm", 1 / 25.4),
        ["cm"] = new("cm", 1 / 2.54),
        ["m"] = new("m", 100 / 2.54),
        ["km"] = new("km", 100_000 / 2.54),
        ["in"] = new("in", 1),
        ["ft"] = new("ft", 12),
        ["yd"] = new("yd", 36),
        ["mi"] = new("mi", 63_360),
    };

    /// <summary>Gets true size in millimetres.</summary>
    public static MeasureScale Metric => new(1, "mm", 1, "mm");

    /// <summary>Gets true size in inches.</summary>
    public static MeasureScale Imperial => new(1, "in", 1, "in");

    /// <summary>
    /// Gets the real length one PDF point stands for, in <see cref="RealUnit"/>: a point is 1/72 inch on paper,
    /// converted to the paper unit, then scaled by the drawing's ratio.
    /// </summary>
    public double RealPerPoint => Inches["pt"].Inches / Inches[PaperUnit].Inches * (RealLength / PaperLength);

    /// <summary>Reads a scale written as "1 cm = 2 m", "1:100" (same unit both sides, millimetres) or "1 in = 10 ft".</summary>
    /// <param name="text">The text.</param>
    /// <param name="scale">The scale.</param>
    /// <returns><see langword="true"/> when the text is a scale.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static bool TryParse(string? text, out MeasureScale scale) => TryParse(text.AsSpan(), out scale);

    /// <summary>Reads a scale written as "1 cm = 2 m", "1:100" (same unit both sides, millimetres) or "1 in = 10 ft".</summary>
    /// <param name="text">The text.</param>
    /// <param name="scale">The scale.</param>
    /// <returns><see langword="true"/> when the text is a scale.</returns>
    public static bool TryParse(ReadOnlySpan<char> text, out MeasureScale scale)
    {
        scale = default;
        if (text.IsWhiteSpace())
        {
            return false;
        }

        // Exactly one colon splits a ratio such as 1:100.
        var colon = text.IndexOf(':');
        if (colon >= 0 && text[(colon + 1)..].IndexOf(':') < 0 && TryNumber(text[..colon], out var paperRatio) && TryNumber(text[(colon + 1)..], out var realRatio))
        {
            scale = new(paperRatio, "mm", realRatio, "mm");
            return true;
        }

        var equals = text.IndexOf('=');
        if (equals < 0 || text[(equals + 1)..].IndexOf('=') >= 0 || !TryLength(text[..equals], out var paper, out var paperUnit) || !TryLength(text[(equals + 1)..], out var real, out var realUnit))
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
    private static bool TryNumber(ReadOnlySpan<char> text, out double value)
    {
        var trimmed = text.Trim();
        char[]? rented = null;
        var digits = trimmed.Length <= StackNumberChars ? stackalloc char[StackNumberChars] : (rented = ArrayPool<char>.Shared.Rent(trimmed.Length));
        try
        {
            trimmed.Replace(digits, ',', '.');
            return double.TryParse(digits[..trimmed.Length], NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Reads a length with its unit, such as "2 m" or "1in".</summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The number.</param>
    /// <param name="unit">The unit, spelled as the table of units spells it.</param>
    /// <returns><see langword="true"/> when it is a positive length in a known unit.</returns>
    private static bool TryLength(ReadOnlySpan<char> text, out double value, out string unit)
    {
        var trimmed = text.Trim();
        var split = 0;
        while (split < trimmed.Length && (char.IsAsciiDigit(trimmed[split]) || trimmed[split] is '.' or ','))
        {
            split++;
        }

        var known = Inches.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(trimmed[split..].Trim(), out var factor);
        unit = known ? factor.Name : string.Empty;
        return TryNumber(trimmed[..split], out value) && known;
    }

    /// <summary>A known unit's spelling and size in inches.</summary>
    /// <param name="Name">The canonical spelling.</param>
    /// <param name="Inches">The length in inches.</param>
    private readonly record struct UnitFactor(string Name, double Inches);
}
