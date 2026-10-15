// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The Unicode value of each CID of an Adobe character collection, built from compact binary CMap resources on demand.
/// A flat UTF-16 array stores BMP characters and indices into a supplementary scalar side table.
/// Each selected service reads a collection from disk once, on first use.
/// </summary>
[DebuggerDisplay("CidToUnicodeTable: {_values.Length} CIDs")]
internal sealed class CidToUnicodeTable
{
    /// <summary>The format version of the packed resource.</summary>
    private const byte Version = 2;

    /// <summary>The largest CID a table can hold.</summary>
    private const int MaxCids = ushort.MaxValue + 1;

    /// <summary>The first surrogate value reserved for a supplementary index.</summary>
    private const int SupplementaryStart = 0xD800;

    /// <summary>The number of supplementary indices representable in the flat array.</summary>
    private const int SupplementaryCapacity = 0x800;

    /// <summary>The empty table used when optional mappings are absent.</summary>
    private static readonly CidToUnicodeTable Empty = new([], []);

    /// <summary>The Unicode value of each CID; zero when none.</summary>
    private readonly char[] _values;

    /// <summary>The supplementary scalars referenced by surrogate values in the flat array.</summary>
    private readonly int[] _supplementary;

    /// <summary>Initializes a new instance of the <see cref="CidToUnicodeTable"/> class.</summary>
    /// <param name="values">The BMP values and supplementary indices of each CID.</param>
    /// <param name="supplementary">The supplementary Unicode scalars.</param>
    private CidToUnicodeTable(char[] values, int[] supplementary)
    {
        _values = values;
        _supplementary = supplementary;
    }

    /// <summary>Generates only the requested collection's Unicode table.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>A task completing when the table is available.</returns>
    internal static async ValueTask EnsureAsync(PdfFontResources resources, CjkScript collection, CancellationToken cancellationToken)
    {
        if (collection == CjkScript.None)
        {
            return;
        }

        var source = Generation.Collections.All[(int)collection - 1];
        await FontDataResources.EnsureAsync(
            resources.Data,
            "CMaps",
            $"{source.UnicodeTable}-v2.bin",
            token => FontDataGeneration.UnicodeAsync(resources, source, token),
            cancellationToken).ConfigureAwait(false);
        var cache = resources.Unicode;
        lock (cache.Gate)
        {
            if (cache.Tables[(int)collection] == Empty)
            {
                Volatile.Write(ref cache.Tables[(int)collection], Read(collection));
            }
        }
    }

    /// <summary>Gets the table of a collection.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="collection">The collection.</param>
    /// <returns>The table, or <see langword="null"/> for <see cref="CjkScript.None"/>.</returns>
    internal static CidToUnicodeTable? Get(PdfFontResources resources, CjkScript collection)
    {
        var index = (int)collection;
        if (collection == CjkScript.None || (uint)index >= CidUnicodeState.CollectionCount)
        {
            return null;
        }

        var cache = resources.Unicode;
        if (Volatile.Read(ref cache.Tables[index]) is { } table)
        {
            return table;
        }

        lock (cache.Gate)
        {
            table = cache.Tables[index] ?? Read(collection);
            Volatile.Write(ref cache.Tables[index], table);
            return table;
        }
    }

    /// <summary>Reads a collection's table from disk without caching it.</summary>
    /// <param name="collection">The collection.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The resource is missing or damaged.</exception>
    internal static CidToUnicodeTable Read(CjkScript collection)
    {
        var name = collection switch
        {
            CjkScript.Japanese => "Adobe-Japan1-UCS2-v2",
            CjkScript.SimplifiedChinese => "Adobe-GB1-UCS2-v2",
            CjkScript.TraditionalChinese => "Adobe-CNS1-UCS2-v2",
            _ => "Adobe-Korea1-UCS2-v2",
        };
        var data = new PooledBuffer(0);
        try
        {
            return CMapResources.TryRead(name, ref data) ? Parse(data.WrittenSpan) : Empty;
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>Reads packed BMP values and validates every supplementary scalar reference.</summary>
    /// <param name="data">The decompressed resource.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The data is damaged.</exception>
    internal static CidToUnicodeTable Parse(ReadOnlySpan<byte> data)
    {
        var reader = new PackedDataReader(data);
        var count = reader.ReadByte() == Version ? reader.ReadNumber() : uint.MaxValue;
        if (count > MaxCids)
        {
            throw new InvalidDataException("The CID-to-Unicode table is damaged.");
        }

        var values = ReadValues(ref reader, (int)count);
        var supplementary = ReadSupplementary(ref reader);
        ValidateIndices(values, supplementary.Length);
        return new(values, supplementary);
    }

    /// <summary>Gets the Unicode scalar of a CID.</summary>
    /// <param name="cid">The CID.</param>
    /// <returns>The Unicode scalar, or zero when the collection has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Lookup(int cid)
    {
        if ((uint)cid >= (uint)_values.Length)
        {
            return 0;
        }

        var value = _values[cid];
        var index = value - SupplementaryStart;
        return (uint)index < SupplementaryCapacity ? _supplementary[index] : value;
    }

    /// <summary>Reads the flat CID-indexed UTF-16 array.</summary>
    /// <param name="reader">The packed resource reader.</param>
    /// <param name="count">The number of CIDs.</param>
    /// <returns>The BMP values and supplementary indices.</returns>
    /// <exception cref="InvalidDataException">A value is outside the packed range.</exception>
    private static char[] ReadValues(ref PackedDataReader reader, int count)
    {
        var values = new char[count];
        long previous = 0;
        for (var cid = 0; cid < values.Length; cid++)
        {
            previous += reader.ReadSigned();
            if ((ulong)previous > char.MaxValue)
            {
                throw new InvalidDataException("The CID-to-Unicode table contains an invalid value.");
            }

            values[cid] = (char)previous;
        }

        return values;
    }

    /// <summary>Reads a bounded side table of supplementary Unicode scalars.</summary>
    /// <param name="reader">The packed resource reader.</param>
    /// <returns>The supplementary scalars.</returns>
    /// <exception cref="InvalidDataException">The side table is too large or contains an invalid scalar.</exception>
    private static int[] ReadSupplementary(ref PackedDataReader reader)
    {
        var count = reader.ReadNumber();
        if (count > SupplementaryCapacity)
        {
            throw new InvalidDataException("The CID-to-Unicode supplementary table is too large.");
        }

        var supplementary = new int[count];
        for (var index = 0; index < supplementary.Length; index++)
        {
            var scalar = reader.ReadNumber();
            if (scalar is <= char.MaxValue or > 0x10FFFF)
            {
                throw new InvalidDataException("The CID-to-Unicode supplementary scalar is invalid.");
            }

            supplementary[index] = (int)scalar;
        }

        return supplementary;
    }

    /// <summary>Rejects side-table references outside the decoded supplementary array.</summary>
    /// <param name="values">The flat values.</param>
    /// <param name="count">The side-table length.</param>
    /// <exception cref="InvalidDataException">A supplementary index is invalid.</exception>
    private static void ValidateIndices(char[] values, int count)
    {
        foreach (var value in values)
        {
            var index = value - SupplementaryStart;
            if ((uint)index < SupplementaryCapacity && index >= count)
            {
                throw new InvalidDataException("The CID-to-Unicode supplementary index is invalid.");
            }
        }
    }
}
