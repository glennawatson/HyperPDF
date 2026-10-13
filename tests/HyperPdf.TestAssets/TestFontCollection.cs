// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.TestAssets;

/// <summary>Packs standalone fonts into one TrueType collection (.ttc), so tests can open a face other than the first.</summary>
public static class TestFontCollection
{
    /// <summary>The collection tag, "ttcf".</summary>
    private const uint CollectionTag = 0x74746366;

    /// <summary>The size of the collection header before the face offsets.</summary>
    private const int HeaderSize = 12;

    /// <summary>The offset of the collection version.</summary>
    private const int VersionOffset = 4;

    /// <summary>The offset of the face count.</summary>
    private const int CountOffset = 8;

    /// <summary>The size of a face's table directory header.</summary>
    private const int DirectoryHeader = 12;

    /// <summary>The size of a table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of a record's table offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of the table count in a directory.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The alignment of each face in the file.</summary>
    private const int Alignment = 4;

    /// <summary>Writes a collection of whole fonts, each placed after the header with its table offsets moved to match.</summary>
    /// <param name="fonts">The fonts, which become faces 0, 1 and so on.</param>
    /// <returns>The collection file.</returns>
    public static byte[] Create(params byte[][] fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        var header = HeaderSize + (fonts.Length * sizeof(uint));
        var length = header;
        foreach (var font in fonts)
        {
            length += Align(font.Length);
        }

        var file = new byte[length];
        BinaryPrimitives.WriteUInt32BigEndian(file, CollectionTag);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(VersionOffset), 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(CountOffset), (uint)fonts.Length);
        var at = header;
        for (var i = 0; i < fonts.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(HeaderSize + (i * sizeof(uint))), (uint)at);
            fonts[i].CopyTo(file, at);
            MoveTables(file, at);
            at += Align(fonts[i].Length);
        }

        return file;
    }

    /// <summary>Rounds a length up to the alignment.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The aligned length.</returns>
    private static int Align(int length) => (length + Alignment - 1) & -Alignment;

    /// <summary>Adds a face's position in the file to its table offsets.</summary>
    /// <param name="file">The collection file.</param>
    /// <param name="at">Where the face starts.</param>
    private static void MoveTables(byte[] file, int at)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(file.AsSpan(at + TableCountOffset));
        for (var i = 0; i < count; i++)
        {
            var record = at + DirectoryHeader + (i * RecordSize) + RecordOffset;
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(record), BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(record)) + (uint)at);
        }
    }
}
