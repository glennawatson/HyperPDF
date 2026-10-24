// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The fixed-point arithmetic of the SILK layer (RFC 6716 section 4.2). SILK is specified in integers, and the decoder's output has to
/// agree bit for bit with the reference, so these helpers do the same rounding and wrapping as the reference macros.
/// </summary>
internal static class SilkMath
{
    /// <summary>The bits in the half of a 32 bit number that the multiply-by-16-bit operations work on.</summary>
    private const int HalfBits = 16;

    /// <summary>The fractional bits of the inverse of a number.</summary>
    private const int InverseBits = 29;

    /// <summary>The largest 32 bit number divided by four, the numerator of the first inverse estimate.</summary>
    private const int InverseNumerator = int.MaxValue >> 2;

    /// <summary>The fractional bits an exact inverse is computed in.</summary>
    private const int FullInverseBits = 61;

    /// <summary>The bits the residual of an inverse is scaled by.</summary>
    private const int ResidualShift = 3;

    /// <summary>The bits of the number the square root estimate takes its fraction from.</summary>
    private const int SqrtFractionShift = 24;

    /// <summary>The mask of the 7 fraction bits.</summary>
    private const int SqrtFractionMask = 0x7F;

    /// <summary>The square root of one in Q15.</summary>
    private const int SqrtOne = 32_768;

    /// <summary>The bits in a 32 bit number.</summary>
    private const int WordBits = 32;

    /// <summary>The square root of two in Q15.</summary>
    private const int SqrtTwo = 46_214;

    /// <summary>The slope that corrects the square root estimate across a fraction.</summary>
    private const int SqrtSlope = 213;

    /// <summary>The multiplier of the pseudo random number generator.</summary>
    private const int RandMultiplier = 196_314_165;

    /// <summary>The increment of the pseudo random number generator.</summary>
    private const int RandIncrement = 907_633_515;

    /// <summary>The largest log value the exponential accepts before it saturates, in Q7.</summary>
    private const int LogCeiling = 3967;

    /// <summary>The bits of the fraction of a log value.</summary>
    private const int LogFractionBits = 7;

    /// <summary>The mask of the fraction of a log value.</summary>
    private const int LogFractionMask = 0x7F;

    /// <summary>The log value below which the exponential uses its larger shift.</summary>
    private const int LogKnee = 2048;

    /// <summary>The slope of the parabola that corrects the exponential between integers.</summary>
    private const int LogParabola = -174;

    /// <summary>The 16 bit signed limit.</summary>
    private const int Short16Max = short.MaxValue;

    /// <summary>Multiplies a 32 bit number by the low 16 bits of another, and shifts the 48 bit result down by 16.</summary>
    /// <param name="a">The 32 bit number.</param>
    /// <param name="b">The number whose low 16 bits are the multiplier.</param>
    /// <returns>The product, rounded towards negative infinity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyWordBottom(int a, int b) => (int)(((long)a * (short)b) >> HalfBits);

    /// <summary>Adds the result of <see cref="MultiplyWordBottom"/> to a number.</summary>
    /// <param name="a">The number to add to.</param>
    /// <param name="b">The 32 bit number.</param>
    /// <param name="c">The number whose low 16 bits are the multiplier.</param>
    /// <returns>The sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyAddWordBottom(int a, int b, int c) => unchecked(a + MultiplyWordBottom(b, c));

    /// <summary>Multiplies two 32 bit numbers and shifts the product down by 16, the way the reference does it in two parts.</summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyWord(int a, int b) => unchecked(MultiplyWordBottom(a, b) + (a * RoundShift(b, HalfBits)));

    /// <summary>Adds the result of <see cref="MultiplyWord"/> to a number, with the reference's rounding.</summary>
    /// <param name="a">The number to add to.</param>
    /// <param name="b">The first number.</param>
    /// <param name="c">The second number.</param>
    /// <returns>The sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyAddWord(int a, int b, int c) => unchecked(MultiplyAddWordBottom(a, b, c) + (b * RoundShift(c, HalfBits)));

    /// <summary>Multiplies the low 16 bits of two numbers.</summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyBottoms(int a, int b) => (short)a * (short)b;

    /// <summary>Adds the product of the low 16 bits of two numbers to a number.</summary>
    /// <param name="a">The number to add to.</param>
    /// <param name="b">The first number.</param>
    /// <param name="c">The second number.</param>
    /// <returns>The sum.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyAddBottoms(int a, int b, int c) => unchecked(a + ((short)b * (short)c));

    /// <summary>Multiplies two 32 bit numbers and keeps the top 32 bits of the 64 bit product.</summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>The high part of the product.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MultiplyHigh(int a, int b) => (int)(((long)a * b) >> 32);

    /// <summary>Limits a number to the range of a 16 bit signed number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The limited number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Saturate16(int value) => Math.Clamp(value, short.MinValue, Short16Max);

    /// <summary>Limits a number to a range whose ends may be given in either order.</summary>
    /// <param name="value">The number.</param>
    /// <param name="limit1">One end of the range.</param>
    /// <param name="limit2">The other end of the range.</param>
    /// <returns>The limited number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Limit(int value, int limit1, int limit2) =>
        limit1 > limit2 ? Math.Min(Math.Max(value, limit2), limit1) : Math.Min(Math.Max(value, limit1), limit2);

    /// <summary>Shifts a number right, rounding to the nearest and halves up.</summary>
    /// <param name="value">The number.</param>
    /// <param name="shift">The bits to shift by, at least one.</param>
    /// <returns>The rounded number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RoundShift(int value, int shift) => shift == 1 ? (value >> 1) + (value & 1) : ((value >> (shift - 1)) + 1) >> 1;

    /// <summary>Shifts a 64 bit number right, rounding to the nearest and halves up.</summary>
    /// <param name="value">The number.</param>
    /// <param name="shift">The bits to shift by, at least one.</param>
    /// <returns>The rounded number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long RoundShift(long value, int shift) => shift == 1 ? (value >> 1) + (value & 1) : ((value >> (shift - 1)) + 1) >> 1;

    /// <summary>Counts the zero bits above the highest set bit of a number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The count; 32 for zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZeros(int value) => BitOperations.LeadingZeroCount((uint)value);

    /// <summary>Takes the next number of the pseudo random sequence.</summary>
    /// <param name="seed">The last number.</param>
    /// <returns>The next number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Rand(int seed) => unchecked(RandIncrement + (seed * RandMultiplier));

    /// <summary>Shifts a number left, letting its top bits fall off like the reference.</summary>
    /// <param name="value">The number.</param>
    /// <param name="shift">The bits to shift by.</param>
    /// <returns>The shifted number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ShiftLeft(int value, int shift) => (int)((uint)value << shift);

    /// <summary>Shifts a number left and limits it to the range of a 32 bit number instead of wrapping.</summary>
    /// <param name="value">The number.</param>
    /// <param name="shift">The bits to shift by.</param>
    /// <returns>The shifted number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int ShiftLeftSaturate(int value, int shift) =>
        ShiftLeft(Math.Clamp(value, int.MinValue >> shift, int.MaxValue >> shift), shift);

    /// <summary>Divides two numbers and gives the result in a given fixed-point scale.</summary>
    /// <param name="a">The numerator.</param>
    /// <param name="b">The denominator, not zero.</param>
    /// <param name="resultBits">The fractional bits of the result.</param>
    /// <returns>An approximation of <c>(a &lt;&lt; resultBits) / b</c>.</returns>
    internal static int DivideScaled(int a, int b, int resultBits)
    {
        var numeratorHeadroom = LeadingZeros(Math.Abs(a)) - 1;
        var numerator = ShiftLeft(a, numeratorHeadroom);
        var denominatorHeadroom = LeadingZeros(Math.Abs(b)) - 1;
        var denominator = ShiftLeft(b, denominatorHeadroom);
        var inverse = InverseNumerator / (denominator >> HalfBits);
        var result = MultiplyWordBottom(numerator, inverse);
        numerator = unchecked(numerator - ShiftLeft(MultiplyHigh(denominator, result), ResidualShift));
        result = MultiplyAddWordBottom(result, numerator, inverse);
        var shift = InverseBits + numeratorHeadroom - denominatorHeadroom - resultBits;
        if (shift < 0)
        {
            return ShiftLeftSaturate(result, -shift);
        }

        return shift < WordBits ? result >> shift : 0;
    }

    /// <summary>Inverts a number and gives the result in a given fixed-point scale.</summary>
    /// <param name="b">The number, not zero.</param>
    /// <param name="resultBits">The fractional bits of the result.</param>
    /// <returns>An approximation of <c>(1 &lt;&lt; resultBits) / b</c>.</returns>
    internal static int InvertScaled(int b, int resultBits)
    {
        var headroom = LeadingZeros(Math.Abs(b)) - 1;
        var normal = ShiftLeft(b, headroom);
        var inverse = InverseNumerator / (normal >> HalfBits);
        var result = ShiftLeft(inverse, HalfBits);
        var error = ShiftLeft((1 << InverseBits) - MultiplyWordBottom(normal, inverse), ResidualShift);
        result = MultiplyAddWord(result, error, inverse);
        var shift = FullInverseBits - headroom - resultBits;
        if (shift <= 0)
        {
            return ShiftLeftSaturate(result, -shift);
        }

        return shift < WordBits ? result >> shift : 0;
    }

    /// <summary>Approximates a square root.</summary>
    /// <param name="x">The number.</param>
    /// <returns>The root, within 10% above 15 and 2.5% above 120.</returns>
    internal static int SquareRootApproximation(int x)
    {
        if (x <= 0)
        {
            return 0;
        }

        var zeros = LeadingZeros(x);
        var fraction = BitOperations.RotateRight((uint)x, SqrtFractionShift - zeros) & SqrtFractionMask;
        var y = ((zeros & 1) != 0 ? SqrtOne : SqrtTwo) >> (zeros >> 1);
        return MultiplyAddWordBottom(y, y, MultiplyBottoms(SqrtSlope, (int)fraction));
    }

    /// <summary>Raises two to a power given in Q7, with a close approximation that is cheap to compute.</summary>
    /// <param name="logQ7">The exponent in Q7.</param>
    /// <returns>The power, or zero for a negative exponent.</returns>
    internal static int Exp2Q7(int logQ7)
    {
        if (logQ7 < 0)
        {
            return 0;
        }

        var value = 1 << (logQ7 >> LogFractionBits);
        var fraction = logQ7 & LogFractionMask;
        var parabola = MultiplyAddWordBottom(fraction, MultiplyBottoms(fraction, (1 << LogFractionBits) - fraction), LogParabola);
        return logQ7 < LogKnee
            ? unchecked(value + ((value * parabola) >> LogFractionBits))
            : unchecked(value + ((value >> LogFractionBits) * parabola));
    }

    /// <summary>Gets the largest exponent <see cref="Exp2Q7"/> is asked for when gains are turned back into a linear scale.</summary>
    /// <param name="logQ7">The exponent in Q7.</param>
    /// <returns>The exponent, limited to what the gain scale holds.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LimitGainLog(int logQ7) => Math.Min(logQ7, LogCeiling);
}
