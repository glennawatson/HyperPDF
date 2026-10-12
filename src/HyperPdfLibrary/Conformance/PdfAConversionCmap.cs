// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Writes a TrueType cmap table with one Microsoft symbol (3,0) subtable in format 4, mapping each one-byte code both as
/// itself and in the F000 page, as readers look up symbolic TrueType fonts (ISO 32000-1 §9.6.6.4). A single subtable meets
/// every PDF/A part (ISO 19005-1 §6.3.7; ISO 19005-2 §6.2.11.6; ISO 19005-4:2020 §6.2.10.6).
/// </summary>
internal static class PdfAConversionCmap
{
    /// <summary>The page Microsoft symbol fonts map one-byte codes into.</summary>
    private const int SymbolPage = 0xF000;

    /// <summary>The pages each code is mapped in: as itself and in the symbol page.</summary>
    private const int Pages = 2;

    /// <summary>The bytes of a 16-bit field.</summary>
    private const int Unit = 2;

    /// <summary>The size of the cmap header and its one encoding record.</summary>
    private const int TableHeader = 12;

    /// <summary>The size of a format 4 subtable header.</summary>
    private const int SubtableHeader = 14;

    /// <summary>The per-segment arrays: end codes, start codes, deltas and range offsets.</summary>
    private const int SegmentArrays = 4;

    /// <summary>The subtable format.</summary>
    private const ushort Format = 4;

    /// <summary>The Microsoft platform.</summary>
    private const ushort Platform = 3;

    /// <summary>The terminating segment's code.</summary>
    private const ushort LastCode = 0xFFFF;

    /// <summary>The offset of the table count in the cmap header.</summary>
    private const int TableCountOffset = 2;

    /// <summary>The offset of the encoding record's platform.</summary>
    private const int PlatformOffset = 4;

    /// <summary>The offset of the encoding record's subtable offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of the subtable's length field.</summary>
    private const int LengthOffset = 2;

    /// <summary>The offset of the segment count field.</summary>
    private const int SegmentCountOffset = 6;

    /// <summary>The offset of the search range field.</summary>
    private const int SearchRangeOffset = 8;

    /// <summary>The offset of the entry selector field.</summary>
    private const int EntrySelectorOffset = 10;

    /// <summary>The offset of the range shift field.</summary>
    private const int RangeShiftOffset = 12;

    /// <summary>Writes the table.</summary>
    /// <param name="glyphs">The glyph of each code, by code.</param>
    /// <returns>The cmap table.</returns>
    internal static byte[] Write(SortedDictionary<int, int> glyphs)
    {
        // One segment per code in each page, then the required final segment.
        var segments = (glyphs.Count * Pages) + 1;
        var arrayBytes = segments * Unit;
        var length = SubtableHeader + (arrayBytes * SegmentArrays) + Unit;
        var table = new byte[TableHeader + length];
        var span = table.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(span[TableCountOffset..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(span[PlatformOffset..], Platform);
        BinaryPrimitives.WriteUInt32BigEndian(span[RecordOffset..], TableHeader);
        var sub = span[TableHeader..];
        WriteHeader(sub, length, segments);

        // The end codes are followed by a reserved pad before the start codes; the range offsets stay zero, as every
        // segment maps by its delta.
        var ends = sub[SubtableHeader..];
        var starts = ends[(arrayBytes + Unit)..];
        var deltas = starts[arrayBytes..];
        var index = 0;
        foreach (var page in (ReadOnlySpan<int>)[0, SymbolPage])
        {
            foreach (var (code, glyph) in glyphs)
            {
                var character = page + code;
                WriteSegment(ends, starts, deltas, index, character, glyph - character);
                index++;
            }
        }

        WriteSegment(ends, starts, deltas, index, LastCode, 1);
        return table;
    }

    /// <summary>Writes the format 4 subtable header.</summary>
    /// <param name="sub">The subtable.</param>
    /// <param name="length">Its length.</param>
    /// <param name="segments">The number of segments.</param>
    private static void WriteHeader(Span<byte> sub, int length, int segments)
    {
        var power = (int)BitOperations.RoundUpToPowerOf2((uint)segments + 1) / Pages;
        BinaryPrimitives.WriteUInt16BigEndian(sub, Format);
        BinaryPrimitives.WriteUInt16BigEndian(sub[LengthOffset..], (ushort)length);
        BinaryPrimitives.WriteUInt16BigEndian(sub[SegmentCountOffset..], (ushort)(segments * Unit));
        BinaryPrimitives.WriteUInt16BigEndian(sub[SearchRangeOffset..], (ushort)(power * Unit));
        BinaryPrimitives.WriteUInt16BigEndian(sub[EntrySelectorOffset..], (ushort)BitOperations.Log2((uint)power));
        BinaryPrimitives.WriteUInt16BigEndian(sub[RangeShiftOffset..], (ushort)((segments - power) * Unit));
    }

    /// <summary>Writes one single-code segment.</summary>
    /// <param name="ends">The end code array.</param>
    /// <param name="starts">The start code array.</param>
    /// <param name="deltas">The delta array.</param>
    /// <param name="index">The segment.</param>
    /// <param name="character">The code.</param>
    /// <param name="delta">The glyph minus the code.</param>
    private static void WriteSegment(Span<byte> ends, Span<byte> starts, Span<byte> deltas, int index, int character, int delta)
    {
        BinaryPrimitives.WriteUInt16BigEndian(ends[(index * Unit)..], (ushort)character);
        BinaryPrimitives.WriteUInt16BigEndian(starts[(index * Unit)..], (ushort)character);
        BinaryPrimitives.WriteUInt16BigEndian(deltas[(index * Unit)..], (ushort)delta);
    }
}
