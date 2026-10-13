// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Cuts an embedded TrueType program down to the glyphs a document shows while keeping every glyph number: unused
/// glyphs keep their slot in /loca with no outline, so /Widths, /W, CIDToGIDMap, cmap and the content streams stay valid
/// unchanged. Components of composite glyphs are kept with them, glyph 0 is always kept, and layout and signature tables
/// that PDF drawing never reads are dropped. A font whose licence forbids subsetting is left whole.
/// </summary>
internal static class TrueTypeSubsetter
{
    /// <summary>The sfnt version of TrueType outlines.</summary>
    private const uint VersionOne = 0x00010000;

    /// <summary>The Apple sfnt version of TrueType outlines.</summary>
    private const uint VersionTrue = 0x74727565;

    /// <summary>The tag of the glyph data table.</summary>
    private const uint GlyfTag = 0x676C7966;

    /// <summary>The tag of the glyph location table.</summary>
    private const uint LocaTag = 0x6C6F6361;

    /// <summary>The tag of the font header table.</summary>
    private const uint HeadTag = 0x68656164;

    /// <summary>The tag of the maximum profile table.</summary>
    private const uint MaxpTag = 0x6D617870;

    /// <summary>The tag of the character to glyph mapping table.</summary>
    private const uint CmapTag = 0x636D6170;

    /// <summary>The tag of the OS/2 table.</summary>
    private const uint Os2Tag = 0x4F532F32;

    /// <summary>The offset of the table count in the directory.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The size of the directory header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of one table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of a record's checksum.</summary>
    private const int RecordChecksum = 4;

    /// <summary>The offset of a record's table offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of a record's table length.</summary>
    private const int RecordLength = 12;

    /// <summary>The offset of the glyph count in /maxp.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The offset of the location format in /head.</summary>
    private const int HeadLocaFormat = 50;

    /// <summary>The offset of the checksum adjustment in /head.</summary>
    private const int HeadChecksum = 8;

    /// <summary>The smallest /head table.</summary>
    private const int HeadSize = 54;

    /// <summary>The offset of the embedding flags in /OS/2.</summary>
    private const int Os2FsType = 8;

    /// <summary>The embedding flag that forbids subsetting.</summary>
    private const int NoSubsetting = 0x0100;

    /// <summary>The value the whole font's checksum adds up to.</summary>
    private const uint ChecksumMagic = 0xB1B0AFBA;

    /// <summary>The alignment of every table.</summary>
    private const int TableAlignment = 4;

    /// <summary>The bytes of a long /loca entry.</summary>
    private const int LongEntry = 4;

    /// <summary>The bytes of a short /loca entry.</summary>
    private const int ShortEntry = 2;

    /// <summary>Gets the zero bytes that pad a glyph to the table alignment.</summary>
    private static ReadOnlySpan<byte> Padding => [0x00, 0x00, 0x00, 0x00];

    /// <summary>Gets the tags of tables PDF drawing never reads: digital signatures and layout tables.</summary>
    private static ReadOnlySpan<uint> DroppedTables =>
    [
        0x44534947, // DSIG
        0x47535542, // GSUB
        0x47504F53, // GPOS
        0x47444546, // GDEF
        0x4A535446, // JSTF
        0x42415345, // BASE
    ];

    /// <summary>Subsets a font program.</summary>
    /// <param name="program">The /FontFile2 stream.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="names">The optimiser's names.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The subset program stream, or <see langword="null"/> when the font cannot be subset or nothing is gained.</returns>
    internal static PdfStream? Subset(PdfStream program, CodeBitSet glyphs, OptimizerNames names, CancellationToken cancellationToken)
    {
        var font = program.DecodeToArray(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryReadTables(font, out var tables) || ForbidsSubsetting(font, Find(tables, Os2Tag)))
        {
            return null;
        }

        var glyf = Find(tables, GlyfTag);
        var head = Find(tables, HeadTag);
        var maxp = Find(tables, MaxpTag);
        if (glyf.Length == 0 || head.Length < HeadSize || maxp.Length < MaxpGlyphs + ShortEntry)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(maxp.Offset + MaxpGlyphs));
        var longLoca = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(head.Offset + HeadLocaFormat)) == 1;
        if (!TryReadLoca(font, Find(tables, LocaTag), glyf, count, longLoca, out var offsets))
        {
            return null;
        }

        var keep = GlyphClosure.Find(font.AsSpan(glyf.Offset, glyf.Length), offsets, glyphs);
        var subset = Rebuild(font, tables, offsets, keep, default);
        return subset.Length < font.Length ? Compress(program, subset, names) : null;
    }

    /// <summary>
    /// Cuts a whole TrueType font down to some glyphs and gives it a new cmap table, for embedding a substitute font. Glyph
    /// numbers are kept, as in <see cref="Subset"/>.
    /// </summary>
    /// <param name="font">The font program.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="cmap">The cmap table to write in place of the font's own.</param>
    /// <returns>The new font, or <see langword="null"/> when the font is not a whole TrueType-outline font.</returns>
    internal static byte[]? SubsetWithCmap(byte[] font, CodeBitSet glyphs, byte[] cmap)
    {
        if (!TryReadTables(font, out var tables))
        {
            return null;
        }

        var glyf = Find(tables, GlyfTag);
        var head = Find(tables, HeadTag);
        var maxp = Find(tables, MaxpTag);
        if (glyf.Length == 0 || head.Length < HeadSize || maxp.Length < MaxpGlyphs + ShortEntry)
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(maxp.Offset + MaxpGlyphs));
        var longLoca = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(head.Offset + HeadLocaFormat)) == 1;
        if (!TryReadLoca(font, Find(tables, LocaTag), glyf, count, longLoca, out var offsets))
        {
            return null;
        }

        var keep = GlyphClosure.Find(font.AsSpan(glyf.Offset, glyf.Length), offsets, glyphs);
        return Rebuild(font, tables, offsets, keep, cmap);
    }

    /// <summary>Reads the table directory of a single TrueType font.</summary>
    /// <param name="font">The font.</param>
    /// <param name="tables">The tables, clamped to the font.</param>
    /// <returns><see langword="true"/> for a TrueType-outline sfnt with whole tables.</returns>
    private static bool TryReadTables(byte[] font, out List<SfntTable> tables)
    {
        tables = [];
        if (font.Length < HeaderSize || BinaryPrimitives.ReadUInt32BigEndian(font) is not (VersionOne or VersionTrue))
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(TableCountOffset));
        for (var i = 0; i < count; i++)
        {
            var record = HeaderSize + (i * RecordSize);
            if (record + RecordSize > font.Length)
            {
                return false;
            }

            var offset = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + RecordOffset));
            var length = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + RecordLength));
            if (offset > (uint)font.Length || length > (uint)font.Length - offset)
            {
                return false;
            }

            tables.Add(new(BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record)), (int)offset, (int)length));
        }

        return true;
    }

    /// <summary>Finds a table.</summary>
    /// <param name="tables">The tables.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The table, or an empty one.</returns>
    private static SfntTable Find(List<SfntTable> tables, uint tag)
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

    /// <summary>Determines whether the font's licence forbids subsetting.</summary>
    /// <param name="font">The font.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns><see langword="true"/> when the no-subsetting flag is set.</returns>
    private static bool ForbidsSubsetting(byte[] font, SfntTable os2) =>
        os2.Length >= Os2FsType + ShortEntry && (BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(os2.Offset + Os2FsType)) & NoSubsetting) != 0;

    /// <summary>Reads the glyph offsets from /loca, checking they lie inside /glyf in order.</summary>
    /// <param name="font">The font.</param>
    /// <param name="loca">The /loca table.</param>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="count">The glyph count.</param>
    /// <param name="longLoca">Whether entries are 32-bit offsets rather than 16-bit halves.</param>
    /// <param name="offsets">The count plus one offsets into /glyf.</param>
    /// <returns><see langword="true"/> when the table is whole and consistent.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadLoca(byte[] font, SfntTable loca, SfntTable glyf, int count, bool longLoca, out int[] offsets) =>
        TrueTypeLocations.TryRead(font.AsSpan(loca.Offset, loca.Length), glyf.Length, count, longLoca, out offsets);

    /// <summary>Writes the font again with only the kept glyphs' outlines and a long /loca.</summary>
    /// <param name="font">The font.</param>
    /// <param name="tables">Its tables.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="keep">Whether each glyph is kept.</param>
    /// <param name="cmap">A cmap table to write in place of the font's own, or empty to keep it.</param>
    /// <returns>The new font.</returns>
    private static byte[] Rebuild(byte[] font, List<SfntTable> tables, int[] offsets, bool[] keep, ReadOnlyMemory<byte> cmap)
    {
        var newGlyf = KeptGlyphs(font, Find(tables, GlyfTag), offsets, keep, out var newLoca);
        var head = font.AsSpan(Find(tables, HeadTag).Offset, Find(tables, HeadTag).Length).ToArray();
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(HeadLocaFormat), 1);
        BinaryPrimitives.WriteUInt32BigEndian(head.AsSpan(HeadChecksum), 0);
        var kept = new List<SfntEntry>(tables.Count);
        foreach (var table in tables)
        {
            if (DroppedTables.Contains(table.Tag))
            {
                continue;
            }

            // Unchanged tables are slices of the source font, which SfntWriter copies once into the new font.
            kept.Add(new(table.Tag, table.Tag switch
            {
                GlyfTag => newGlyf,
                LocaTag => newLoca,
                HeadTag => head,
                CmapTag when !cmap.IsEmpty => cmap,
                _ => font.AsMemory(table.Offset, table.Length),
            }));
        }

        return SfntWriter.Write(BinaryPrimitives.ReadUInt32BigEndian(font), [.. kept], HeadTag, HeadChecksum, ChecksumMagic);
    }

    /// <summary>Copies the kept glyphs' outlines into a new /glyf, each padded to four bytes, and builds a long /loca for it.</summary>
    /// <param name="font">The font.</param>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="keep">Whether each glyph is kept.</param>
    /// <param name="loca">Receives the new /loca.</param>
    /// <returns>The new /glyf.</returns>
    private static byte[] KeptGlyphs(byte[] font, SfntTable glyf, int[] offsets, bool[] keep, out byte[] loca)
    {
        loca = new byte[offsets.Length * LongEntry];
        var data = default(PooledBuffer);
        try
        {
            for (var g = 0; g < keep.Length; g++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(g * LongEntry), (uint)data.Length);
                if (!keep[g] || offsets[g + 1] <= offsets[g])
                {
                    continue;
                }

                data.Write(font.AsSpan(glyf.Offset + offsets[g], offsets[g + 1] - offsets[g]));
                data.Write(Padding[..((TableAlignment - (data.Length % TableAlignment)) % TableAlignment)]);
            }

            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(keep.Length * LongEntry), (uint)data.Length);
            return data.ToArray();
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>Compresses the subset into a new /FontFile2 stream with /Length1 updated.</summary>
    /// <param name="program">The original stream.</param>
    /// <param name="subset">The subset font.</param>
    /// <param name="names">The optimiser's names.</param>
    /// <returns>The stream, or <see langword="null"/> when it is not smaller than the original.</returns>
    private static PdfStream? Compress(PdfStream program, byte[] subset, OptimizerNames names)
    {
        var compressed = default(PooledBuffer);
        try
        {
            StreamRecompressor.Deflate(subset, ref compressed);
            if (compressed.Length >= program.RawLength)
            {
                return null;
            }

            var dictionary = program.Dictionary.Clone();
            _ = dictionary.Remove(KnownName.Length);
            _ = dictionary.Remove(KnownName.DecodeParms);
            _ = dictionary.Remove(KnownName.DL);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            dictionary.Set(names.Length1, PdfValue.FromInteger(subset.Length));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }
}
