// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Writes the building blocks of a CFF font: INDEXes and DICT numbers (Adobe Technical Note #5176, sections 2 to 4).</summary>
internal static class CffWriting
{
    /// <summary>The prefix of a 32-bit DICT integer.</summary>
    private const byte LongIntPrefix = 29;

    /// <summary>The bytes of a 32-bit DICT integer with its prefix.</summary>
    private const int LongIntLength = 5;

    /// <summary>The bytes of the CFF header that precede the Name INDEX.</summary>
    private const int HeaderBytes = 4;

    /// <summary>The bytes of an INDEX's count.</summary>
    private const int CountBytes = 2;

    /// <summary>The largest offset a one-byte INDEX offset holds.</summary>
    private const int MaxOneByte = 0xFF;

    /// <summary>The largest offset a two-byte INDEX offset holds.</summary>
    private const int MaxTwoBytes = 0xFFFF;

    /// <summary>The largest offset a three-byte INDEX offset holds.</summary>
    private const int MaxThreeBytes = 0xFFFFFF;

    /// <summary>The offsets in an INDEX of one object.</summary>
    private const int SingleOffsets = 2;

    /// <summary>The offset size that takes two bytes.</summary>
    private const int TwoByteOffsets = 2;

    /// <summary>The offset size that takes three bytes.</summary>
    private const int ThreeByteOffsets = 3;

    /// <summary>The offset size that takes four bytes.</summary>
    private const int FourByteOffsets = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Writes an INDEX.</summary>
    /// <param name="items">The objects.</param>
    /// <returns>The INDEX's bytes; two bytes for an empty INDEX.</returns>
    internal static byte[] Index(ReadOnlySpan<byte[]> items)
    {
        if (items.IsEmpty)
        {
            return new byte[CountBytes];
        }

        var total = 1;
        foreach (var item in items)
        {
            total += item.Length;
        }

        var offsetSize = OffsetSize(total);
        var result = new byte[CountBytes + 1 + ((items.Length + 1) * offsetSize) + total - 1];
        BinaryPrimitives.WriteUInt16BigEndian(result, (ushort)items.Length);
        result[CountBytes] = (byte)offsetSize;
        const int table = CountBytes + 1;
        var data = table + ((items.Length + 1) * offsetSize);
        var offset = 1;
        WriteOffset(result.AsSpan(table), offset, offsetSize);
        for (var i = 0; i < items.Length; i++)
        {
            items[i].CopyTo(result.AsSpan(data));
            data += items[i].Length;
            offset += items[i].Length;
            WriteOffset(result.AsSpan(table + ((i + 1) * offsetSize)), offset, offsetSize);
        }

        return result;
    }

    /// <summary>Writes a DICT integer that always takes five bytes, so the DICT's size does not depend on the value.</summary>
    /// <param name="value">The value.</param>
    /// <param name="destination">Receives the five bytes.</param>
    internal static void FixedInt(int value, Span<byte> destination)
    {
        destination[0] = LongIntPrefix;
        BinaryPrimitives.WriteInt32BigEndian(destination[1..], value);
    }

    /// <summary>Appends a DICT entry with fixed-size integer operands.</summary>
    /// <param name="dict">The DICT being built.</param>
    /// <param name="op">The operator; a two-byte operator is <c>(12 &lt;&lt; 8) | second byte</c>.</param>
    /// <param name="operands">The operands.</param>
    internal static void Entry(List<byte> dict, int op, params ReadOnlySpan<int> operands)
    {
        Span<byte> number = stackalloc byte[LongIntLength];
        foreach (var operand in operands)
        {
            FixedInt(operand, number);
            foreach (var b in number)
            {
                dict.Add(b);
            }
        }

        if (op > byte.MaxValue)
        {
            dict.Add((byte)(op >> CffDictScanner.EscapeShift));
        }

        dict.Add((byte)op);
    }

    /// <summary>Overwrites the font's name in place, which a CFF font written by <see cref="CffSubsetter"/> holds as the only object of its Name INDEX right after the header.</summary>
    /// <param name="cff">The font.</param>
    /// <param name="name">The new name, which must be as long as the one written.</param>
    internal static void SetName(byte[] cff, string name)
    {
        var offsetSize = cff[HeaderBytes + CountBytes];
        var start = HeaderBytes + CountBytes + 1 + (SingleOffsets * offsetSize);
        _ = System.Text.Encoding.ASCII.GetBytes(name, cff.AsSpan(start, name.Length));
    }

    /// <summary>Gets the bytes of an INDEX holding one object.</summary>
    /// <param name="item">The object.</param>
    /// <returns>The INDEX.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte[] SingleIndex(byte[] item) => Index([item]);

    /// <summary>Chooses the smallest offset size for an INDEX whose data takes some bytes.</summary>
    /// <param name="total">The data's length plus one.</param>
    /// <returns>One to four.</returns>
    private static int OffsetSize(int total) => total switch
    {
        <= MaxOneByte => 1,
        <= MaxTwoBytes => TwoByteOffsets,
        <= MaxThreeBytes => ThreeByteOffsets,
        _ => FourByteOffsets,
    };

    /// <summary>Writes one INDEX offset.</summary>
    /// <param name="destination">Where the offset goes.</param>
    /// <param name="value">The offset.</param>
    /// <param name="size">The offset size.</param>
    private static void WriteOffset(Span<byte> destination, int value, int size)
    {
        for (var i = 0; i < size; i++)
        {
            destination[i] = (byte)(value >> (ByteBits * (size - 1 - i)));
        }
    }
}
