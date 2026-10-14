// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// Builds the Huffman tables of an encoded JPEG: optimal tables from symbol counts (ISO/IEC 10918-1, Annex K.2), the
/// example tables of Annex K.3, and the code lookup the entropy coder reads.
/// </summary>
internal static class JpegHuffmanBuilder
{
    /// <summary>The slots of a frequency table: the 256 symbols and the reserved symbol that keeps the all-ones code free.</summary>
    internal const int FrequencySlots = 257;

    /// <summary>The Huffman table slots of an encoder: two DC and two AC.</summary>
    internal const int TableSlots = 4;

    /// <summary>The symbols a table can hold.</summary>
    internal const int SymbolSlots = 256;

    /// <summary>The longest Huffman code.</summary>
    internal const int MaxCodeLength = 16;

    /// <summary>The bit position of the code length in a code lookup entry.</summary>
    internal const int SizeShift = 16;

    /// <summary>The mask of the code in a code lookup entry.</summary>
    internal const uint CodeMask = 0xFFFF;

    /// <summary>The first AC table slot.</summary>
    internal const int FirstAcSlot = 2;

    /// <summary>The reserved symbol that is added to every count so that no real symbol gets the all-ones code.</summary>
    private const int ReservedSymbol = 256;

    /// <summary>The bits of the symbol in a sort key.</summary>
    private const int SymbolKeyBits = 9;

    /// <summary>The mask of the symbol in a sort key.</summary>
    private const long SymbolKeyMask = (1L << SymbolKeyBits) - 1;

    /// <summary>The largest symbol number that fits the sort key; larger symbols sort first among equal weights.</summary>
    private const long SymbolKeyFlip = SymbolKeyMask;

    /// <summary>The bit position of the depth in a value sort key.</summary>
    private const int DepthShift = 48;

    /// <summary>The bit position of the inverted weight in a value sort key.</summary>
    private const int WeightShift = 16;

    /// <summary>The factor that makes every real weight larger than the reserved symbol's weight of one.</summary>
    private const int WeightScale = 2;

    /// <summary>The nodes a tree has for each leaf: the leaf and, counting one fewer overall, the node that joins it.</summary>
    private const int NodesPerLeaf = 2;

    /// <summary>The most nodes a tree over the symbols can have.</summary>
    private const int MaxNodes = (FrequencySlots * NodesPerLeaf) - 1;

    /// <summary>The codes that two codes of one length become when joined, and that one split code becomes.</summary>
    private const int Pair = 2;

    /// <summary>Gets the DC counts of the Annex K.3 luminance table.</summary>
    internal static ReadOnlySpan<byte> DcLuminanceCounts => [0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>Gets the DC counts of the Annex K.3 chrominance table.</summary>
    internal static ReadOnlySpan<byte> DcChrominanceCounts => [0x00, 0x03, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00];

    /// <summary>Gets the DC symbols of both Annex K.3 tables: the size categories 0 to 11.</summary>
    internal static ReadOnlySpan<byte> DcValues => [0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B];

    /// <summary>Gets the AC counts of the Annex K.3 luminance table.</summary>
    internal static ReadOnlySpan<byte> AcLuminanceCounts => [0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D];

    /// <summary>Gets the AC counts of the Annex K.3 chrominance table.</summary>
    internal static ReadOnlySpan<byte> AcChrominanceCounts => [0x00, 0x02, 0x01, 0x02, 0x04, 0x04, 0x03, 0x04, 0x07, 0x05, 0x04, 0x04, 0x00, 0x01, 0x02, 0x77];

    /// <summary>Gets the AC symbols of the Annex K.3 luminance table.</summary>
    internal static ReadOnlySpan<byte> AcLuminanceValues =>
    [
        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
        0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
        0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
        0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
        0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
        0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
        0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
        0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA,
    ];

    /// <summary>Gets the AC symbols of the Annex K.3 chrominance table.</summary>
    internal static ReadOnlySpan<byte> AcChrominanceValues =>
    [
        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0,
        0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
        0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
        0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
        0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
        0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
        0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
        0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
        0xF9, 0xFA,
    ];

    /// <summary>
    /// Builds an optimal table from symbol counts. A symbol that never occurs gets no code. The code lengths are limited to
    /// sixteen bits and the all-ones code is left unused, as the standard requires.
    /// </summary>
    /// <param name="frequencies">The counts of the symbols 0 to 255, followed by one unused slot.</param>
    /// <param name="counts">Receives the number of codes of each length from 1 to 16.</param>
    /// <param name="values">Receives the symbols in order of increasing code length.</param>
    /// <returns>The number of symbols in the table; zero when no symbol occurs.</returns>
    internal static int BuildOptimal(ReadOnlySpan<int> frequencies, Span<byte> counts, Span<byte> values)
    {
        counts.Clear();
        Span<long> leafBuffer = stackalloc long[FrequencySlots];
        var leafCount = CollectLeaves(frequencies, leafBuffer);
        if (leafCount == 0)
        {
            return 0;
        }

        var leaves = leafBuffer[..leafCount];
        leaves.Sort();
        Span<int> depthBuffer = stackalloc int[FrequencySlots];
        var depths = depthBuffer[..leafCount];
        MeasureDepths(leaves, depths);

        // A tree of n leaves is at most n - 1 deep, so one slot for each possible depth is enough.
        Span<int> lengthCounts = stackalloc int[FrequencySlots + 1];
        foreach (var depth in depths)
        {
            lengthCounts[depth]++;
        }

        LimitLengths(lengthCounts);
        ReleaseAllOnesCode(lengthCounts);
        for (var length = 1; length <= MaxCodeLength; length++)
        {
            counts[length - 1] = (byte)lengthCounts[length];
        }

        return OrderSymbols(leaves, depths, values);
    }

    /// <summary>Assigns the canonical codes of a table (ISO/IEC 10918-1, Annex C) and records them for the entropy coder.</summary>
    /// <param name="counts">The number of codes of each length from 1 to 16.</param>
    /// <param name="values">The symbols in code order.</param>
    /// <param name="codes">Receives one entry per symbol: the code length in the high 16 bits and the code in the low 16; zero for a symbol without a code.</param>
    internal static void BuildCodes(ReadOnlySpan<byte> counts, ReadOnlySpan<byte> values, Span<uint> codes)
    {
        codes[..SymbolSlots].Clear();
        uint code = 0;
        var index = 0;
        for (var length = 1; length <= MaxCodeLength; length++)
        {
            for (var i = 0; i < counts[length - 1]; i++)
            {
                codes[values[index]] = (uint)(length << SizeShift) | code;
                index++;
                code++;
            }

            code <<= 1;
        }
    }

    /// <summary>Collects the symbols that occur, plus the reserved symbol, as sort keys that order by weight and then by symbol number descending.</summary>
    /// <param name="frequencies">The symbol counts.</param>
    /// <param name="leaves">Receives the keys.</param>
    /// <returns>The number of keys; zero when no real symbol occurs.</returns>
    private static int CollectLeaves(ReadOnlySpan<int> frequencies, Span<long> leaves)
    {
        var count = 0;
        for (var symbol = 0; symbol < SymbolSlots; symbol++)
        {
            if (frequencies[symbol] == 0)
            {
                continue;
            }

            leaves[count] = LeafKey((long)frequencies[symbol] * WeightScale, symbol);
            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        leaves[count] = LeafKey(1, ReservedSymbol);
        return count + 1;
    }

    /// <summary>Packs a weight and a symbol into a key whose order is weight ascending, then symbol descending.</summary>
    /// <param name="weight">The weight.</param>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The key.</returns>
    private static long LeafKey(long weight, int symbol) => (weight << SymbolKeyBits) | (SymbolKeyFlip - symbol);

    /// <summary>Gets the weight of a leaf key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The weight.</returns>
    private static long WeightOf(long key) => key >> SymbolKeyBits;

    /// <summary>Gets the symbol of a leaf key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The symbol.</returns>
    private static int SymbolOf(long key) => (int)(SymbolKeyFlip - (key & SymbolKeyMask));

    /// <summary>Builds the Huffman tree by repeatedly joining the two lightest nodes and records each leaf's depth.</summary>
    /// <param name="leaves">The leaf keys, lightest first.</param>
    /// <param name="depths">Receives the depth of each leaf; at least one for every leaf.</param>
    private static void MeasureDepths(ReadOnlySpan<long> leaves, Span<int> depths)
    {
        var leafCount = leaves.Length;
        var nodeCount = (leafCount * NodesPerLeaf) - 1;
        Span<long> weight = stackalloc long[MaxNodes];
        Span<int> parent = stackalloc int[MaxNodes];
        for (var i = 0; i < leafCount; i++)
        {
            weight[i] = WeightOf(leaves[i]);
        }

        // Leaves wait in sorted order; joined nodes are produced in non-decreasing weight, so they queue in order too.
        var nextLeaf = 0;
        var nextNode = leafCount;
        for (var created = leafCount; created < nodeCount; created++)
        {
            var first = TakeLightest(weight, ref nextLeaf, leafCount, ref nextNode, created);
            var second = TakeLightest(weight, ref nextLeaf, leafCount, ref nextNode, created);
            weight[created] = weight[first] + weight[second];
            parent[first] = created;
            parent[second] = created;
        }

        Span<int> nodeDepth = stackalloc int[MaxNodes];
        var root = nodeCount - 1;
        for (var i = root - 1; i >= 0; i--)
        {
            nodeDepth[i] = nodeDepth[parent[i]] + 1;
        }

        nodeDepth[..leafCount].CopyTo(depths);
    }

    /// <summary>Takes the lighter of the next waiting leaf and the next joined node.</summary>
    /// <param name="weight">The node weights.</param>
    /// <param name="nextLeaf">The next waiting leaf, advanced when it is taken.</param>
    /// <param name="leafCount">The number of leaves.</param>
    /// <param name="nextNode">The next joined node, advanced when it is taken.</param>
    /// <param name="created">The number of nodes created so far.</param>
    /// <returns>The index of the node taken.</returns>
    private static int TakeLightest(ReadOnlySpan<long> weight, ref int nextLeaf, int leafCount, ref int nextNode, int created)
    {
        int taken;
        if (nextLeaf < leafCount && (nextNode >= created || weight[nextLeaf] <= weight[nextNode]))
        {
            taken = nextLeaf;
            nextLeaf++;
        }
        else
        {
            taken = nextNode;
            nextNode++;
        }

        return taken;
    }

    /// <summary>Shortens codes longer than sixteen bits while keeping the code space complete (ISO/IEC 10918-1, Figure K.3).</summary>
    /// <param name="lengthCounts">The number of codes of each length, index 0 unused; updated.</param>
    private static void LimitLengths(Span<int> lengthCounts)
    {
        for (var length = lengthCounts.Length - 1; length > MaxCodeLength; length--)
        {
            while (lengthCounts[length] > 0)
            {
                var shorter = length - Pair;
                while (lengthCounts[shorter] == 0)
                {
                    shorter--;
                }

                // Two codes of this length become one of the length above; one shorter code splits into two.
                lengthCounts[length] -= Pair;
                lengthCounts[length - 1]++;
                lengthCounts[shorter + 1] += Pair;
                lengthCounts[shorter]--;
            }
        }
    }

    /// <summary>Removes the longest code, which belongs to the reserved symbol, so the all-ones code stays unused.</summary>
    /// <param name="lengthCounts">The number of codes of each length; updated.</param>
    private static void ReleaseAllOnesCode(Span<int> lengthCounts)
    {
        var length = MaxCodeLength;
        while (lengthCounts[length] == 0)
        {
            length--;
        }

        lengthCounts[length]--;
    }

    /// <summary>Lists the real symbols by increasing depth, then by decreasing weight.</summary>
    /// <param name="leaves">The leaf keys.</param>
    /// <param name="depths">The depth of each leaf.</param>
    /// <param name="values">Receives the symbols.</param>
    /// <returns>The number of symbols.</returns>
    private static int OrderSymbols(ReadOnlySpan<long> leaves, ReadOnlySpan<int> depths, Span<byte> values)
    {
        Span<long> keyBuffer = stackalloc long[FrequencySlots];
        var keys = keyBuffer[..leaves.Length];
        for (var i = 0; i < leaves.Length; i++)
        {
            var inverted = uint.MaxValue - (uint)Math.Min(WeightOf(leaves[i]), uint.MaxValue);
            keys[i] = ((long)depths[i] << DepthShift) | ((long)inverted << WeightShift) | (uint)SymbolOf(leaves[i]);
        }

        keys.Sort();
        var count = 0;
        foreach (var key in keys)
        {
            var symbol = (int)(key & ushort.MaxValue);
            if (symbol == ReservedSymbol)
            {
                continue;
            }

            values[count] = (byte)symbol;
            count++;
        }

        return count;
    }
}
