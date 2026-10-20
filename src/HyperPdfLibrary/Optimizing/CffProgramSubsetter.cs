// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Cuts an embedded <c>/FontFile3</c> program down to the glyphs a document shows: a bare Compact Font Format program, or an
/// OpenType font whose outlines are CFF. Glyph numbers are kept, as in <see cref="TrueTypeSubsetter"/>, so widths, CIDs and
/// content streams stay valid. An OpenType font with TrueType outlines is handed to that subsetter. A font whose licence
/// forbids subsetting is left whole.
/// </summary>
internal static class CffProgramSubsetter
{
    /// <summary>The first byte of a bare CFF program: the major version.</summary>
    private const byte CffMajorVersion = 1;

    /// <summary>The sfnt version of OpenType fonts with CFF outlines: <c>OTTO</c>.</summary>
    private const uint OpenTypeVersion = 0x4F54544F;

    /// <summary>The tag of the CFF table.</summary>
    private const uint CffTag = 0x43464620;

    /// <summary>The tag of the font header table.</summary>
    private const uint HeadTag = 0x68656164;

    /// <summary>The tag of the OS/2 table.</summary>
    private const uint Os2Tag = 0x4F532F32;

    /// <summary>The size of the sfnt header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of one table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of the table count in the directory.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The offset of a record's table offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a record's table length.</summary>
    private const int RecordLength = 12;

    /// <summary>The offset of the checksum adjustment in /head.</summary>
    private const int HeadChecksum = 8;

    /// <summary>The offset of the embedding flags in /OS/2.</summary>
    private const int Os2FsType = 8;

    /// <summary>The embedding flag that forbids subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The value the whole font's checksum must add up to.</summary>
    private const uint ChecksumMagic = 0xB1B0AFBA;

    /// <summary>The smallest /head table that holds the checksum adjustment.</summary>
    private const int MinHeadSize = HeadChecksum + sizeof(uint);

    /// <summary>The tags of tables PDF drawing never reads: signatures and layout tables.</summary>
    private static readonly uint[] DroppedTables =
    [
        0x44534947, // DSIG
        0x47535542, // GSUB
        0x47504F53, // GPOS
        0x47444546, // GDEF
        0x4A535446, // JSTF
        0x42415345, // BASE
    ];

    /// <summary>Cuts a CFF font program down.</summary>
    /// <param name="program">The /FontFile3 stream.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="fontName">The name to write in the program's Name INDEX.</param>
    /// <param name="scratchPools">The compression owner.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The cut-down program stream, or <see langword="null"/> when the program cannot be cut down or nothing is gained.</returns>
    internal static PdfStream? Subset(PdfStream program, CodeBitSet glyphs, string fontName, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        var font = default(PooledBuffer);
        try
        {
            _ = program.Decode(ref font, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsSfnt(font.WrittenSpan) && BinaryPrimitives.ReadUInt32BigEndian(font.WrittenSpan) != OpenTypeVersion)
            {
                // An OpenType font with TrueType outlines is cut down like a /FontFile2 program.
                return TrueTypeSubsetter.Subset(program, glyphs, scratchPools, cancellationToken);
            }

            var memory = font.Array.AsMemory(0, font.Length);
            var cut = IsSfnt(font.WrittenSpan) ? CutOpenType(memory, glyphs, fontName, cancellationToken) : CutCff(memory, glyphs, fontName);
            return cut is { } bytes && bytes.Length < font.Length ? Compress(program, bytes, scratchPools) : null;
        }
        finally
        {
            font.Dispose();
        }
    }

    /// <summary>Determines whether a font program is an sfnt (OpenType) font rather than bare CFF data.</summary>
    /// <param name="font">The decoded program.</param>
    /// <returns><see langword="true"/> for an sfnt.</returns>
    internal static bool IsSfnt(ReadOnlySpan<byte> font) => font.Length >= HeaderSize && font[0] != CffMajorVersion;

    /// <summary>Lists the glyphs of a set.</summary>
    /// <param name="set">The set.</param>
    /// <returns>The glyph numbers.</returns>
    private static IEnumerable<int> Bits(CodeBitSet set)
    {
        for (var bit = set.NextBit(0); bit != CodeBitSet.NoBit; bit = set.NextBit(bit + 1))
        {
            yield return bit;
        }

        if (set.Overflow is not { } overflow)
        {
            yield break;
        }

        foreach (var glyph in overflow)
        {
            yield return glyph;
        }
    }

    /// <summary>Cuts a bare CFF program down.</summary>
    /// <param name="cff">The decoded program.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="fontName">The name for the Name INDEX.</param>
    /// <returns>The new program, or <see langword="null"/> when it cannot be read.</returns>
    private static byte[]? CutCff(ReadOnlyMemory<byte> cff, CodeBitSet glyphs, string fontName)
    {
        try
        {
            return Fonts.Embedding.CffSubsetter.KeepNumbers(cff, Bits(glyphs), fontName);
        }
        catch (PdfException)
        {
            return null;
        }
    }

    /// <summary>Cuts an OpenType font with CFF outlines down.</summary>
    /// <param name="font">The decoded font.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="fontName">The name for the Name INDEX.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The new font, or <see langword="null"/> when the font cannot be cut down.</returns>
    private static byte[]? CutOpenType(ReadOnlyMemory<byte> font, CodeBitSet glyphs, string fontName, CancellationToken cancellationToken)
    {
        var data = font.Span;
        if (BinaryPrimitives.ReadUInt32BigEndian(data) != OpenTypeVersion)
        {
            return null;
        }

        var tables = default(RentedList<SfntTable>);
        var entries = default(RentedList<SfntEntry>);
        try
        {
            if (!TryReadTables(data, ref tables) || ForbidsSubsetting(data, tables.Span))
            {
                return null;
            }

            var cffTable = Find(tables.Span, CffTag);
            if (cffTable.Length == 0 || CutCff(font.Slice(cffTable.Offset, cffTable.Length), glyphs, fontName) is not { } cff)
            {
                return null;
            }

            var head = CopyHead(data, Find(tables.Span, HeadTag));
            foreach (var table in tables.Span)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!DroppedTables.Contains(table.Tag))
                {
                    entries.Add(new(table.Tag, ContentOf(font, table, cff, head)));
                }
            }

            return SfntWriter.Write(OpenTypeVersion, entries.Span, HeadTag, HeadChecksum, ChecksumMagic, cancellationToken);
        }
        finally
        {
            tables.Dispose();
            entries.Dispose();
        }
    }

    /// <summary>Reads the table directory.</summary>
    /// <param name="data">The font.</param>
    /// <param name="tables">Receives the tables.</param>
    /// <returns><see langword="true"/> when every table lies inside the font.</returns>
    private static bool TryReadTables(ReadOnlySpan<byte> data, ref RentedList<SfntTable> tables)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(data[TableCountOffset..]);
        for (var i = 0; i < count; i++)
        {
            var record = HeaderSize + (i * RecordSize);
            if (record + RecordSize > data.Length)
            {
                return false;
            }

            var offset = BinaryPrimitives.ReadUInt32BigEndian(data[(record + RecordOffset)..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(data[(record + RecordLength)..]);
            if (offset > (uint)data.Length || length > (uint)data.Length - offset)
            {
                return false;
            }

            tables.Add(new(BinaryPrimitives.ReadUInt32BigEndian(data[record..]), (int)offset, (int)length));
        }

        return true;
    }

    /// <summary>Determines whether the font's licence forbids subsetting.</summary>
    /// <param name="data">The font.</param>
    /// <param name="tables">The tables.</param>
    /// <returns><see langword="true"/> when the no-subsetting flag is set.</returns>
    private static bool ForbidsSubsetting(ReadOnlySpan<byte> data, ReadOnlySpan<SfntTable> tables)
    {
        var os2 = Find(tables, Os2Tag);
        return os2.Length >= Os2FsType + sizeof(ushort) && (BinaryPrimitives.ReadUInt16BigEndian(data[(os2.Offset + Os2FsType)..]) & NoSubsetting) != 0;
    }

    /// <summary>Finds a table.</summary>
    /// <param name="tables">The tables.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The table, or an empty one.</returns>
    private static SfntTable Find(ReadOnlySpan<SfntTable> tables, uint tag)
    {
        foreach (var table in tables)
        {
            if (table.Tag == tag)
            {
                return table;
            }
        }

        return default;
    }

    /// <summary>Copies /head with a cleared checksum adjustment, which the writer sets last.</summary>
    /// <param name="data">The font.</param>
    /// <param name="head">The /head table.</param>
    /// <returns>The copy, or <see langword="null"/> when the font has no usable /head.</returns>
    private static byte[]? CopyHead(ReadOnlySpan<byte> data, SfntTable head)
    {
        if (head.Length < MinHeadSize)
        {
            return null;
        }

        var copy = data.Slice(head.Offset, head.Length).ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(HeadChecksum), 0);
        return copy;
    }

    /// <summary>Gets the bytes a table has in the new font.</summary>
    /// <param name="font">The source font.</param>
    /// <param name="table">The source table.</param>
    /// <param name="cff">The new CFF table.</param>
    /// <param name="head">The new /head table, or <see langword="null"/>.</param>
    /// <returns>The table's content.</returns>
    private static ReadOnlyMemory<byte> ContentOf(ReadOnlyMemory<byte> font, SfntTable table, byte[] cff, byte[]? head) => table.Tag switch
    {
        CffTag => cff,
        HeadTag when head is not null => head,
        _ => font.Slice(table.Offset, table.Length),
    };

    /// <summary>Compresses the subset into a new /FontFile3 stream with the original's other entries.</summary>
    /// <param name="program">The original stream.</param>
    /// <param name="subset">The subset font.</param>
    /// <param name="scratchPools">The compression owner.</param>
    /// <returns>The stream, or <see langword="null"/> when it is not smaller than the original.</returns>
    private static PdfStream? Compress(PdfStream program, ReadOnlySpan<byte> subset, ScratchPools scratchPools)
    {
        var compressed = default(PooledBuffer);
        try
        {
            StreamRecompressor.Deflate(subset, ref compressed, scratchPools);
            if (compressed.Length >= program.RawLength)
            {
                return null;
            }

            var dictionary = program.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Length);
            _ = dictionary.Remove(KnownName.DecodeParms);
            _ = dictionary.Remove(KnownName.DL);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
