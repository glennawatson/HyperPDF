// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The working state of cutting one TrueType font down to some glyphs: its table directory, the kept glyphs, and the
/// rebuilt /glyf, /loca and /head tables. Every list and buffer comes from a pool; dispose it when the font is written.
/// </summary>
internal ref struct TrueTypeRebuild
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

    /// <summary>The glyph batch between cancellation checks.</summary>
    private const int GlyphBatch = 256;

    /// <summary>The source font.</summary>
    private readonly ReadOnlyMemory<byte> _font;

    /// <summary>Stops the font rebuild.</summary>
    private readonly CancellationToken _cancellationToken;

    /// <summary>The source font's tables.</summary>
    private RentedList<SfntTable> _tables;

    /// <summary>The glyph offsets into /glyf, one more than the glyph count; rented.</summary>
    private int[]? _offsets;

    /// <summary>Whether each glyph is kept; rented.</summary>
    private bool[]? _keep;

    /// <summary>The number of glyphs.</summary>
    private int _glyphCount;

    /// <summary>The rebuilt /glyf.</summary>
    private PooledBuffer _glyf;

    /// <summary>The rebuilt /loca.</summary>
    private PooledBuffer _loca;

    /// <summary>The rebuilt /head; rented.</summary>
    private byte[]? _head;

    /// <summary>The length of the rebuilt /head.</summary>
    private int _headLength;

    /// <summary>The tables of the new font.</summary>
    private RentedList<SfntEntry> _entries;

    /// <summary>Initializes a new instance of the <see cref="TrueTypeRebuild"/> struct.</summary>
    /// <param name="font">The source font.</param>
    /// <param name="cancellationToken">Stops the font rebuild.</param>
    internal TrueTypeRebuild(ReadOnlyMemory<byte> font, CancellationToken cancellationToken)
    {
        _font = font;
        _cancellationToken = cancellationToken;
    }

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

    /// <summary>Gets the zero bytes that pad a glyph to the table alignment.</summary>
    private static ReadOnlySpan<byte> Padding => [0x00, 0x00, 0x00, 0x00];

    /// <summary>Reads the font's directory and glyph locations and works out which glyphs are kept.</summary>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <param name="honourLicence">Whether a font whose licence forbids subsetting is refused.</param>
    /// <returns><see langword="true"/> for a TrueType-outline font with whole tables that may be cut down.</returns>
    internal bool TryRead(CodeBitSet glyphs, bool honourLicence)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var font = _font.Span;
        if (!TryReadTables(font) || (honourLicence && ForbidsSubsetting(font, Find(Os2Tag))))
        {
            return false;
        }

        var glyf = Find(GlyfTag);
        var head = Find(HeadTag);
        var maxp = Find(MaxpTag);
        if (glyf.Length == 0 || head.Length < HeadSize || maxp.Length < MaxpGlyphs + ShortEntry)
        {
            return false;
        }

        _glyphCount = BinaryPrimitives.ReadUInt16BigEndian(font[(maxp.Offset + MaxpGlyphs)..]);
        var longLoca = BinaryPrimitives.ReadInt16BigEndian(font[(head.Offset + HeadLocaFormat)..]) == 1;
        var loca = Find(LocaTag);
        _offsets = ArrayPool<int>.Shared.Rent(_glyphCount + 1);
        if (!TrueTypeLocations.TryRead(font.Slice(loca.Offset, loca.Length), glyf.Length, _glyphCount, longLoca, _offsets.AsSpan(0, _glyphCount + 1)))
        {
            return false;
        }

        _keep = ArrayPool<bool>.Shared.Rent(_glyphCount);
        GlyphClosure.Find(font.Slice(glyf.Offset, glyf.Length), _offsets.AsSpan(0, _glyphCount + 1), glyphs, _keep.AsSpan(0, _glyphCount), _cancellationToken);
        return true;
    }

    /// <summary>Writes the font again with only the kept glyphs' outlines and a long /loca.</summary>
    /// <param name="cmap">A cmap table to write in place of the font's own, or empty to keep it.</param>
    /// <param name="output">Receives the new font.</param>
    internal void Write(ReadOnlyMemory<byte> cmap, ref PooledBuffer output)
    {
        var font = _font.Span;
        WriteGlyphs(font);
        CopyHead(font);
        foreach (var table in _tables.Span)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (!DroppedTables.Contains(table.Tag))
            {
                _entries.Add(new(table.Tag, ContentOf(table, cmap)));
            }
        }

        SfntWriter.Write(ref output, BinaryPrimitives.ReadUInt32BigEndian(font), _entries.Span, HeadTag, HeadChecksum, ChecksumMagic, _cancellationToken);
    }

    /// <summary>Gives every list and buffer back to its pool.</summary>
    internal void Dispose()
    {
        _tables.Dispose();
        _entries.Dispose();
        _glyf.Dispose();
        _loca.Dispose();
        Release(ref _offsets);
        Release(ref _keep);
        Release(ref _head);
    }

    /// <summary>Determines whether the font's licence forbids subsetting.</summary>
    /// <param name="font">The font.</param>
    /// <param name="os2">The /OS/2 table.</param>
    /// <returns><see langword="true"/> when the no-subsetting flag is set.</returns>
    private static bool ForbidsSubsetting(ReadOnlySpan<byte> font, SfntTable os2) =>
        os2.Length >= Os2FsType + ShortEntry && (BinaryPrimitives.ReadUInt16BigEndian(font[(os2.Offset + Os2FsType)..]) & NoSubsetting) != 0;

    /// <summary>Gives a rented array back and forgets it.</summary>
    /// <typeparam name="T">The entry type.</typeparam>
    /// <param name="array">The array, or <see langword="null"/>; set to null.</param>
    private static void Release<T>(ref T[]? array)
    {
        if (array is null)
        {
            return;
        }

        ArrayPool<T>.Shared.Return(array);
        array = null;
    }

    /// <summary>Finds a table of the source font.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns>The table, or an empty one.</returns>
    private readonly SfntTable Find(uint tag)
    {
        foreach (var table in _tables.Span)
        {
            if (table.Tag == tag)
            {
                return table;
            }
        }

        return default;
    }

    /// <summary>Reads the table directory of a single TrueType font.</summary>
    /// <param name="font">The font.</param>
    /// <returns><see langword="true"/> for a TrueType-outline sfnt with whole tables.</returns>
    private bool TryReadTables(ReadOnlySpan<byte> font)
    {
        if (font.Length < HeaderSize || BinaryPrimitives.ReadUInt32BigEndian(font) is not (VersionOne or VersionTrue))
        {
            return false;
        }

        var count = BinaryPrimitives.ReadUInt16BigEndian(font[TableCountOffset..]);
        for (var i = 0; i < count; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var record = HeaderSize + (i * RecordSize);
            if (record + RecordSize > font.Length)
            {
                return false;
            }

            var offset = BinaryPrimitives.ReadUInt32BigEndian(font[(record + RecordOffset)..]);
            var length = BinaryPrimitives.ReadUInt32BigEndian(font[(record + RecordLength)..]);
            if (offset > (uint)font.Length || length > (uint)font.Length - offset)
            {
                return false;
            }

            _tables.Add(new(BinaryPrimitives.ReadUInt32BigEndian(font[record..]), (int)offset, (int)length));
        }

        return true;
    }

    /// <summary>Copies the kept glyphs' outlines into a new /glyf, each padded to four bytes, and builds a long /loca for it.</summary>
    /// <param name="font">The source font.</param>
    private void WriteGlyphs(ReadOnlySpan<byte> font)
    {
        var glyf = Find(GlyfTag);
        var offsets = _offsets.AsSpan(0, _glyphCount + 1);
        var keep = _keep.AsSpan(0, _glyphCount);
        var loca = _loca.GetSpan((_glyphCount + 1) * LongEntry);
        for (var g = 0; g < keep.Length; g++)
        {
            if ((g & (GlyphBatch - 1)) == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
            }

            BinaryPrimitives.WriteUInt32BigEndian(loca[(g * LongEntry)..], (uint)_glyf.Length);
            if (!keep[g] || offsets[g + 1] <= offsets[g])
            {
                continue;
            }

            _glyf.Write(font.Slice(glyf.Offset + offsets[g], offsets[g + 1] - offsets[g]));
            _glyf.Write(Padding[..((TableAlignment - (_glyf.Length % TableAlignment)) % TableAlignment)]);
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca[(keep.Length * LongEntry)..], (uint)_glyf.Length);
        _loca.Advance((_glyphCount + 1) * LongEntry);
    }

    /// <summary>Copies /head with long glyph locations and a cleared checksum adjustment, which the writer sets last.</summary>
    /// <param name="font">The source font.</param>
    private void CopyHead(ReadOnlySpan<byte> font)
    {
        var head = Find(HeadTag);
        _headLength = head.Length;
        _head = ArrayPool<byte>.Shared.Rent(head.Length);
        font.Slice(head.Offset, head.Length).CopyTo(_head);
        BinaryPrimitives.WriteInt16BigEndian(_head.AsSpan(HeadLocaFormat), 1);
        BinaryPrimitives.WriteUInt32BigEndian(_head.AsSpan(HeadChecksum), 0);
    }

    /// <summary>Gets the bytes a table has in the new font.</summary>
    /// <param name="table">The source table.</param>
    /// <param name="cmap">The replacement cmap, or empty.</param>
    /// <returns>The rebuilt table or a slice of the source font; unchanged tables are copied once by the writer.</returns>
    private readonly ReadOnlyMemory<byte> ContentOf(SfntTable table, ReadOnlyMemory<byte> cmap) => table.Tag switch
    {
        GlyfTag => _glyf.Array.AsMemory(0, _glyf.Length),
        LocaTag => _loca.Array.AsMemory(0, _loca.Length),
        HeadTag => _head.AsMemory(0, _headLength),
        CmapTag when !cmap.IsEmpty => cmap,
        _ => _font.Slice(table.Offset, table.Length),
    };
}
