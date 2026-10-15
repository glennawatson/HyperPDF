// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Checks that <see cref="PdfNumber.Format(double, Span{byte})"/> writes the same bytes as the general six-decimal format it replaced on the common path.</summary>
public sealed class NumberFormatDifferentialTests
{
    /// <summary>The seed of the generated values.</summary>
    private const ulong Seed = 0x2026_0101UL;

    /// <summary>How many values each generated range checks.</summary>
    private const int Samples = 400_000;

    /// <summary>The format the general path uses.</summary>
    private const string ReferenceFormat = "0.######";

    /// <summary>The size of the buffers.</summary>
    private const int BufferSize = 64;

    /// <summary>The most differences kept for the failure message.</summary>
    private const int MaxReported = 20;

    /// <summary>The scale of the six-decimal ties.</summary>
    private const double Million = 1_000_000;

    /// <summary>The whole parts tried beside the ties.</summary>
    private const int WholeParts = 100_000;

    /// <summary>The number of six-decimal fractions.</summary>
    private const int Fractions = 1_000_000;

    /// <summary>The half that makes a value a tie.</summary>
    private const double Half = 0.5;

    /// <summary>The widest span of decimal exponents generated.</summary>
    private const double ExponentSpan = 14;

    /// <summary>The smallest decimal exponent generated.</summary>
    private const double ExponentFloor = 8;

    /// <summary>The most decimals a value is rounded to before it is compared.</summary>
    private const int RoundedDecimals = 8;

    /// <summary>The most decimals a float source is rounded to before it is compared.</summary>
    private const int FloatDecimals = 4;

    /// <summary>The number of representable neighbours checked on each side of a tie.</summary>
    private const int Neighbours = 3;

    /// <summary>The multiplier of the generator.</summary>
    private const ulong Gamma = 0x9E37_79B9_7F4A_7C15UL;

    /// <summary>The first mixing multiplier.</summary>
    private const ulong MixOne = 0xBF58_476D_1CE4_E5B9UL;

    /// <summary>The second mixing multiplier.</summary>
    private const ulong MixTwo = 0x94D0_49BB_1331_11EBUL;

    /// <summary>The shift of the first mixing step.</summary>
    private const int ShiftOne = 30;

    /// <summary>The shift of the second mixing step.</summary>
    private const int ShiftTwo = 27;

    /// <summary>The shift of the last mixing step.</summary>
    private const int ShiftThree = 31;

    /// <summary>The base of the generated magnitudes.</summary>
    private const double Base = 10;

    /// <summary>The bits of the generator's word.</summary>
    private const int WordBits = 64;

    /// <summary>The bits kept for a fraction of a unit.</summary>
    private const int UnitBits = 53;

    /// <summary>Compares generated values across magnitudes, both signs, as doubles and as floats.</summary>
    /// <returns>A task that completes when the check ends.</returns>
    [Test]
    public async Task RandomValuesMatchTheGeneralFormat()
    {
        var state = Seed;
        var mismatches = new List<string>();
        for (var i = 0; i < Samples; i++)
        {
            var magnitude = Math.Pow(Base, (NextUnit(ref state) * ExponentSpan) - ExponentFloor);
            var sign = NextUnit(ref state) < Half ? -1 : 1;
            var value = sign * magnitude * NextUnit(ref state);
            Compare(value, mismatches);
            Compare((float)value, mismatches);
            Compare(Math.Round(value, (int)(NextUnit(ref state) * RoundedDecimals)), mismatches);
            Compare((float)Math.Round(value, (int)(NextUnit(ref state) * FloatDecimals)), mismatches);
        }

        await Assert.That(mismatches).IsEmpty();
    }

    /// <summary>Compares values on and beside the rounding ties of the sixth decimal.</summary>
    /// <returns>A task that completes when the check ends.</returns>
    [Test]
    public async Task ValuesNearRoundingTiesMatchTheGeneralFormat()
    {
        var state = Seed + 1;
        var mismatches = new List<string>();
        for (var i = 0; i < Samples; i++)
        {
            var whole = (int)(NextUnit(ref state) * WholeParts);
            var tie = (((int)(NextUnit(ref state) * Fractions) + Half) / Million) + whole;
            for (var step = -Neighbours; step <= Neighbours; step++)
            {
                var value = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(tie) + step);
                Compare(value, mismatches);
                Compare(-value, mismatches);
                Compare((float)value, mismatches);
            }
        }

        await Assert.That(mismatches).IsEmpty();
    }

    /// <summary>Compares fixed edge values.</summary>
    /// <returns>A task that completes when the check ends.</returns>
    [Test]
    public async Task EdgeValuesMatchTheGeneralFormat()
    {
        double[] edges =
        [
            0, -0.0, 1, -1, 0.5, -0.5, 0.1, 0.000001, 0.0000005, 0.0000004, -0.0000004, -0.0000005, 0.0000015, 0.9999995, 0.9999994,
            99999.9999995, 99999.9999994, 99999.999999, 100_000, 99999.5, 100_000.5, 100_000.0000005, 1e9, 1e15, 1e19, -1e19, 1e300,
            double.Epsilon, double.MaxValue, double.MinValue, 1e-7, 1e-10, 595.276, 841.89, 0.3, 0.7, 2.675, 1.005, 1.0000005,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity, 12345.678901, 0.123456, 0.1234565, 0.1234575,
        ];
        var mismatches = new List<string>();
        foreach (var value in edges)
        {
            Compare(value, mismatches);
            Compare((float)value, mismatches);
        }

        await Assert.That(mismatches).IsEmpty();
    }

    /// <summary>Steps a SplitMix64 generator and scales its output to the range 0 up to 1.</summary>
    /// <param name="state">The generator's state.</param>
    /// <returns>A value from 0 up to, not including, 1.</returns>
    private static double NextUnit(ref ulong state)
    {
        state += Gamma;
        var z = state;
        z = (z ^ (z >> ShiftOne)) * MixOne;
        z = (z ^ (z >> ShiftTwo)) * MixTwo;
        z ^= z >> ShiftThree;
        return (z >> (WordBits - UnitBits)) / (double)(1UL << UnitBits);
    }

    /// <summary>Formats a value and compares it with the reference, noting a difference.</summary>
    /// <param name="value">The value.</param>
    /// <param name="mismatches">Receives a description of a difference.</param>
    private static void Compare(double value, List<string> mismatches)
    {
        Span<byte> actual = stackalloc byte[BufferSize];
        var written = PdfNumber.Format(value, actual);
        var expected = Reference(value);
        var text = Encoding.ASCII.GetString(actual[..written]);
        if (text != expected && mismatches.Count < MaxReported)
        {
            mismatches.Add(FormattableString.Invariant($"{value:R}: wrote {text}, expected {expected}"));
        }
    }

    /// <summary>Formats a number the way the general path did.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The text.</returns>
    private static string Reference(double number)
    {
        if (!double.IsFinite(number))
        {
            number = 0;
        }

        if (double.IsInteger(number))
        {
            return ReferenceWhole(number).ToString(CultureInfo.InvariantCulture);
        }

        var text = number.ToString(ReferenceFormat, CultureInfo.InvariantCulture);
        return text == "-0" ? "0" : text;
    }

    /// <summary>Clamps a whole number to the range of a long the way the general path did.</summary>
    /// <param name="number">The whole number.</param>
    /// <returns>The clamped value.</returns>
    private static long ReferenceWhole(double number)
    {
        if (Math.Abs(number) < long.MaxValue)
        {
            return (long)number;
        }

        return number < 0 ? -long.MaxValue : long.MaxValue;
    }
}
