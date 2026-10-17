// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Writes the packed format.</summary>
internal static class PackedTables
{
    /// <summary>The format version of both table kinds.</summary>
    private const byte Version = 1;

    /// <summary>The flag of a vertical CMap.</summary>
    private const byte VerticalFlag = 1;

    /// <summary>The bits each LEB128 byte carries.</summary>
    private const int GroupBits = 7;

    /// <summary>The value bits of a LEB128 byte.</summary>
    private const uint GroupMask = 0x7F;

    /// <summary>The flag of a LEB128 byte that another byte follows.</summary>
    private const uint MoreFlag = 0x80;

    /// <summary>The shift that spreads the sign of a 32-bit value.</summary>
    private const int SignShift = 31;

    /// <summary>The coding value of a national encoding.</summary>
    private const byte Native = 4;

    /// <summary>The coding value of UCS-2 CMaps.</summary>
    private const byte Ucs2 = 2;

    /// <summary>The coding value of UTF-16 CMaps.</summary>
    private const byte Utf16 = 3;

    /// <summary>The coding value of UTF-32 CMaps.</summary>
    private const byte Utf32 = 5;

    /// <summary>The Unicode table format with a supplementary scalar side table.</summary>
    private const byte UnicodeVersion = 2;

    /// <summary>The first reserved UTF-16 surrogate value used as a side-table index.</summary>
    private const int SupplementaryStart = 0xD800;

    /// <summary>The number of surrogate values available for side-table indices.</summary>
    private const int SupplementaryCapacity = 0x800;

    /// <summary>The number of table entries between cancellation checks.</summary>
    private const int CancellationChunk = 256;

    /// <summary>Packs BMP values with surrogate-range indices for supplementary scalars.</summary>
    /// <param name="values">The scalar value of each CID.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The packed bytes.</returns>
    internal static byte[] PackUnicode(ReadOnlySpan<int> values, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var output = new MemoryStream();
        output.WriteByte(UnicodeVersion);
        var count = values.Length;
        while (count > 0 && values[count - 1] == 0)
        {
            if ((values.Length - count) % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            count--;
        }

        WriteNumber(output, (uint)count);
        var supplementary = new Dictionary<int, int>();
        var previous = 0;
        for (var index = 0; index < count; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var packed = PackedScalar(values[index], supplementary);
            WriteSigned(output, packed - previous);
            previous = packed;
        }

        WriteNumber(output, (uint)supplementary.Count);
        var scalars = new int[supplementary.Count];
        var supplementaryIndex = 0;
        foreach (var (scalar, index) in supplementary)
        {
            if (supplementaryIndex % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            scalars[index] = scalar;
            supplementaryIndex++;
        }

        for (var index = 0; index < scalars.Length; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            WriteNumber(output, (uint)scalars[index]);
        }

        return output.ToArray();
    }

    /// <summary>Packs a CMap.</summary>
    /// <param name="maps">Every parsed CMap of the collection.</param>
    /// <param name="name">The CMap to pack.</param>
    /// <param name="collection">The collection it belongs to.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The packed bytes.</returns>
    internal static byte[] PackCMap(Dictionary<string, BinaryCMapData> maps, string name, Collection collection, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = maps[name];
        using var output = new MemoryStream();
        output.WriteByte(Version);
        output.WriteByte(map.Vertical ? VerticalFlag : (byte)0);
        output.WriteByte(Coding(name));
        output.WriteByte(collection.Script);
        var parent = Encoding.ASCII.GetBytes(map.Parent);
        output.WriteByte((byte)parent.Length);
        output.Write(parent);
        var codespaces = new List<Codespace>();
        AddCodespaces(maps, name, codespaces, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        codespaces.Sort(static (a, b) => a.Length.CompareTo(b.Length));
        output.WriteByte((byte)codespaces.Count);
        for (var index = 0; index < codespaces.Count; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var codespace = codespaces[index];
            output.WriteByte((byte)codespace.Length);
            WriteNumber(output, codespace.Low);
            WriteNumber(output, codespace.High);
        }

        WriteRanges(output, Merge(Sort(map.Ranges, cancellationToken), cancellationToken), cancellationToken);
        return output.ToArray();
    }

    /// <summary>Collects the codespace ranges of a CMap and the CMaps it builds on, since the packed reader does not inherit them.</summary>
    /// <param name="maps">Every parsed CMap.</param>
    /// <param name="name">The CMap name.</param>
    /// <param name="output">Receives the ranges without duplicates.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void AddCodespaces(Dictionary<string, BinaryCMapData> maps, string name, List<Codespace> output, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var map = maps[name];
        if (map.Parent.Length > 0)
        {
            AddCodespaces(maps, map.Parent, output, cancellationToken);
        }

        for (var index = 0; index < map.Codespaces.Length; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var codespace = map.Codespaces[index];
            if (!output.Contains(codespace))
            {
                output.Add(codespace);
            }
        }
    }

    /// <summary>Sorts ranges by first code, keeping file order among equal codes so the later entry wins.</summary>
    /// <param name="ranges">The ranges.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The sorted ranges.</returns>
    private static List<CidRange> Sort(CidRange[] ranges, CancellationToken cancellationToken)
    {
        var sorted = new List<CidRange>(ranges);
        var order = new int[sorted.Count];
        for (var i = 0; i < order.Length; i++)
        {
            if (i % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            order[i] = i;
        }

        cancellationToken.ThrowIfCancellationRequested();
        Array.Sort(order, (a, b) => sorted[a].Low != sorted[b].Low ? sorted[a].Low.CompareTo(sorted[b].Low) : a.CompareTo(b));
        cancellationToken.ThrowIfCancellationRequested();
        var result = new List<CidRange>(sorted.Count);
        for (var i = 0; i < order.Length; i++)
        {
            if (i % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            result.Add(sorted[order[i]]);
        }

        return result;
    }

    /// <summary>Joins neighbouring ranges that continue each other.</summary>
    /// <param name="ranges">The sorted ranges.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The merged ranges.</returns>
    private static List<CidRange> Merge(List<CidRange> ranges, CancellationToken cancellationToken)
    {
        var merged = new List<CidRange>(ranges.Count);
        for (var index = 0; index < ranges.Count; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var range = ranges[index];
            if (merged.Count > 0 && Continues(merged[^1], range))
            {
                merged[^1] = merged[^1] with { High = range.High };
            }
            else
            {
                merged.Add(range);
            }
        }

        return merged;
    }

    /// <summary>Tests whether a range directly continues another.</summary>
    /// <param name="previous">The earlier range.</param>
    /// <param name="next">The later range.</param>
    /// <returns><see langword="true"/> when joining them changes no lookup.</returns>
    private static bool Continues(CidRange previous, CidRange next) =>
        previous.High != uint.MaxValue && next.Low == previous.High + 1 && next.Cid == previous.Cid + (int)(previous.High - previous.Low) + 1;

    /// <summary>Writes the CID ranges as deltas.</summary>
    /// <param name="output">The output.</param>
    /// <param name="ranges">The sorted ranges.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void WriteRanges(MemoryStream output, List<CidRange> ranges, CancellationToken cancellationToken)
    {
        WriteNumber(output, (uint)ranges.Count);
        uint low = 0;
        var nextCid = 0;
        for (var index = 0; index < ranges.Count; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var range = ranges[index];
            WriteNumber(output, range.Low - low);
            WriteNumber(output, range.High - range.Low);
            WriteSigned(output, range.Cid - nextCid);
            low = range.Low;
            nextCid = range.Cid + (int)(range.High - range.Low) + 1;
        }
    }

    /// <summary>Gets the coding of a CMap from its name.</summary>
    /// <param name="name">The CMap name.</param>
    /// <returns>The <c>CidCoding</c> value.</returns>
    private static byte Coding(string name)
    {
        if (name.Contains("UCS2", StringComparison.Ordinal))
        {
            return Ucs2;
        }

        if (name.Contains("UTF16", StringComparison.Ordinal))
        {
            return Utf16;
        }

        return name.Contains("UTF32", StringComparison.Ordinal) ? Utf32 : Native;
    }

    /// <summary>Writes an unsigned LEB128 number.</summary>
    /// <param name="output">The output.</param>
    /// <param name="value">The number.</param>
    private static void WriteNumber(MemoryStream output, uint value)
    {
        while (value > GroupMask)
        {
            output.WriteByte((byte)((value & GroupMask) | MoreFlag));
            value >>= GroupBits;
        }

        output.WriteByte((byte)value);
    }

    /// <summary>Writes a zigzag-encoded signed number.</summary>
    /// <param name="output">The output.</param>
    /// <param name="value">The number.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteSigned(MemoryStream output, int value) =>
        WriteNumber(output, (uint)((value << 1) ^ (value >> SignShift)));

    /// <summary>Assigns supplementary scalars dense indices that cannot collide with BMP characters.</summary>
    /// <param name="scalar">The Unicode scalar.</param>
    /// <param name="supplementary">The scalar-to-index map in insertion order.</param>
    /// <returns>The packed UTF-16 value.</returns>
    /// <exception cref="InvalidDataException">The scalar is invalid or the side table is too large.</exception>
    private static int PackedScalar(int scalar, Dictionary<int, int> supplementary)
    {
        if (!Rune.IsValid(scalar))
        {
            throw new InvalidDataException("The CID-to-Unicode table contains an invalid scalar.");
        }

        if (scalar <= char.MaxValue)
        {
            return scalar;
        }

        ref var index = ref System.Runtime.InteropServices.CollectionsMarshal.GetValueRefOrAddDefault(supplementary, scalar, out var exists);
        if (!exists)
        {
            index = supplementary.Count - 1;
            if (index >= SupplementaryCapacity)
            {
                throw new InvalidDataException("The CID-to-Unicode supplementary table is too large.");
            }
        }

        return SupplementaryStart + index;
    }
}
