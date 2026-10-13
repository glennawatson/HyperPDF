// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Numerics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Writes an sfnt font from its tables: the directory sorted by tag, each table four-byte aligned, and checksums.</summary>
internal static class SfntWriter
{
    /// <summary>The size of the directory header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of one table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The alignment of every table.</summary>
    private const int Alignment = 4;

    /// <summary>The offset of the table count.</summary>
    private const int CountOffset = 4;

    /// <summary>The offset of the search range.</summary>
    private const int SearchRangeOffset = 6;

    /// <summary>The offset of the entry selector.</summary>
    private const int SelectorOffset = 8;

    /// <summary>The offset of the range shift.</summary>
    private const int RangeShiftOffset = 10;

    /// <summary>The offset of a record's checksum.</summary>
    private const int RecordChecksum = 4;

    /// <summary>The offset of a record's table offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a record's table length.</summary>
    private const int RecordLength = 12;

    /// <summary>Writes the font.</summary>
    /// <param name="version">The sfnt version tag.</param>
    /// <param name="tables">The tables; they are sorted by tag in place.</param>
    /// <param name="headTag">The tag of /head, whose checksum adjustment is set last.</param>
    /// <param name="adjustmentOffset">The offset of the checksum adjustment in /head.</param>
    /// <param name="magic">The value the whole font's checksum must add up to.</param>
    /// <returns>The font.</returns>
    internal static byte[] Write(uint version, SfntEntry[] tables, uint headTag, int adjustmentOffset, uint magic)
    {
        Array.Sort(tables, static (a, b) => a.Tag.CompareTo(b.Tag));
        var size = HeaderSize + (tables.Length * RecordSize);
        foreach (var table in tables)
        {
            size += Align(table.Content.Length);
        }

        var font = new byte[size];
        WriteHeader(font, version, tables.Length);
        var offset = HeaderSize + (tables.Length * RecordSize);
        var headOffset = -1;
        for (var i = 0; i < tables.Length; i++)
        {
            var (tag, content) = tables[i];
            var record = font.AsSpan(HeaderSize + (i * RecordSize));
            BinaryPrimitives.WriteUInt32BigEndian(record, tag);
            BinaryPrimitives.WriteUInt32BigEndian(record[RecordChecksum..], SfntChecksum.Compute(content.Span));
            BinaryPrimitives.WriteUInt32BigEndian(record[RecordOffset..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(record[RecordLength..], (uint)content.Length);
            content.Span.CopyTo(font.AsSpan(offset));
            headOffset = tag == headTag ? offset : headOffset;
            offset += Align(content.Length);
        }

        if (headOffset >= 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(font.AsSpan(headOffset + adjustmentOffset), magic - SfntChecksum.Compute(font));
        }

        return font;
    }

    /// <summary>Writes the directory header with its binary search fields.</summary>
    /// <param name="font">The font.</param>
    /// <param name="version">The sfnt version.</param>
    /// <param name="count">The table count.</param>
    private static void WriteHeader(byte[] font, uint version, int count)
    {
        var selector = count > 0 ? BitOperations.Log2((uint)count) : 0;
        var searchRange = (1 << selector) * RecordSize;
        BinaryPrimitives.WriteUInt32BigEndian(font, version);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(CountOffset), (ushort)count);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(SearchRangeOffset), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(SelectorOffset), (ushort)selector);
        BinaryPrimitives.WriteUInt16BigEndian(font.AsSpan(RangeShiftOffset), (ushort)((count * RecordSize) - searchRange));
    }

    /// <summary>Rounds a length up to the table alignment.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The aligned length.</returns>
    private static int Align(int length) => (length + Alignment - 1) & -Alignment;
}
