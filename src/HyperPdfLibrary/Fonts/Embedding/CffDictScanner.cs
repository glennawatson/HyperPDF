// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Splits a CFF DICT into entries so a writer can copy the ones it keeps byte for byte (Adobe Technical Note #5176, section 4).</summary>
internal static class CffDictScanner
{
    /// <summary>The escape byte of two-byte operators.</summary>
    internal const int Escape = 12;

    /// <summary>The shift that marks a two-byte operator.</summary>
    internal const int EscapeShift = 8;

    /// <summary>The largest byte that is an operator.</summary>
    private const int MaxOperator = 21;

    /// <summary>The prefix of a 16-bit integer, followed by two bytes.</summary>
    private const int ShortInt = 28;

    /// <summary>The prefix of a 32-bit integer, followed by four bytes.</summary>
    private const int LongInt = 29;

    /// <summary>The prefix of a real number, followed by nibbles.</summary>
    private const int Real = 30;

    /// <summary>The first prefix of a two-byte integer.</summary>
    private const int TwoByteStart = 247;

    /// <summary>The last prefix of a two-byte integer.</summary>
    private const int TwoByteEnd = 254;

    /// <summary>The nibble that ends a real number.</summary>
    private const int EndNibble = 0xF;

    /// <summary>The bits in a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The bytes of a two-byte integer operand.</summary>
    private const int TwoByteLength = 2;

    /// <summary>The bytes of a 16-bit integer operand.</summary>
    private const int ShortLength = 3;

    /// <summary>The bytes of a 32-bit integer operand.</summary>
    private const int LongLength = 5;

    /// <summary>The most operands a DICT entry is read with.</summary>
    private const int OperandSpace = 48;

    /// <summary>Splits a DICT.</summary>
    /// <param name="dict">The DICT's bytes.</param>
    /// <returns>The entries in order; a damaged tail is ignored.</returns>
    internal static List<CffDictEntry> Scan(ReadOnlySpan<byte> dict)
    {
        var entries = new List<CffDictEntry>();
        var start = 0;
        var position = 0;
        while (position < dict.Length)
        {
            int b = dict[position];
            if (b > MaxOperator)
            {
                position += OperandLength(dict, position);
                continue;
            }

            var op = b;
            position++;
            if (b == Escape)
            {
                op = (Escape << EscapeShift) | FontBytes.U8(dict, position);
                position++;
            }

            entries.Add(new(op, start, Math.Min(position, dict.Length)));
            start = position;
        }

        return entries;
    }

    /// <summary>Reads one integer operand of an entry.</summary>
    /// <param name="dict">The DICT's bytes.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="index">The operand's index.</param>
    /// <returns>The operand, or zero when the entry has fewer.</returns>
    internal static int GetInt(ReadOnlySpan<byte> dict, CffDictEntry entry, int index)
    {
        var reader = new CffDictReader(dict[entry.Start..entry.End], stackalloc double[OperandSpace]);
        return reader.TryRead(out _) ? reader.GetInt(index) : 0;
    }

    /// <summary>Gets the bytes of an entry.</summary>
    /// <param name="dict">The DICT's bytes.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The operands and operator.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> Bytes(ReadOnlySpan<byte> dict, CffDictEntry entry) => dict[entry.Start..entry.End];

    /// <summary>Gets the length of the operand at a position.</summary>
    /// <param name="dict">The DICT's bytes.</param>
    /// <param name="position">The offset of the operand's first byte.</param>
    /// <returns>The operand's length in bytes.</returns>
    private static int OperandLength(ReadOnlySpan<byte> dict, int position)
    {
        int b = dict[position];
        return b switch
        {
            ShortInt => ShortLength,
            LongInt => LongLength,
            Real => RealLength(dict, position),
            >= TwoByteStart and <= TwoByteEnd => TwoByteLength,
            _ => 1,
        };
    }

    /// <summary>Gets the length of a real number operand.</summary>
    /// <param name="dict">The DICT's bytes.</param>
    /// <param name="position">The offset of the prefix byte.</param>
    /// <returns>The length, including the prefix.</returns>
    private static int RealLength(ReadOnlySpan<byte> dict, int position)
    {
        var at = position + 1;
        while (at < dict.Length)
        {
            int b = dict[at];
            at++;
            if ((b & EndNibble) == EndNibble || (b >> NibbleBits) == EndNibble)
            {
                break;
            }
        }

        return at - position;
    }
}
