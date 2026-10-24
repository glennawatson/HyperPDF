// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Mp3;

/// <summary>
/// A big value Huffman table laid out for lookup: the first eight bits of a code pick an entry, and a code that is longer than eight bits
/// is finished in a second table named by that entry. An entry holds the first value in bits 8 to 11, the second in bits 12 to 15 and the
/// bits the code still takes in bits 0 to 4; an entry that leads to a second table has bit 31 set, the table's offset in the low 20 bits
/// and the bits to look at in bits 20 to 24.
/// </summary>
/// <param name="Primary">The entries picked by the first eight bits.</param>
/// <param name="Secondary">The entries of the second tables.</param>
/// <param name="LinkBits">The extra bits that follow a value of 15.</param>
[DebuggerDisplay("Mp3HuffmanTable: {LinkBits} link bits")]
internal sealed record Mp3HuffmanTable(uint[] Primary, uint[] Secondary, int LinkBits)
{
    /// <summary>The bits of the first lookup.</summary>
    internal const int PrimaryBits = 8;

    /// <summary>The flag on an entry that leads to a second table.</summary>
    internal const uint SecondFlag = 0x8000_0000;

    /// <summary>The mask of the offset in an entry that leads to a second table.</summary>
    internal const uint OffsetMask = 0xF_FFFF;

    /// <summary>The shift of the bit count in an entry that leads to a second table.</summary>
    internal const int BitsShift = 20;

    /// <summary>The mask of a bit count.</summary>
    internal const uint BitsMask = 0x1F;

    /// <summary>The shift of the first value in an entry.</summary>
    internal const int FirstShift = 8;

    /// <summary>The shift of the second value in an entry.</summary>
    internal const int SecondShift = 12;

    /// <summary>The mask of a value.</summary>
    internal const uint ValueMask = 0xF;

    /// <summary>The shift of the code length in a packed code.</summary>
    private const int LengthShift = 19;

    /// <summary>The mask of the code in a packed code.</summary>
    private const uint CodeMask = 0x7_FFFF;

    /// <summary>The shift of the first value in a packed code.</summary>
    private const int PackedFirstShift = 28;

    /// <summary>The shift of the second value in a packed code.</summary>
    private const int PackedSecondShift = 24;

    /// <summary>Builds a table from the codes of the standard.</summary>
    /// <param name="codes">The codes of one table, as packed in <see cref="Mp3TableData"/>.</param>
    /// <param name="linkBits">The extra bits that follow a value of 15.</param>
    /// <returns>The table.</returns>
    internal static Mp3HuffmanTable Build(ReadOnlySpan<uint> codes, int linkBits)
    {
        var primary = new uint[1 << PrimaryBits];
        var groupBits = new int[primary.Length];
        foreach (var packed in codes)
        {
            var length = (int)((packed >> LengthShift) & BitsMask);
            if (length > PrimaryBits)
            {
                var prefix = (int)((packed & CodeMask) >> (length - PrimaryBits));
                groupBits[prefix] = Math.Max(groupBits[prefix], length - PrimaryBits);
            }
        }

        var offsets = new int[primary.Length];
        var total = 0;
        for (var prefix = 0; prefix < groupBits.Length; prefix++)
        {
            offsets[prefix] = total;
            total += groupBits[prefix] == 0 ? 0 : 1 << groupBits[prefix];
        }

        var secondary = new uint[total];
        for (var prefix = 0; prefix < groupBits.Length; prefix++)
        {
            if (groupBits[prefix] != 0)
            {
                primary[prefix] = SecondFlag | (uint)offsets[prefix] | ((uint)groupBits[prefix] << BitsShift);
            }
        }

        foreach (var packed in codes)
        {
            Place(packed, primary, secondary, groupBits, offsets);
        }

        return new(primary, secondary, linkBits);
    }

    /// <summary>Puts one code in the lookup.</summary>
    /// <param name="packed">The packed code.</param>
    /// <param name="primary">The first lookup.</param>
    /// <param name="secondary">The second tables.</param>
    /// <param name="groupBits">The bits each second table looks at.</param>
    /// <param name="offsets">Where each second table starts.</param>
    private static void Place(uint packed, uint[] primary, uint[] secondary, int[] groupBits, int[] offsets)
    {
        var length = (int)((packed >> LengthShift) & BitsMask);
        var code = (int)(packed & CodeMask);
        var first = (packed >> PackedFirstShift) & ValueMask;
        var second = (packed >> PackedSecondShift) & ValueMask;
        var values = (first << FirstShift) | (second << SecondShift);
        if (length <= PrimaryBits)
        {
            primary.AsSpan(code << (PrimaryBits - length), 1 << (PrimaryBits - length)).Fill(values | (uint)length);
            return;
        }

        var prefix = code >> (length - PrimaryBits);
        var remaining = length - PrimaryBits;
        var bits = groupBits[prefix];
        secondary.AsSpan(offsets[prefix] + ((code & ((1 << remaining) - 1)) << (bits - remaining)), 1 << (bits - remaining)).Fill(values | (uint)remaining);
    }
}
