// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Decodes compact CMap resources without parsing PostScript source.</summary>
internal static class BinaryCMapReader
{
    /// <summary>The largest number of mappings in a block or CID-indexed table.</summary>
    internal const int MaxItems = ushort.MaxValue + 1;

    /// <summary>The bit position of the block kind.</summary>
    private const int KindShift = 5;

    /// <summary>The mask of a block's encoded width.</summary>
    private const int WidthMask = 0x0F;

    /// <summary>The flag of sequential codes.</summary>
    private const int SequenceFlag = 0x10;

    /// <summary>The kind of a metadata block.</summary>
    private const int MetadataKind = 7;

    /// <summary>The width of a CID in Unicode blocks.</summary>
    private const int CidBytes = 2;

    /// <summary>The bits per byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The largest binary string width.</summary>
    private const int MaxValueBytes = 16;

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
    /// <returns>The decoded mapping.</returns>
    /// <exception cref="PdfException">The resource has an invalid block or mapping.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static BinaryCMapData Parse(ReadOnlySpan<byte> data) => Parse(data, CancellationToken.None);

    /// <summary>Reads a compact binary CMap with cancellation between blocks and mapping batches.</summary>
    /// <param name="data">The binary mapping bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>Independently owned mapping arrays; unused tables are empty.</returns>
    /// <exception cref="PdfException">The resource has an invalid block or mapping.</exception>
    internal static BinaryCMapData Parse(ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stream = new BinaryCMapStream(data);
        var vertical = (stream.ReadByte() & 1) != 0;
        var map = new BinaryCMapState(string.Empty, vertical, null, 0, null, 0, null, 0);
        try
        {
            while (!stream.AtEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var header = stream.ReadByte();
                var kind = header >> KindShift;
                if (kind == MetadataKind)
                {
                    if ((header & ~SequenceFlag) == (MetadataKind << KindShift | 1))
                    {
                        map = map with { Parent = stream.ReadString() };
                    }
                    else
                    {
                        stream.SkipString();
                    }

                    continue;
                }

                var width = (header & WidthMask) + 1;
                var encodedCount = stream.ReadNumber();
                if (encodedCount < 1 || encodedCount > MaxItems || kind > UnicodeRange)
                {
                    throw new PdfException("The binary CMap has an invalid block.");
                }

                ReadBlock(ref stream, ref map, new(kind, width, (int)encodedCount, (header & SequenceFlag) != 0), cancellationToken);
            }

            return new(
                map.Parent,
                map.Vertical,
                map.Codespaces.AsSpan(0, map.CodespaceCount).ToArray(),
                map.Ranges.AsSpan(0, map.RangeCount).ToArray(),
                map.Unicode.AsSpan(0, map.UnicodeCount).ToArray());
        }
        finally
        {
            var codespaces = map.Codespaces;
            var ranges = map.Ranges;
            var unicode = map.Unicode;
            PooledArrays.Return(ref codespaces, map.CodespaceCount, false);
            PooledArrays.Return(ref ranges, map.RangeCount, false);
            PooledArrays.Return(ref unicode, map.UnicodeCount, false);
        }
    }

    /// <summary>Reads one mapping block.</summary>
    /// <param name="stream">The resource reader.</param>
    /// <param name="map">Receives the mappings.</param>
    /// <param name="block">The encoded block shape.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void ReadBlock(ref BinaryCMapStream stream, ref BinaryCMapState map, BinaryCMapBlock block, CancellationToken cancellationToken)
    {
        var width = block.Kind >= UnicodeCharacter ? CidBytes : block.Width;
        var isCharacter = block.Kind is CidCharacter or UnicodeCharacter;
        var low = stream.ReadHex(width);
        var high = low;
        UInt128 destination = 0;
        for (var index = 0; index < block.Count; index++)
        {
            if (index % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (index != 0)
            {
                low = AddCode(high, 1);
                if (!block.Sequential || block.Kind < CidCharacter)
                {
                    low = AddCode(low, stream.ReadNumber());
                }
            }

            high = isCharacter ? low : AddCode(low, stream.ReadNumber());
            destination = ReadDestination(ref stream, block, index, destination);
            AddMapping(ref map, block, low, high, destination, cancellationToken);
        }
    }

    /// <summary>Reads a block entry's destination.</summary>
    /// <param name="stream">The resource reader.</param>
    /// <param name="block">The block shape.</param>
    /// <param name="index">The entry index.</param>
    /// <param name="previous">The previous destination.</param>
    /// <returns>The decoded destination.</returns>
    private static UInt128 ReadDestination(ref BinaryCMapStream stream, BinaryCMapBlock block, int index, UInt128 previous) => block.Kind switch
    {
        0 => 0,
        1 or CidRangeKind => stream.ReadNumber(),
        CidCharacter => index == 0 ? stream.ReadNumber() : unchecked(previous + 1 + (UInt128)stream.ReadSigned()),
        UnicodeCharacter => index == 0 ? stream.ReadHex(block.Width) : unchecked(previous + 1 + (UInt128)stream.ReadSigned()) & (UInt128.MaxValue >> ((MaxValueBytes - block.Width) * ByteBits)),
        _ => stream.ReadHex(block.Width),
    };

    /// <summary>Adds an unsigned code delta without wraparound or unbounded integer storage.</summary>
    /// <param name="value">The preceding code.</param>
    /// <param name="delta">The encoded increase.</param>
    /// <returns>The next bounded code.</returns>
    /// <exception cref="PdfException">The code exceeds the supported width.</exception>
    private static UInt128 AddCode(UInt128 value, UInt128 delta)
    {
        if (value > uint.MaxValue || delta > uint.MaxValue - value)
        {
            throw new PdfException("The binary CMap has an invalid code range.");
        }

        return value + delta;
    }

    /// <summary>Adds one decoded entry.</summary>
    /// <param name="map">The decoded map.</param>
    /// <param name="block">The block shape.</param>
    /// <param name="low">The first code.</param>
    /// <param name="high">The last code.</param>
    /// <param name="destination">The first destination.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <exception cref="PdfException">A code range is invalid.</exception>
    private static void AddMapping(ref BinaryCMapState map, BinaryCMapBlock block, UInt128 low, UInt128 high, UInt128 destination, CancellationToken cancellationToken)
    {
        if (high > uint.MaxValue || high < low)
        {
            throw new PdfException("The binary CMap has an invalid code range.");
        }

        switch (block.Kind)
        {
            case 0:
                {
                    var codespaces = map.Codespaces;
                    PooledArrays.EnsureRoom(ref codespaces, map.CodespaceCount, false)[map.CodespaceCount] = new(block.Width, (uint)low, (uint)high);
                    map = map with { Codespaces = codespaces, CodespaceCount = map.CodespaceCount + 1 };
                    break;
                }

            case CidCharacter or CidRangeKind:
                {
                    if (destination > int.MaxValue || high - low > (uint)int.MaxValue - destination)
                    {
                        throw new PdfException(PdfError.Format, "The binary CMap destination exceeds its signed width.");
                    }

                    var ranges = map.Ranges;
                    PooledArrays.EnsureRoom(ref ranges, map.RangeCount, false)[map.RangeCount] = new((uint)low, (uint)high, (int)destination);
                    map = map with { Ranges = ranges, RangeCount = map.RangeCount + 1 };
                    break;
                }

            case UnicodeCharacter or UnicodeRange:
                {
                    AddUnicode(ref map, block.Width, low, high, destination, cancellationToken);
                    break;
                }
        }
    }

    /// <summary>Stores Unicode scalars, including valid UTF-16 surrogate pairs.</summary>
    /// <param name="map">Receives usable CID-indexed scalar values.</param>
    /// <param name="width">The encoded string width.</param>
    /// <param name="low">The first CID.</param>
    /// <param name="high">The last CID.</param>
    /// <param name="destination">The first UTF-16 value.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void AddUnicode(ref BinaryCMapState map, int width, UInt128 low, UInt128 high, UInt128 destination, CancellationToken cancellationToken)
    {
        if (width is not (CidBytes or SupplementaryBytes) || high >= MaxItems)
        {
            return;
        }

        for (var cid = (int)low; cid <= (int)high; cid++)
        {
            if ((cid - (int)low) % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var scalar = ScalarOf(destination + (uint)cid - low, width);
            if (scalar == 0)
            {
                continue;
            }

            SetUnicode(ref map, cid, scalar);
        }
    }

    /// <summary>Stores one scalar, growing only as far as the highest CID read.</summary>
    /// <param name="map">The parse's leased storage.</param>
    /// <param name="cid">The mapped CID.</param>
    /// <param name="scalar">The usable Unicode scalar.</param>
    private static void SetUnicode(ref BinaryCMapState map, int cid, int scalar)
    {
        var count = Math.Max(map.UnicodeCount, cid + 1);
        var values = map.Unicode;
        if (values is null || count > values.Length)
        {
            var larger = ArrayPool<int>.Shared.Rent(count);
            larger.AsSpan().Clear();
            if (values is not null)
            {
                values.AsSpan(0, map.UnicodeCount).CopyTo(larger);
                ArrayPool<int>.Shared.Return(values);
            }

            values = larger;
        }

        values[cid] = scalar;
        map = map with { Unicode = values, UnicodeCount = count };
    }

    /// <summary>Decodes one UTF-16 scalar; multi-character strings remain unmapped.</summary>
    /// <param name="value">The encoded UTF-16 string.</param>
    /// <param name="width">Its byte width.</param>
    /// <returns>The scalar, or zero for an invalid or multi-character value.</returns>
    private static int ScalarOf(UInt128 value, int width)
    {
        if (value > uint.MaxValue)
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
