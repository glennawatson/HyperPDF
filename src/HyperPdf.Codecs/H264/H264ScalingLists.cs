// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.H264;

/// <summary>
/// The scaling lists of a sequence or picture: six lists of 16 weights for the 4x4 transforms and six of 64 for the 8x8 transforms,
/// each in zig-zag order. Lists 0 to 2 are for intra luma and the two chroma planes, lists 3 to 5 for inter; the 8x8 lists run
/// intra luma, inter luma, then the chroma planes of 4:4:4 video.
/// </summary>
/// <param name="Lists4x4">The six lists of 16 weights.</param>
/// <param name="Lists8x8">The six lists of 64 weights.</param>
internal sealed record H264ScalingLists(byte[][] Lists4x4, byte[][] Lists8x8)
{
    /// <summary>The number of 4x4 lists.</summary>
    internal const int Count4x4 = 6;

    /// <summary>The number of 8x8 lists.</summary>
    internal const int Count8x8 = 6;

    /// <summary>The weights of a 4x4 list.</summary>
    internal const int Size4x4 = 16;

    /// <summary>The weights of an 8x8 list.</summary>
    internal const int Size8x8 = 64;

    /// <summary>The weight that stands for no scaling.</summary>
    private const byte Flat = 16;

    /// <summary>The first of the 4x4 lists for inter blocks.</summary>
    private const int FirstInter4x4 = 3;

    /// <summary>The 8x8 list for inter luma blocks.</summary>
    private const int InterLuma8x8 = 1;

    /// <summary>The kinds of 8x8 list, intra and inter, which a chroma list steps back over to find the list it copies.</summary>
    private const int KindsOf8x8 = 2;

    /// <summary>The first transmitted index that is an 8x8 list.</summary>
    private const int First8x8 = 6;

    /// <summary>The weight a scaling list starts counting changes from.</summary>
    private const int StartScale = 8;

    /// <summary>The modulus that keeps a scale within a byte.</summary>
    private const int ScaleModulus = 256;

    /// <summary>Gets the lists that scale nothing.</summary>
    internal static H264ScalingLists FlatLists { get; } = MakeFlat();

    /// <summary>Gets the default weights of the intra 4x4 list.</summary>
    private static ReadOnlySpan<byte> DefaultIntra4x4 => [0x6, 0xD, 0xD, 0x14, 0x14, 0x14, 0x1C, 0x1C, 0x1C, 0x1C, 0x20, 0x20, 0x20, 0x25, 0x25, 0x2A];

    /// <summary>Gets the default weights of the inter 4x4 list.</summary>
    private static ReadOnlySpan<byte> DefaultInter4x4 => [0xA, 0xE, 0xE, 0x14, 0x14, 0x14, 0x18, 0x18, 0x18, 0x18, 0x1B, 0x1B, 0x1B, 0x1E, 0x1E, 0x22];

    /// <summary>Gets the default weights of the intra 8x8 list.</summary>
    private static ReadOnlySpan<byte> DefaultIntra8x8 =>
    [
        0x6, 0xA, 0xA, 0xD, 0xB, 0xD, 0x10, 0x10, 0x10, 0x10, 0x12, 0x12, 0x12, 0x12, 0x12, 0x17,
        0x17, 0x17, 0x17, 0x17, 0x17, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x1B, 0x1B, 0x1B, 0x1B,
        0x1B, 0x1B, 0x1B, 0x1B, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1D, 0x1F, 0x1F, 0x1F, 0x1F, 0x1F,
        0x1F, 0x21, 0x21, 0x21, 0x21, 0x21, 0x24, 0x24, 0x24, 0x24, 0x26, 0x26, 0x26, 0x28, 0x28, 0x2A,
    ];

    /// <summary>Gets the default weights of the inter 8x8 list.</summary>
    private static ReadOnlySpan<byte> DefaultInter8x8 =>
    [
        0x9, 0xD, 0xD, 0xF, 0xD, 0xF, 0x11, 0x11, 0x11, 0x11, 0x13, 0x13, 0x13, 0x13, 0x13, 0x15,
        0x15, 0x15, 0x15, 0x15, 0x15, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x16, 0x18, 0x18, 0x18, 0x18,
        0x18, 0x18, 0x18, 0x18, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x19, 0x1B, 0x1B, 0x1B, 0x1B, 0x1B,
        0x1B, 0x1C, 0x1C, 0x1C, 0x1C, 0x1C, 0x1E, 0x1E, 0x1E, 0x1E, 0x20, 0x20, 0x20, 0x21, 0x21, 0x23,
    ];

    /// <summary>Reads the scaling lists of a sequence or picture parameter set.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="transmitted">The number of lists the syntax can carry: 8 normally, 12 for 4:4:4 video.</param>
    /// <param name="fallback">For a picture parameter set, the lists of the sequence, which a list that is not sent copies; otherwise <see langword="null"/>, and the default lists stand in.</param>
    /// <returns>The lists.</returns>
    internal static H264ScalingLists Read(ref MsbBitReader reader, int transmitted, H264ScalingLists? fallback)
    {
        var lists4x4 = new byte[Count4x4][];
        var lists8x8 = new byte[Count8x8][];
        for (var index = 0; index < Count4x4 + Count8x8; index++)
        {
            var isSmall = index < First8x8;
            var present = index < transmitted && reader.ReadBit();
            var list = present ? ReadList(ref reader, isSmall ? Size4x4 : Size8x8, DefaultOf(index)) : Missing(index, fallback, lists4x4, lists8x8);
            if (isSmall)
            {
                lists4x4[index] = list;
            }
            else
            {
                lists8x8[index - First8x8] = list;
            }
        }

        return new(lists4x4, lists8x8);
    }

    /// <summary>Makes lists with every weight 16.</summary>
    /// <returns>The lists.</returns>
    private static H264ScalingLists MakeFlat()
    {
        var lists4x4 = new byte[Count4x4][];
        var lists8x8 = new byte[Count8x8][];
        for (var index = 0; index < Count4x4; index++)
        {
            lists4x4[index] = new byte[Size4x4];
            Array.Fill(lists4x4[index], Flat);
        }

        for (var index = 0; index < Count8x8; index++)
        {
            lists8x8[index] = new byte[Size8x8];
            Array.Fill(lists8x8[index], Flat);
        }

        return new(lists4x4, lists8x8);
    }

    /// <summary>Gets the default list for a list index.</summary>
    /// <param name="index">The list index, from 0 to 11.</param>
    /// <returns>The default weights.</returns>
    private static byte[] DefaultOf(int index)
    {
        if (index < First8x8)
        {
            return (index < FirstInter4x4 ? DefaultIntra4x4 : DefaultInter4x4).ToArray();
        }

        return ((index & 1) == 0 ? DefaultIntra8x8 : DefaultInter8x8).ToArray();
    }

    /// <summary>Gets the list used when none is sent: the default or the sequence's for the first of each kind, otherwise the one before.</summary>
    /// <param name="index">The list index, from 0 to 11.</param>
    /// <param name="fallback">The lists of the sequence, or <see langword="null"/>.</param>
    /// <param name="lists4x4">The 4x4 lists read so far.</param>
    /// <param name="lists8x8">The 8x8 lists read so far.</param>
    /// <returns>The list, which is a new array.</returns>
    private static byte[] Missing(int index, H264ScalingLists? fallback, byte[][] lists4x4, byte[][] lists8x8)
    {
        var firstOfKind = index is 0 or FirstInter4x4 or First8x8 or First8x8 + InterLuma8x8;
        if (firstOfKind)
        {
            if (fallback is null)
            {
                return DefaultOf(index);
            }

            return (byte[])(index < First8x8 ? fallback.Lists4x4[index] : fallback.Lists8x8[index - First8x8]).Clone();
        }

        // The chroma lists copy the list of the same kind that comes before them.
        return (byte[])(index < First8x8 ? lists4x4[index - 1] : lists8x8[index - First8x8 - KindsOf8x8]).Clone();
    }

    /// <summary>Reads one scaling list, scaling_list().</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="size">The number of weights.</param>
    /// <param name="defaults">The default list, used when the first change says to.</param>
    /// <returns>The list.</returns>
    private static byte[] ReadList(ref MsbBitReader reader, int size, byte[] defaults)
    {
        var list = new byte[size];
        var last = StartScale;
        var next = StartScale;
        for (var index = 0; index < size; index++)
        {
            if (next != 0)
            {
                next = (last + ExpGolomb.ReadSe(ref reader) + ScaleModulus) % ScaleModulus;
                if (index == 0 && next == 0)
                {
                    return defaults;
                }
            }

            list[index] = (byte)(next == 0 ? last : next);
            last = list[index];
        }

        return list;
    }
}
