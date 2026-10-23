// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds bfrange entries in a CMap whose start and end codes differ in more than the last byte, or run backwards, which the
/// CMap format does not allow (Adobe Technical Note #5014, 7.2).
/// </summary>
internal static class PdfAConversionBfRanges
{
    /// <summary>The number of bits in a hex digit.</summary>
    private const int NibbleBits = 4;

    /// <summary>The mask for the low four bits of a hex digit character, which hold the value of a digit or of a letter's offset.</summary>
    private const int LowNibble = 0xF;

    /// <summary>The shift that is one for a letter and zero for a digit.</summary>
    private const int LetterBit = 6;

    /// <summary>The amount a letter's low nibble is short of its value: A is 1 in its low nibble but 10 as a digit.</summary>
    private const int LetterOffset = 9;

    /// <summary>The number of bits in a byte, which the last byte of a range may vary.</summary>
    private const int ByteBits = NibbleBits + NibbleBits;

    /// <summary>Gets the keyword that opens a range block.</summary>
    private static ReadOnlySpan<byte> Begin => "beginbfrange"u8;

    /// <summary>Gets the keyword that closes a range block.</summary>
    private static ReadOnlySpan<byte> End => "endbfrange"u8;

    /// <summary>Determines whether a CMap has a range that crosses a 256-code block.</summary>
    /// <param name="cmap">The CMap bytes.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool HasCrossingRange(ReadOnlySpan<byte> cmap)
    {
        var rest = cmap;
        while (rest.IndexOf(Begin) is var start and >= 0)
        {
            rest = rest[(start + Begin.Length)..];
            var end = rest.IndexOf(End);
            var block = end < 0 ? rest : rest[..end];
            if (HasCrossingRangeIn(block))
            {
                return true;
            }

            rest = end < 0 ? [] : rest[end..];
        }

        return false;
    }

    /// <summary>Checks the entries of one range block: start code, end code, then a destination string or array.</summary>
    /// <param name="block">The block's bytes.</param>
    /// <returns><see langword="true"/> when an entry crosses a block.</returns>
    private static bool HasCrossingRangeIn(ReadOnlySpan<byte> block)
    {
        var position = 0;
        while (NextCode(block, ref position, out var startValue, out var startDigits) && NextCode(block, ref position, out var endValue, out var endDigits))
        {
            if (startDigits != endDigits || (startValue >> ByteBits) != (endValue >> ByteBits) || endValue < startValue)
            {
                return true;
            }

            SkipDestination(block, ref position);
        }

        return false;
    }

    /// <summary>Reads the next hex code.</summary>
    /// <param name="block">The block.</param>
    /// <param name="position">The read position.</param>
    /// <param name="value">The code.</param>
    /// <param name="digits">The number of hex digits.</param>
    /// <returns><see langword="true"/> when a code was read.</returns>
    private static bool NextCode(ReadOnlySpan<byte> block, ref int position, out long value, out int digits)
    {
        value = 0;
        digits = 0;
        var open = block[position..].IndexOf((byte)'<');
        if (open < 0)
        {
            return false;
        }

        position += open + 1;
        while (position < block.Length && block[position] != (byte)'>')
        {
            var digit = HexValue(block[position]);
            position++;
            if (digit < 0)
            {
                continue;
            }

            value = (value << NibbleBits) | (uint)digit;
            digits++;
        }

        position = Math.Min(block.Length, position + 1);
        return true;
    }

    /// <summary>Skips a destination: a hex string, or an array of them.</summary>
    /// <param name="block">The block.</param>
    /// <param name="position">The read position.</param>
    private static void SkipDestination(ReadOnlySpan<byte> block, ref int position)
    {
        var rest = block[position..];
        var array = rest.IndexOf((byte)'[');
        var hex = rest.IndexOf((byte)'<');
        if (array >= 0 && (hex < 0 || array < hex))
        {
            var close = rest.IndexOf((byte)']');
            position = close < 0 ? block.Length : position + close + 1;
            return;
        }

        _ = NextCode(block, ref position, out _, out _);
    }

    /// <summary>Reads a hex digit.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>Its value, or -1 when it is not a hex digit.</returns>
    private static int HexValue(byte value)
    {
        var digit = (char)value;
        return char.IsAsciiHexDigit(digit) ? (digit & LowNibble) + ((digit >> LetterBit) * LetterOffset) : -1;
    }
}
