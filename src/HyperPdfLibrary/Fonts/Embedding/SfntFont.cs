// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>One face of an sfnt font file: TrueType or OpenType, alone or inside a collection (.ttc).</summary>
[DebuggerDisplay("SfntFont: face {FaceIndex} of {FaceCount}, {_tables.Length} tables")]
internal sealed class SfntFont
{
    /// <summary>The size of the directory header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of one table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of the table count in a directory.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The offset of a table's offset within its record.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a table's length within its record.</summary>
    private const int RecordLength = 12;

    /// <summary>The offset of the face count in a collection header.</summary>
    private const int CollectionCountOffset = 8;

    /// <summary>The offset of the first face offset in a collection header.</summary>
    private const int CollectionFacesOffset = 12;

    /// <summary>The most faces a collection may list.</summary>
    private const int MaxFaces = 4096;

    /// <summary>The whole file.</summary>
    private readonly byte[] _data;

    /// <summary>The tables of this face, as ranges of the file.</summary>
    private readonly SfntTable[] _tables;

    /// <summary>The face's directory version.</summary>
    private readonly uint _version;

    /// <summary>Initializes a new instance of the <see cref="SfntFont"/> class.</summary>
    /// <param name="data">The whole file.</param>
    /// <param name="version">The directory version.</param>
    /// <param name="tables">The face's tables.</param>
    /// <param name="faceIndex">The face's index.</param>
    /// <param name="faceCount">The number of faces in the file.</param>
    private SfntFont(byte[] data, uint version, SfntTable[] tables, int faceIndex, int faceCount)
    {
        _data = data;
        _version = version;
        _tables = tables;
        FaceIndex = faceIndex;
        FaceCount = faceCount;
    }

    /// <summary>Gets the face's index in the file.</summary>
    internal int FaceIndex { get; }

    /// <summary>Gets the number of faces in the file; one unless the file is a collection.</summary>
    internal int FaceCount { get; }

    /// <summary>Gets a value indicating whether the file is a collection.</summary>
    internal bool IsCollection => FaceCount > 1 || FontBytes.U32(_data, 0) == SfntTags.Collection;

    /// <summary>Gets the directory version of the face.</summary>
    internal uint Version => _version;

    /// <summary>Reads one face.</summary>
    /// <param name="data">The font file.</param>
    /// <param name="faceIndex">The face's index in a collection; zero for a single font.</param>
    /// <param name="font">Receives the face.</param>
    /// <param name="refusal">Receives the plain reason when the data cannot be read.</param>
    /// <returns><see langword="true"/> when the face was read.</returns>
    internal static bool TryRead(byte[] data, int faceIndex, [NotNullWhen(true)] out SfntFont? font, [NotNullWhen(false)] out string? refusal)
    {
        font = null;
        refusal = null;
        var span = data.AsSpan();
        var directory = 0;
        var faces = 1;
        if (FontBytes.U32(span, 0) == SfntTags.Collection)
        {
            faces = Math.Min(FontBytes.Offset32(span, CollectionCountOffset), MaxFaces);
            if ((uint)faceIndex >= (uint)faces)
            {
                refusal = $"The font collection has {faces} faces, so face {faceIndex} does not exist.";
                return false;
            }

            directory = FontBytes.Offset32(span, CollectionFacesOffset + (faceIndex * FontBytes.U32Size));
        }
        else if (faceIndex != 0)
        {
            refusal = "The font file is not a collection, so only face 0 exists.";
            return false;
        }

        var version = FontBytes.U32(span, directory);
        if (version is not (SfntTags.VersionOne or SfntTags.VersionTrue or SfntTags.VersionOtto))
        {
            refusal = "The file is not a TrueType or OpenType font.";
            return false;
        }

        var tables = ReadTables(span, directory);
        font = new(data, version, tables, faceIndex, faces);
        return true;
    }

    /// <summary>Gets a table's bytes.</summary>
    /// <param name="tag">The table tag.</param>
    /// <returns>The table, or an empty span when the face has none.</returns>
    internal ReadOnlySpan<byte> Table(uint tag)
    {
        foreach (var table in _tables)
        {
            if (table.Tag == tag)
            {
                return _data.AsSpan(table.Offset, table.Length);
            }
        }

        return [];
    }

    /// <summary>Gets a table's bytes as memory, which the writer can copy without a second allocation.</summary>
    /// <param name="tag">The table tag.</param>
    /// <returns>The table, or an empty memory when the face has none.</returns>
    internal ReadOnlyMemory<byte> TableMemory(uint tag)
    {
        foreach (var table in _tables)
        {
            if (table.Tag == tag)
            {
                return _data.AsMemory(table.Offset, table.Length);
            }
        }

        return default;
    }

    /// <summary>Determines whether the face has a table.</summary>
    /// <param name="tag">The table tag.</param>
    /// <returns><see langword="true"/> when the table is present and not empty.</returns>
    internal bool Has(uint tag) => !Table(tag).IsEmpty;

    /// <summary>Writes the face as a font file of its own, which a collection's face needs before it can be embedded.</summary>
    /// <returns>The font file; the original bytes for a font that is not a collection.</returns>
    internal byte[] ToStandalone() => IsCollection ? Write(Entries()) : _data;

    /// <summary>Writes a standalone font from this face's tables with some replaced or dropped.</summary>
    /// <param name="entries">The tables to write.</param>
    /// <returns>The font file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] Write(SfntEntry[] entries) =>
        SfntWriter.Write(_version, entries, SfntTags.Head, SfntTags.HeadChecksumAdjustment, SfntTags.ChecksumMagic);

    /// <summary>Gets a copy of the face's tables as writer entries.</summary>
    /// <returns>An entry per table.</returns>
    internal SfntEntry[] Entries()
    {
        var entries = new SfntEntry[_tables.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new(_tables[i].Tag, _data.AsMemory(_tables[i].Offset, _tables[i].Length));
        }

        return entries;
    }

    /// <summary>Reads a face's table records, leaving out any that point outside the file.</summary>
    /// <param name="data">The file.</param>
    /// <param name="directory">The offset of the face's directory.</param>
    /// <returns>The tables.</returns>
    private static SfntTable[] ReadTables(ReadOnlySpan<byte> data, int directory)
    {
        var count = Math.Min(FontBytes.U16(data, directory + TableCountOffset), Math.Max((data.Length - directory - HeaderSize) / RecordSize, 0));
        var tables = new List<SfntTable>(count);
        for (var i = 0; i < count; i++)
        {
            var record = directory + HeaderSize + (i * RecordSize);
            var offset = FontBytes.U32(data, record + RecordOffset);
            var length = FontBytes.U32(data, record + RecordLength);
            if (offset > (uint)data.Length || length > (uint)data.Length - offset)
            {
                continue;
            }

            tables.Add(new(FontBytes.U32(data, record), (int)offset, (int)length));
        }

        return [.. tables];
    }
}
