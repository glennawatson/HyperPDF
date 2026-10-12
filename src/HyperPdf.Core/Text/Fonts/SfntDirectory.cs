// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// The table directory of one font in a TrueType or OpenType file, or one face of a collection. Offsets are from the
/// start of the file, so tables are read from the same bytes whichever face they belong to.
/// </summary>
[DebuggerDisplay("SfntDirectory: {Count} tables")]
internal sealed class SfntDirectory
{
    /// <summary>The bytes of the offset table before the table records.</summary>
    internal const int HeaderBytes = 12;

    /// <summary>The bytes of one table record.</summary>
    internal const int RecordBytes = 16;

    /// <summary>The offset of the table count in the offset table.</summary>
    private const int CountOffset = 4;

    /// <summary>The offset of the face count in a collection header.</summary>
    private const int FaceCountOffset = 8;

    /// <summary>The offset of the first face offset in a collection header.</summary>
    private const int FaceOffsets = 12;

    /// <summary>The bytes of a 32-bit value.</summary>
    private const int Int32Bytes = 4;

    /// <summary>The offset of a table's offset in its record.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a table's length in its record.</summary>
    private const int RecordLength = 12;

    /// <summary>The most tables a font directory is read with.</summary>
    private const int MaxTables = 512;

    /// <summary>The TrueType outline version.</summary>
    private const uint TrueTypeVersion = 0x00010000;

    /// <summary>The Apple TrueType version, "true".</summary>
    private const uint AppleTrueTypeVersion = 0x74727565;

    /// <summary>The OpenType CFF version, "OTTO".</summary>
    private const uint OpenTypeCffVersion = 0x4F54544F;

    /// <summary>The tables, by tag.</summary>
    private readonly (uint Tag, int Offset, int Length)[] _tables;

    /// <summary>Initializes a new instance of the <see cref="SfntDirectory"/> class.</summary>
    /// <param name="tables">The tables.</param>
    private SfntDirectory((uint Tag, int Offset, int Length)[] tables) => _tables = tables;

    /// <summary>Gets the number of tables.</summary>
    internal int Count => _tables.Length;

    /// <summary>Gets the tables in directory order.</summary>
    internal ReadOnlySpan<(uint Tag, int Offset, int Length)> Tables => _tables;

    /// <summary>Gets the number of faces in a file: the collection's count, or 1 for a single font.</summary>
    /// <param name="header">At least the first 12 bytes of the file.</param>
    /// <returns>The face count, or 0 when the bytes are not a font.</returns>
    internal static int FaceCount(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderBytes)
        {
            return 0;
        }

        var version = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (version == SfntTag.Ttcf)
        {
            return (int)Math.Min(BinaryPrimitives.ReadUInt32BigEndian(header[FaceCountOffset..]), MaxTables);
        }

        return IsFontVersion(version) ? 1 : 0;
    }

    /// <summary>Gets where a face's offset table starts.</summary>
    /// <param name="header">The start of the file, holding at least the collection header and its face offsets.</param>
    /// <param name="faceIndex">The face.</param>
    /// <returns>The offset, or -1.</returns>
    internal static long FaceOffset(ReadOnlySpan<byte> header, int faceIndex)
    {
        if (header.Length < HeaderBytes)
        {
            return -1;
        }

        if (BinaryPrimitives.ReadUInt32BigEndian(header) != SfntTag.Ttcf)
        {
            return faceIndex == 0 ? 0 : -1;
        }

        var at = FaceOffsets + (faceIndex * Int32Bytes);
        return faceIndex < 0 || at + Int32Bytes > header.Length ? -1 : BinaryPrimitives.ReadUInt32BigEndian(header[at..]);
    }

    /// <summary>Gets the bytes a face's directory takes, from its table count.</summary>
    /// <param name="offsetTable">At least the first 12 bytes of the face's offset table.</param>
    /// <returns>The bytes, or 0 when it is not a font.</returns>
    internal static int DirectoryBytes(ReadOnlySpan<byte> offsetTable)
    {
        if (offsetTable.Length < HeaderBytes || !IsFontVersion(BinaryPrimitives.ReadUInt32BigEndian(offsetTable)))
        {
            return 0;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(offsetTable[CountOffset..]);
        return count > MaxTables ? 0 : HeaderBytes + (count * RecordBytes);
    }

    /// <summary>Reads a face's table directory.</summary>
    /// <param name="directory">The face's offset table and table records.</param>
    /// <param name="fileLength">The length of the file, so tables past its end are dropped.</param>
    /// <returns>The directory, or <see langword="null"/> when it is not a font.</returns>
    internal static SfntDirectory? Read(ReadOnlySpan<byte> directory, long fileLength)
    {
        var bytes = DirectoryBytes(directory);
        if (bytes == 0 || directory.Length < bytes)
        {
            return null;
        }

        var count = (bytes - HeaderBytes) / RecordBytes;
        var tables = new List<(uint Tag, int Offset, int Length)>(count);
        for (var i = 0; i < count; i++)
        {
            var record = directory.Slice(HeaderBytes + (i * RecordBytes), RecordBytes);
            var offset = BinaryPrimitives.ReadUInt32BigEndian(record[RecordOffset..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(record[RecordLength..]);
            if (offset + (long)length <= fileLength)
            {
                tables.Add((BinaryPrimitives.ReadUInt32BigEndian(record), (int)offset, (int)length));
            }
        }

        return new([.. tables]);
    }

    /// <summary>Finds a table.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="offset">The table's offset in the file.</param>
    /// <param name="length">The table's length.</param>
    /// <returns><see langword="true"/> when the face has the table.</returns>
    internal bool TryFind(uint tag, out int offset, out int length)
    {
        foreach (var table in _tables)
        {
            if (table.Tag != tag)
            {
                continue;
            }

            offset = table.Offset;
            length = table.Length;
            return true;
        }

        offset = 0;
        length = 0;
        return false;
    }

    /// <summary>Gets a table's bytes from the whole file.</summary>
    /// <param name="file">The file.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The table, or an empty span.</returns>
    internal ReadOnlySpan<byte> Table(ReadOnlySpan<byte> file, uint tag) =>
        TryFind(tag, out var offset, out var length) && offset + (long)length <= file.Length ? file.Slice(offset, length) : default;

    /// <summary>Determines whether a version number starts a font.</summary>
    /// <param name="version">The version.</param>
    /// <returns><see langword="true"/> for TrueType or OpenType.</returns>
    private static bool IsFontVersion(uint version) => version is TrueTypeVersion or AppleTrueTypeVersion or OpenTypeCffVersion;
}
