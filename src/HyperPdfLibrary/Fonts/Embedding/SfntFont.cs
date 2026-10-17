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
/// <param name="Data">The whole file.</param>
/// <param name="Version">The directory version.</param>
/// <param name="Tables">The face's table ranges.</param>
/// <param name="FaceIndex">The index of this face.</param>
/// <param name="FaceCount">The collection's face count.</param>
[DebuggerDisplay("SfntFont: face {FaceIndex} of {FaceCount}, {Tables.Length} tables")]
internal sealed record SfntFont(byte[] Data, uint Version, SfntTable[] Tables, int FaceIndex, int FaceCount)
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

    /// <summary>Gets a value indicating whether the file is a collection.</summary>
    internal bool IsCollection => FaceCount > 1 || FontBytes.U32(Data, 0) == SfntTags.Collection;

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
            faces = CollectionFaces(span);
            if ((uint)faceIndex >= (uint)faces)
            {
                refusal = $"The font collection has {faces} faces, so face {faceIndex} does not exist.";
                return false;
            }

            directory = CollectionDirectory(span, faceIndex);
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
        foreach (var table in Tables)
        {
            if (table.Tag == tag)
            {
                return Data.AsSpan(table.Offset, table.Length);
            }
        }

        return [];
    }

    /// <summary>Gets a table's bytes as memory, which the writer can copy without a second allocation.</summary>
    /// <param name="tag">The table tag.</param>
    /// <returns>The table, or an empty memory when the face has none.</returns>
    internal ReadOnlyMemory<byte> TableMemory(uint tag)
    {
        foreach (var table in Tables)
        {
            if (table.Tag == tag)
            {
                return Data.AsMemory(table.Offset, table.Length);
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
    internal byte[] ToStandalone() => IsCollection ? Write(Entries()) : Data;

    /// <summary>Writes a standalone font from this face's tables with some replaced or dropped.</summary>
    /// <param name="entries">The tables to write.</param>
    /// <returns>The font file.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] Write(SfntEntry[] entries) =>
        SfntWriter.Write(Version, entries, SfntTags.Head, SfntTags.HeadChecksumAdjustment, SfntTags.ChecksumMagic);

    /// <summary>Writes a standalone font from borrowed entries, sorting them in place.</summary>
    /// <param name="entries">The tables, retained only during the call.</param>
    /// <param name="cancellationToken">Cancels table copying.</param>
    /// <returns>The owned font bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] Write(Span<SfntEntry> entries, CancellationToken cancellationToken) =>
        SfntWriter.Write(Version, entries, SfntTags.Head, SfntTags.HeadChecksumAdjustment, SfntTags.ChecksumMagic, cancellationToken);

    /// <summary>Gets a copy of the face's tables as writer entries.</summary>
    /// <returns>An entry per table.</returns>
    internal SfntEntry[] Entries()
    {
        var entries = new SfntEntry[Tables.Length];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = new(Tables[i].Tag, Data.AsMemory(Tables[i].Offset, Tables[i].Length));
        }

        return entries;
    }

    /// <summary>Reads the bounded number of faces in a collection.</summary>
    /// <param name="data">The font bytes.</param>
    /// <returns>The face count.</returns>
    /// <exception cref="PdfException">The face count exceeds the directory bytes or the parser limit.</exception>
    private static int CollectionFaces(ReadOnlySpan<byte> data)
    {
        var count = FontBytes.U32(data, CollectionCountOffset);
        if (count > MaxFaces || count > Math.Max((data.Length - CollectionFacesOffset) / FontBytes.U32Size, 0))
        {
            throw new PdfException(PdfError.Format, "The font collection face count exceeds its data.");
        }

        return (int)count;
    }

    /// <summary>Reads a face directory offset without overflowing the directory header.</summary>
    /// <param name="data">The font bytes.</param>
    /// <param name="face">The validated face index.</param>
    /// <returns>The directory offset.</returns>
    /// <exception cref="PdfException">The face directory lies outside the font.</exception>
    private static int CollectionDirectory(ReadOnlySpan<byte> data, int face)
    {
        var directory = FontBytes.Offset32(data, CollectionFacesOffset + (face * FontBytes.U32Size));
        if (directory > data.Length - HeaderSize)
        {
            throw new PdfException(PdfError.Format, "The font collection face offset exceeds its data.");
        }

        return directory;
    }

    /// <summary>Reads a face's table records after bounding their byte ranges.</summary>
    /// <param name="data">The file.</param>
    /// <param name="directory">The offset of the face's directory.</param>
    /// <returns>The tables.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
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
                throw new PdfException(PdfError.Format, "The font table range exceeds its data.");
            }

            tables.Add(new(FontBytes.U32(data, record), (int)offset, (int)length));
        }

        return [.. tables];
    }
}
