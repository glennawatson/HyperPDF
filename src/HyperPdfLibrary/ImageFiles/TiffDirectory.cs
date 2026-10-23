// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The first image file directory of a TIFF file: its tags and the way numbers are stored.</summary>
[DebuggerDisplay("TiffDirectory: {_entries.Count} tags")]
internal sealed class TiffDirectory
{
    /// <summary>The bytes of the file header.</summary>
    private const int HeaderBytes = 8;

    /// <summary>The bytes of one directory entry.</summary>
    private const int EntryBytes = 12;

    /// <summary>The bytes of the entry count that opens a directory.</summary>
    private const int CountBytes = 2;

    /// <summary>The field type for an unsigned byte.</summary>
    private const int TypeByte = 1;

    /// <summary>The field type for a 16-bit unsigned integer.</summary>
    private const int TypeShort = 3;

    /// <summary>The field type for a 32-bit unsigned integer.</summary>
    private const int TypeLong = 4;

    /// <summary>The bytes a value may take and still sit inside its entry.</summary>
    private const int InlineBytes = 4;

    /// <summary>The offset of the field type in an entry.</summary>
    private const int TypeOffset = 2;

    /// <summary>The offset of the value count in an entry.</summary>
    private const int CountOffset = 4;

    /// <summary>The offset of the value, or the offset of the values, in an entry.</summary>
    private const int ValueOffset = 8;

    /// <summary>The tags of the directory by number.</summary>
    private readonly Dictionary<int, TiffEntry> _entries = [];

    /// <summary>The file's bytes.</summary>
    private readonly byte[] _file;

    /// <summary>Initializes a new instance of the <see cref="TiffDirectory"/> class.</summary>
    /// <param name="file">The file's bytes.</param>
    /// <exception cref="InvalidDataException">The header or directory is cut short or invalid.</exception>
    internal TiffDirectory(byte[] file)
    {
        _file = file;
        if (file.Length < HeaderBytes)
        {
            throw new InvalidDataException("The TIFF is too short.");
        }

        IsLittleEndian = file[0] == (byte)'I';
        var offset = ReadUInt32(HeaderBytes - InlineBytes);
        if (offset + CountBytes > file.Length)
        {
            throw new InvalidDataException("The TIFF's first directory is outside the file.");
        }

        var count = ReadUInt16((int)offset);
        for (var i = 0; i < count; i++)
        {
            var at = (int)offset + CountBytes + (i * EntryBytes);
            if (at + EntryBytes > file.Length)
            {
                throw new InvalidDataException("The TIFF's directory is cut short.");
            }

            _entries[ReadUInt16(at)] = new(ReadUInt16(at + TypeOffset), (int)Math.Min(ReadUInt32(at + CountOffset), int.MaxValue), at + ValueOffset);
        }
    }

    /// <summary>Gets a value indicating whether numbers are stored least significant byte first.</summary>
    internal bool IsLittleEndian { get; }

    /// <summary>Gets the file's bytes.</summary>
    internal byte[] File => _file;

    /// <summary>Reads a 16-bit number from the file.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The number is outside the file.</exception>
    internal ushort ReadUInt16(int offset)
    {
        Check(offset, sizeof(ushort));
        return IsLittleEndian ? BinaryPrimitives.ReadUInt16LittleEndian(_file.AsSpan(offset)) : BinaryPrimitives.ReadUInt16BigEndian(_file.AsSpan(offset));
    }

    /// <summary>Reads a 32-bit number from the file.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The number is outside the file.</exception>
    internal uint ReadUInt32(int offset)
    {
        Check(offset, sizeof(uint));
        return IsLittleEndian ? BinaryPrimitives.ReadUInt32LittleEndian(_file.AsSpan(offset)) : BinaryPrimitives.ReadUInt32BigEndian(_file.AsSpan(offset));
    }

    /// <summary>Determines whether the directory holds a tag.</summary>
    /// <param name="tag">The tag number.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool Has(int tag) => _entries.ContainsKey(tag);

    /// <summary>Gets a tag's first value.</summary>
    /// <param name="tag">The tag number.</param>
    /// <param name="fallback">The value when the tag is absent.</param>
    /// <returns>The value.</returns>
    internal uint GetValue(int tag, uint fallback)
    {
        var values = GetValues(tag, 1);
        return values.Length == 0 ? fallback : values[0];
    }

    /// <summary>Gets a tag's byte, short or long values.</summary>
    /// <param name="tag">The tag number.</param>
    /// <param name="limit">The most values to read.</param>
    /// <returns>The values; empty when the tag is absent or has another type.</returns>
    /// <exception cref="InvalidDataException">The values are outside the file.</exception>
    internal uint[] GetValues(int tag, int limit)
    {
        if (!_entries.TryGetValue(tag, out var entry) || entry.Type is not (TypeByte or TypeShort or TypeLong))
        {
            return [];
        }

        var size = GetItemSize(entry.Type);
        var start = (long)entry.Count * size <= InlineBytes ? entry.ValueAt : (int)ReadUInt32(entry.ValueAt);
        var values = new uint[Math.Min(entry.Count, limit)];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = ReadItem(start + (i * size), size);
        }

        return values;
    }

    /// <summary>Gets the bytes in one value of a field type.</summary>
    /// <param name="type">The field type.</param>
    /// <returns>The bytes.</returns>
    private static int GetItemSize(int type) => type switch
    {
        TypeLong => sizeof(uint),
        TypeShort => sizeof(ushort),
        _ => 1,
    };

    /// <summary>Reads one value.</summary>
    /// <param name="offset">Where the value is.</param>
    /// <param name="size">The bytes in the value: 1, 2 or 4.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The value is outside the file.</exception>
    private uint ReadItem(int offset, int size) => size switch
    {
        sizeof(uint) => ReadUInt32(offset),
        sizeof(ushort) => ReadUInt16(offset),
        _ => ReadByte(offset),
    };

    /// <summary>Reads a byte from the file.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The byte is outside the file.</exception>
    private byte ReadByte(int offset)
    {
        Check(offset, 1);
        return _file[offset];
    }

    /// <summary>Checks that a range is inside the file.</summary>
    /// <param name="offset">The first byte.</param>
    /// <param name="length">The bytes.</param>
    /// <exception cref="InvalidDataException">The range is outside the file.</exception>
    private void Check(int offset, int length)
    {
        if (offset < 0 || (long)offset + length > _file.Length)
        {
            throw new InvalidDataException("The TIFF refers to a place outside the file.");
        }
    }
}
