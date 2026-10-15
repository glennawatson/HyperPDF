// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Objects;

/// <summary>Parses and formats PDF numbers. PDF numbers have no exponent; parsing is lenient the way viewers are.</summary>
public static class PdfNumber
{
    /// <summary>The most significant digits accumulated; more are below double precision.</summary>
    private const int MaxDigits = 18;

    /// <summary>The longest formatted number.</summary>
    private const int FormattedLength = 32;

    /// <summary>The format of real numbers written to files: up to six decimals, no exponent.</summary>
    private const string RealFormat = "0.######";

    /// <summary>The format of reals copied from a parsed file: up to ten decimals, no exponent.</summary>
    private const string PreciseFormat = "0.##########";

    /// <summary>The decimals of <see cref="RealFormat"/>.</summary>
    private const int RealDecimals = 6;

    /// <summary>The factor that turns a number into millionths, 10^<see cref="RealDecimals"/>.</summary>
    private const double RealScale = 1_000_000;

    /// <summary>The magnitude below which scaling to millionths keeps its error far under <see cref="TieMargin"/>.</summary>
    private const double ScaledLimit = 100_000;

    /// <summary>How close the scaled fraction may be to one half before the general format decides the rounding.</summary>
    private const double TieMargin = 0.001;

    /// <summary>The smallest buffer the scaled formatter writes into: a sign, five whole digits, a point and six decimals.</summary>
    private const int ScaledBufferLength = 16;

    /// <summary>One half.</summary>
    private const double Half = 0.5;

    /// <summary>The base of decimal digits.</summary>
    private const int Radix = 10;

    /// <summary>The length of "-0".</summary>
    private const int NegativeZeroLength = 2;

    /// <summary>Gets the longest number <see cref="Format(double, Span{byte})"/> writes.</summary>
    public static int MaxFormattedLength => FormattedLength;

    /// <summary>Gets the IEEE 754 bits of the powers of ten that scale decimals, 10^0 to 10^<see cref="MaxDigits"/>; each is exact in a double.</summary>
    private static ReadOnlySpan<long> PowerOfTenBits =>
    [
        0x3FF0000000000000, 0x4024000000000000, 0x4059000000000000, 0x408F400000000000, 0x40C3880000000000,
        0x40F86A0000000000, 0x412E848000000000, 0x416312D000000000, 0x4197D78400000000, 0x41CDCD6500000000,
        0x4202A05F20000000, 0x42374876E8000000, 0x426D1A94A2000000, 0x42A2309CE5400000, 0x42D6BCC41E900000,
        0x430C6BF526340000, 0x4341C37937E08000, 0x4376345785D8A000, 0x43ABC16D674EC800,
    ];

    /// <summary>Parses a number token.</summary>
    /// <param name="lexeme">The token's bytes.</param>
    /// <param name="value">The integer or real value.</param>
    /// <returns><see langword="true"/> when the token holds at least one digit.</returns>
    public static bool TryParse(ReadOnlySpan<byte> lexeme, out PdfValue value)
    {
        var index = 0;
        var negative = false;

        // Viewers accept runs of signs such as "--5"; the last sign wins.
        while (index < lexeme.Length && lexeme[index] is (byte)'-' or (byte)'+')
        {
            negative = lexeme[index] == '-';
            index++;
        }

        var digits = new DigitAccumulator();
        while (index < lexeme.Length && digits.Add(lexeme[index]))
        {
            index++;
        }

        if (!digits.SawDigit)
        {
            value = default;
            return false;
        }

        var mantissa = negative ? -digits.Mantissa : digits.Mantissa;
        value = digits.Decimals < 0 ? PdfValue.FromInteger(mantissa) : PdfValue.FromReal(mantissa / BitConverter.Int64BitsToDouble(PowerOfTenBits[Math.Min(digits.Decimals, MaxDigits)]));
        return true;
    }

    /// <summary>Parses a number token into a float, as content streams need.</summary>
    /// <param name="lexeme">The token's bytes.</param>
    /// <param name="result">The number.</param>
    /// <returns><see langword="true"/> when the token holds at least one digit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseSingle(ReadOnlySpan<byte> lexeme, out float result)
    {
        var parsed = TryParse(lexeme, out var value);
        result = value.AsSingle();
        return parsed;
    }

    /// <summary>Creates a value for a number, as an integer when it is whole.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    public static PdfValue ToValue(double number) =>
        double.IsInteger(number) && Math.Abs(number) < long.MaxValue ? PdfValue.FromInteger((long)number) : PdfValue.FromReal(number);

    /// <summary>Formats a number as PDF syntax.</summary>
    /// <param name="number">The number.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxFormattedLength"/> bytes.</param>
    /// <returns>The number of bytes written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Format(double number, Span<byte> destination) => FormatReal(number, destination, false);

    /// <summary>Formats an integer as PDF syntax.</summary>
    /// <param name="number">The number.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxFormattedLength"/> bytes.</param>
    /// <returns>The number of bytes written.</returns>
    public static int Format(long number, Span<byte> destination)
    {
        _ = number.TryFormat(destination, out var written, default, CultureInfo.InvariantCulture);
        return written;
    }

    /// <summary>
    /// Formats a number as PDF syntax with enough digits to round-trip a value read from a file: up to ten decimals,
    /// where <see cref="Format(double, Span{byte})"/> keeps six.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxFormattedLength"/> bytes.</param>
    /// <returns>The number of bytes written.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int FormatPrecise(double number, Span<byte> destination) => FormatReal(number, destination, true);

    /// <summary>Formats a real number.</summary>
    /// <param name="number">The number.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="precise">Whether to keep ten decimals rather than six.</param>
    /// <returns>The number of bytes written.</returns>
    private static int FormatReal(double number, Span<byte> destination, bool precise)
    {
        if (!double.IsFinite(number))
        {
            number = 0;
        }

        if (double.IsInteger(number))
        {
            return FormatWhole(number, destination);
        }

        if (!precise && TryFormatScaled(number, destination, out var scaledWritten))
        {
            return scaledWritten;
        }

        if (!TryFormatFraction(number, destination, precise, out var written))
        {
            return FormatWhole(Math.Round(number), destination);
        }

        // A tiny negative number rounds to "-0".
        if (written == NegativeZeroLength && destination[0] == (byte)'-' && destination[1] == (byte)'0')
        {
            destination[0] = (byte)'0';
            return 1;
        }

        return written;
    }

    /// <summary>Formats a whole number, clamped to the range of a long, as beyond it no digits would fit.</summary>
    /// <param name="whole">The whole number.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of bytes written.</returns>
    private static int FormatWhole(double whole, Span<byte> destination)
    {
        if (Math.Abs(whole) < long.MaxValue)
        {
            return Format((long)whole, destination);
        }

        var limit = whole < 0 ? -long.MaxValue : long.MaxValue;
        return Format(limit, destination);
    }

    /// <summary>
    /// Formats a number that has a fractional part with integer arithmetic, giving the same bytes as the six-decimal general
    /// format. It declines the values that format could round differently: those near a rounding tie, where the format's
    /// 15-digit intermediate rounding and a double scaling error both matter, and those too large to scale exactly enough.
    /// </summary>
    /// <param name="number">The finite number with a fractional part.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> when the number was formatted.</returns>
    private static bool TryFormatScaled(double number, Span<byte> destination, out int written)
    {
        written = 0;
        var magnitude = Math.Abs(number);
        if (magnitude >= ScaledLimit || destination.Length < ScaledBufferLength)
        {
            return false;
        }

        var scaled = magnitude * RealScale;
        var floor = Math.Floor(scaled);
        var remainder = scaled - floor;
        if (Math.Abs(remainder - Half) < TieMargin)
        {
            return false;
        }

        var units = (long)floor + (remainder > Half ? 1 : 0);
        if (units == 0)
        {
            destination[0] = (byte)'0';
            written = 1;
            return true;
        }

        var index = 0;
        if (number < 0)
        {
            destination[index] = (byte)'-';
            index++;
        }

        var whole = units / (long)RealScale;
        var fraction = (int)(units % (long)RealScale);
        index += Format(whole, destination[index..]);
        if (fraction != 0)
        {
            destination[index] = (byte)'.';
            index++;
            index += WriteFraction(fraction, destination[index..]);
        }

        written = index;
        return true;
    }

    /// <summary>Writes the six decimals of a fraction without trailing zeros.</summary>
    /// <param name="fraction">The fraction in millionths, from 1 to 999999.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of digits written.</returns>
    private static int WriteFraction(int fraction, Span<byte> destination)
    {
        var digits = RealDecimals;
        while (fraction % Radix == 0)
        {
            fraction /= Radix;
            digits--;
        }

        for (var i = digits - 1; i >= 0; i--)
        {
            destination[i] = (byte)('0' + (fraction % Radix));
            fraction /= Radix;
        }

        return digits;
    }

    /// <summary>Formats a number that has a fractional part.</summary>
    /// <param name="number">The number.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="precise">Whether to keep ten decimals rather than six.</param>
    /// <param name="written">The number of bytes written.</param>
    /// <returns><see langword="true"/> when the digits fitted the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryFormatFraction(double number, Span<byte> destination, bool precise, out int written) =>
        number.TryFormat(destination, out written, precise ? PreciseFormat : RealFormat, CultureInfo.InvariantCulture);

    /// <summary>Collects the digits and decimal point of a number.</summary>
    private struct DigitAccumulator
    {
        /// <summary>The base of decimal digits.</summary>
        private const int Ten = 10;

        /// <summary>Initializes a new instance of the <see cref="DigitAccumulator"/> struct.</summary>
        public DigitAccumulator() => Decimals = -1;

        /// <summary>Gets the significant digits.</summary>
        public long Mantissa { get; private set; }

        /// <summary>Gets the decimals kept, or -1 before a point.</summary>
        public int Decimals { get; private set; }

        /// <summary>Gets a value indicating whether any digit was seen.</summary>
        public bool SawDigit { get; private set; }

        /// <summary>Gets or sets the number of significant digits kept.</summary>
        private int Digits { get; set; }

        /// <summary>Adds a byte of the number.</summary>
        /// <param name="c">The byte.</param>
        /// <returns><see langword="false"/> when the byte ends the number.</returns>
        public bool Add(byte c)
        {
            var digit = (uint)(c - '0');
            if (digit < Ten)
            {
                SawDigit = true;
                AddDigit(digit);
                return true;
            }

            if (c != '.' || Decimals >= 0)
            {
                return false;
            }

            Decimals = 0;
            return true;
        }

        /// <summary>Adds a digit, dropping those beyond double precision.</summary>
        /// <param name="digit">The digit.</param>
        private void AddDigit(uint digit)
        {
            if (Digits < MaxDigits)
            {
                Mantissa = (Mantissa * Ten) + digit;
                Digits += Mantissa == 0 ? 0 : 1;
                Decimals += Decimals >= 0 ? 1 : 0;
                return;
            }

            // Integer digits past the precision still scale the value; decimals past it are dropped.
            if (Decimals < 0)
            {
                Mantissa = Mantissa > long.MaxValue / Ten ? long.MaxValue : Mantissa * Ten;
            }
        }
    }
}
