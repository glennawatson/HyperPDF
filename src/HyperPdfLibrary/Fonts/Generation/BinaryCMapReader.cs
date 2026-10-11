// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Decodes compact CMap resources without parsing PostScript source.</summary>
internal static class BinaryCMapReader
{
    /// <summary>The bit position of the block kind.</summary>
    private const int KindShift = 5;

    /// <summary>The mask of a block's encoded width.</summary>
    private const int WidthMask = 0x0F;

    /// <summary>The flag of sequential codes.</summary>
    private const int SequenceFlag = 0x10;

    /// <summary>The kind of a metadata block.</summary>
    private const int MetadataKind = 7;

    /// <summary>The largest number of mappings in a block.</summary>
    private const int MaxItems = ushort.MaxValue + 1;

    /// <summary>The width of a CID in Unicode blocks.</summary>
    private const int CidBytes = 2;

    /// <summary>The bits per byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The kind of a CID character block.</summary>
    private const int CidCharacter = 2;

    /// <summary>The kind of a CID range block.</summary>
    private const int CidRangeKind = 3;

    /// <summary>The kind of a Unicode character block.</summary>
    private const int UnicodeCharacter = 4;

    /// <summary>The kind of a Unicode range block.</summary>
    private const int UnicodeRange = 5;

    /// <summary>The encoded width of a UTF-16 surrogate pair.</summary>
    private const int SupplementaryBytes = 4;

    /// <summary>The bits in one UTF-16 unit.</summary>
    private const int Utf16Bits = 16;

    /// <summary>The number of mappings between cancellation checks.</summary>
    private const int CancellationChunk = 256;

    /// <summary>Reads a compact binary CMap.</summary>
    /// <param name="data">The binary mapping bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded mapping.</returns>
    /// <exception cref="InvalidDataException">The resource has an invalid block or mapping.</exception>
    internal static BinaryCMapData Parse(ReadOnlySpan<byte> data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stream = new BinaryCMapStream(data);
        var vertical = (stream.ReadByte() & 1) != 0;
        var map = new BinaryCMapData(string.Empty, vertical, [], [], new int[MaxItems]);
        while (!stream.AtEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = stream.ReadByte();
            var kind = header >> KindShift;
            if (kind == MetadataKind)
            {
                var text = stream.ReadString();
                if ((header & ~SequenceFlag) == (MetadataKind << KindShift | 1))
                {
                    map = map with { Parent = text };
                }

                continue;
            }

            var width = (header & WidthMask) + 1;
            var count = (int)stream.ReadNumber();
            if (count is < 1 or > MaxItems || kind > UnicodeRange)
            {
                throw new InvalidDataException("The binary CMap has an invalid block.");
            }

            ReadBlock(ref stream, map, new(kind, width, count, (header & SequenceFlag) != 0), cancellationToken);
        }

        return map;
    }

    /// <summary>Reads one mapping block.</summary>
    /// <param name="stream">The resource reader.</param>
    /// <param name="map">Receives the mappings.</param>
    /// <param name="block">The encoded block shape.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void ReadBlock(ref BinaryCMapStream stream, BinaryCMapData map, BinaryCMapBlock block, CancellationToken cancellationToken)
    {
        var width = block.Kind >= UnicodeCharacter ? CidBytes : block.Width;
        var isCharacter = block.Kind is CidCharacter or UnicodeCharacter;
        var low = stream.ReadHex(width);
        var high = low;
        var destination = BigInteger.Zero;
        for (var index = 0; index < block.Count; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (index != 0)
            {
                low = high + 1;
                if (!block.Sequential || block.Kind < CidCharacter)
                {
                    low += stream.ReadNumber();
                }
            }

            high = isCharacter ? low : low + stream.ReadNumber();
            destination = ReadDestination(ref stream, block, index, destination);
            AddMapping(map, block, low, high, destination, cancellationToken);
        }
    }

    /// <summary>Reads a block entry's destination.</summary>
    /// <param name="stream">The resource reader.</param>
    /// <param name="block">The block shape.</param>
    /// <param name="index">The entry index.</param>
    /// <param name="previous">The previous destination.</param>
    /// <returns>The decoded destination.</returns>
    private static BigInteger ReadDestination(ref BinaryCMapStream stream, BinaryCMapBlock block, int index, BigInteger previous) => block.Kind switch
    {
        0 => BigInteger.Zero,
        1 or CidRangeKind => stream.ReadNumber(),
        CidCharacter => index == 0 ? stream.ReadNumber() : previous + 1 + stream.ReadSigned(),
        UnicodeCharacter => index == 0 ? stream.ReadHex(block.Width) : (previous + 1 + stream.ReadSigned()) & ((BigInteger.One << (block.Width * ByteBits)) - 1),
        _ => stream.ReadHex(block.Width),
    };

    /// <summary>Adds one decoded entry.</summary>
    /// <param name="map">The decoded map.</param>
    /// <param name="block">The block shape.</param>
    /// <param name="low">The first code.</param>
    /// <param name="high">The last code.</param>
    /// <param name="destination">The first destination.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <exception cref="InvalidDataException">A code range is invalid.</exception>
    private static void AddMapping(BinaryCMapData map, BinaryCMapBlock block, BigInteger low, BigInteger high, BigInteger destination, CancellationToken cancellationToken)
    {
        if (low < 0 || high > uint.MaxValue || high < low)
        {
            throw new InvalidDataException("The binary CMap has an invalid code range.");
        }

        switch (block.Kind)
        {
            case 0:
                {
                    map.Codespaces.Add(new(block.Width, (uint)low, (uint)high));
                    break;
                }

            case CidCharacter or CidRangeKind:
                {
                    map.Ranges.Add(new((uint)low, (uint)high, (int)destination));
                    break;
                }

            case UnicodeCharacter or UnicodeRange:
                {
                    AddUnicode(map.Unicode, block.Width, low, high, destination, cancellationToken);
                    break;
                }
        }
    }

    /// <summary>Stores Unicode scalars, including valid UTF-16 surrogate pairs.</summary>
    /// <param name="values">The CID-indexed scalar values.</param>
    /// <param name="width">The encoded string width.</param>
    /// <param name="low">The first CID.</param>
    /// <param name="high">The last CID.</param>
    /// <param name="destination">The first UTF-16 value.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void AddUnicode(int[] values, int width, BigInteger low, BigInteger high, BigInteger destination, CancellationToken cancellationToken)
    {
        if (width is not (CidBytes or SupplementaryBytes) || high >= values.Length)
        {
            return;
        }

        for (var cid = (int)low; cid <= (int)high; cid++)
        {
            if ((cid - (int)low) % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var scalar = ScalarOf(destination + cid - low, width);
            if (scalar != 0)
            {
                values[cid] = scalar;
            }
        }
    }

    /// <summary>Decodes one UTF-16 scalar; multi-character strings remain unmapped.</summary>
    /// <param name="value">The encoded UTF-16 string.</param>
    /// <param name="width">Its byte width.</param>
    /// <returns>The scalar, or zero for an invalid or multi-character value.</returns>
    private static int ScalarOf(BigInteger value, int width)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            return 0;
        }

        var encoded = (uint)value;
        if (width == CidBytes)
        {
            return encoded <= char.MaxValue && !char.IsSurrogate((char)encoded) ? (int)encoded : 0;
        }

        return System.Text.Rune.TryCreate((char)(encoded >> Utf16Bits), (char)encoded, out var rune) ? rune.Value : 0;
    }
}
