// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>
/// Cuts a TrueType face down to the glyphs some text uses, so an embedded font carries only what the page shows.
/// Glyphs are renumbered from 0, parts of composite glyphs are kept with them, and the tables PDF does not need for
/// drawing (layout, kerning, signatures) are dropped. A face whose licence forbids subsetting keeps every glyph.
/// </summary>
public static class FontSubsetter
{
    /// <summary>The bytes of a glyph's header.</summary>
    private const int GlyphHeaderBytes = 10;

    /// <summary>The composite flag: arguments are 16-bit.</summary>
    private const int ArgsAreWords = 0x0001;

    /// <summary>The composite flag: one scale follows.</summary>
    private const int HasScale = 0x0008;

    /// <summary>The composite flag: more components follow.</summary>
    private const int MoreComponents = 0x0020;

    /// <summary>The composite flag: separate x and y scales follow.</summary>
    private const int HasXyScale = 0x0040;

    /// <summary>The composite flag: a two by two matrix follows.</summary>
    private const int HasMatrix = 0x0080;

    /// <summary>The bytes of a component's flags and glyph index.</summary>
    private const int ComponentHeaderBytes = 4;

    /// <summary>The bytes of two 16-bit arguments.</summary>
    private const int WordArgsBytes = 4;

    /// <summary>The bytes of two 8-bit arguments.</summary>
    private const int ByteArgsBytes = 2;

    /// <summary>The bytes of one 2.14 scale.</summary>
    private const int ScaleBytes = 2;

    /// <summary>The bytes of two 2.14 scales.</summary>
    private const int XyScaleBytes = 4;

    /// <summary>The bytes of a 2.14 matrix.</summary>
    private const int MatrixBytes = 8;

    /// <summary>The bytes of a 16-bit value.</summary>
    private const int Int16Bytes = 2;

    /// <summary>The bytes of a 32-bit value.</summary>
    private const int Int32Bytes = 4;

    /// <summary>The bytes of one long horizontal metric.</summary>
    private const int MetricBytes = 4;

    /// <summary>The offset of the metric count in the horizontal header.</summary>
    private const int HheaMetricCount = 34;

    /// <summary>The offset of the glyph count in the maximum profile.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The offset of the checksum adjustment in the font header.</summary>
    private const int HeadChecksum = 8;

    /// <summary>The offset of the location format in the font header.</summary>
    private const int HeadLocaFormat = 50;

    /// <summary>The bytes of a version 3 PostScript table, which has no glyph names.</summary>
    private const int PostBytes = 32;

    /// <summary>The PostScript table version without glyph names.</summary>
    private const uint PostVersion3 = 0x00030000;

    /// <summary>The value the whole font's checksum adds up to.</summary>
    private const uint ChecksumMagic = 0xB1B0AFBA;

    /// <summary>The TrueType outline version.</summary>
    private const uint TrueTypeVersion = 0x00010000;

    /// <summary>The alignment of tables and glyphs.</summary>
    private const int Alignment = 4;

    /// <summary>The largest Basic Multilingual Plane code point a segment map can hold.</summary>
    private const int LastBmp = 0xFFFE;

    /// <summary>The bytes of a format 4 subtable's fixed part.</summary>
    private const int SegmentHeaderBytes = 16;

    /// <summary>The bytes each segment takes in a format 4 subtable.</summary>
    private const int SegmentBytes = 8;

    /// <summary>The bytes of the cmap header and one encoding record.</summary>
    private const int CmapHeaderBytes = 12;

    /// <summary>The segment map format.</summary>
    private const int SegmentFormat = 4;

    /// <summary>The Windows platform.</summary>
    private const int WindowsPlatform = 3;

    /// <summary>The Windows Unicode BMP encoding.</summary>
    private const int UnicodeBmpEncoding = 1;

    /// <summary>The end code that closes a segment map.</summary>
    private const ushort LastSegment = 0xFFFF;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The highest power of two not above a number, used for binary search fields.</summary>
    private const int SearchMaxShift = 15;

    /// <summary>The offset of the segment count in a format 4 subtable.</summary>
    private const int SegCountAt = 6;

    /// <summary>The offset of the end codes in a format 4 subtable.</summary>
    private const int EndCodesAt = 14;

    /// <summary>The offset of the subtable length in a format 4 subtable.</summary>
    private const int SubtableLengthAt = 2;

    /// <summary>The offset of the binary search fields in a format 4 subtable.</summary>
    private const int SearchFieldsAt = 8;

    /// <summary>The offset of the encoding in the cmap's encoding record.</summary>
    private const int EncodingAt = 6;

    /// <summary>The offset of the subtable offset in the cmap's encoding record.</summary>
    private const int SubtableOffsetAt = 8;

    /// <summary>The offset of the platform in the cmap's encoding record.</summary>
    private const int PlatformAt = 4;

    /// <summary>The most characters the subset's own character map lists; PDF readers draw from the glyph numbers, not this map.</summary>
    private const int MaxSegments = 8192;

    /// <summary>The offset of a table's checksum in its record.</summary>
    private const int RecordChecksumAt = 4;

    /// <summary>The offset of a table's offset in its record.</summary>
    private const int RecordOffsetAt = 8;

    /// <summary>The offset of a table's length in its record.</summary>
    private const int RecordLengthAt = 12;

    /// <summary>The offset of the table count in the offset table.</summary>
    private const int TableCountAt = 4;

    /// <summary>The offset of the search range in the offset table.</summary>
    private const int SearchRangeAt = 6;

    /// <summary>The offset of the entry selector in the offset table.</summary>
    private const int EntrySelectorAt = 8;

    /// <summary>The offset of the range shift in the offset table.</summary>
    private const int RangeShiftAt = 10;

    /// <summary>The tables copied unchanged.</summary>
    private static readonly uint[] CopiedTables = [SfntTag.Name, SfntTag.Os2, SfntTag.Cvt, SfntTag.Fpgm, SfntTag.Prep, SfntTag.Gasp];

    /// <summary>Cuts a face down to some glyphs; glyph 0 and the parts of composite glyphs are always kept.</summary>
    /// <param name="font">The face; it must have TrueType outlines.</param>
    /// <param name="glyphs">The glyphs to keep.</param>
    /// <returns>The subset, or <see langword="null"/> when the face has no TrueType outlines.</returns>
    public static FontSubset? Create(FontProgram font, IEnumerable<ushort> glyphs)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(glyphs);
        var data = font.Data;
        var loca = font.Directory.Table(data, SfntTag.Loca);
        var glyf = font.Directory.Table(data, SfntTag.Glyf);
        if (loca.IsEmpty || glyf.IsEmpty || font.GlyphCount == 0)
        {
            return null;
        }

        var keep = new bool[font.GlyphCount];
        keep[0] = true;
        if (font.Face.CanSubset)
        {
            foreach (var glyph in glyphs)
            {
                Keep(glyph, keep, loca, glyf, font.IsLongLoca);
            }
        }
        else
        {
            Array.Fill(keep, true);
        }

        var map = new ushort[font.GlyphCount];
        var original = new List<ushort>();
        for (var glyph = 0; glyph < keep.Length; glyph++)
        {
            if (!keep[glyph])
            {
                continue;
            }

            map[glyph] = (ushort)original.Count;
            original.Add((ushort)glyph);
        }

        return new(Build(font, map, original), map, [.. original]);
    }

    /// <summary>Keeps a glyph and, for a composite glyph, its parts.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="keep">Which glyphs are kept.</param>
    /// <param name="loca">The location table.</param>
    /// <param name="glyf">The glyph table.</param>
    /// <param name="longLoca">Whether locations are 32-bit.</param>
    private static void Keep(ushort glyph, bool[] keep, ReadOnlySpan<byte> loca, ReadOnlySpan<byte> glyf, bool longLoca)
    {
        var pending = new Stack<ushort>();
        pending.Push(glyph);
        while (pending.TryPop(out var next))
        {
            if (next >= keep.Length || (keep[next] && next != glyph))
            {
                continue;
            }

            keep[next] = true;
            PushParts(GlyphData(next, loca, glyf, longLoca), keep, pending);
        }
    }

    /// <summary>Queues the parts of a composite glyph that are not kept yet.</summary>
    /// <param name="outline">The glyph's outline.</param>
    /// <param name="keep">Which glyphs are kept.</param>
    /// <param name="pending">The glyphs still to keep.</param>
    private static void PushParts(ReadOnlySpan<byte> outline, bool[] keep, Stack<ushort> pending)
    {
        if (outline.Length < GlyphHeaderBytes || BinaryPrimitives.ReadInt16BigEndian(outline) >= 0)
        {
            return;
        }

        for (var at = GlyphHeaderBytes; ComponentAt(outline, at, out var flags, out var part); at = NextComponent(at, flags))
        {
            if (part < keep.Length && !keep[part])
            {
                pending.Push(part);
            }

            if ((flags & MoreComponents) == 0)
            {
                return;
            }
        }
    }

    /// <summary>Reads a composite glyph's component at an offset.</summary>
    /// <param name="outline">The composite glyph.</param>
    /// <param name="at">The component's offset.</param>
    /// <param name="flags">The component's flags.</param>
    /// <param name="part">The component's glyph.</param>
    /// <returns><see langword="true"/> when a whole component header is there.</returns>
    private static bool ComponentAt(ReadOnlySpan<byte> outline, int at, out int flags, out ushort part)
    {
        if (at + ComponentHeaderBytes > outline.Length)
        {
            flags = 0;
            part = 0;
            return false;
        }

        flags = BinaryPrimitives.ReadUInt16BigEndian(outline[at..]);
        part = BinaryPrimitives.ReadUInt16BigEndian(outline[(at + Int16Bytes)..]);
        return true;
    }

    /// <summary>Gets the offset of the component after one.</summary>
    /// <param name="at">The component's offset.</param>
    /// <param name="flags">Its flags.</param>
    /// <returns>The next component's offset.</returns>
    private static int NextComponent(int at, int flags)
    {
        var size = ComponentHeaderBytes + ((flags & ArgsAreWords) != 0 ? WordArgsBytes : ByteArgsBytes);
        if ((flags & HasScale) != 0)
        {
            size += ScaleBytes;
        }
        else if ((flags & HasXyScale) != 0)
        {
            size += XyScaleBytes;
        }
        else if ((flags & HasMatrix) != 0)
        {
            size += MatrixBytes;
        }

        return at + size;
    }

    /// <summary>Gets a glyph's outline bytes.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="loca">The location table.</param>
    /// <param name="glyf">The glyph table.</param>
    /// <param name="longLoca">Whether locations are 32-bit.</param>
    /// <returns>The outline, or empty for a glyph with none.</returns>
    private static ReadOnlySpan<byte> GlyphData(int glyph, ReadOnlySpan<byte> loca, ReadOnlySpan<byte> glyf, bool longLoca)
    {
        var entry = longLoca ? Int32Bytes : Int16Bytes;
        if (((glyph + 1) * entry) + entry > loca.Length)
        {
            return default;
        }

        var start = Location(loca[(glyph * entry)..], longLoca);
        var end = Location(loca[((glyph + 1) * entry)..], longLoca);
        return end <= start || end > glyf.Length ? default : glyf[(int)start..(int)end];
    }

    /// <summary>Reads one glyph location as a byte offset.</summary>
    /// <param name="entry">The location entry.</param>
    /// <param name="longLoca">Whether locations are 32-bit; short ones count 16-bit words.</param>
    /// <returns>The offset.</returns>
    private static long Location(ReadOnlySpan<byte> entry, bool longLoca) =>
        longLoca ? BinaryPrimitives.ReadUInt32BigEndian(entry) : BinaryPrimitives.ReadUInt16BigEndian(entry) * (long)Int16Bytes;

    /// <summary>Writes the subset font file.</summary>
    /// <param name="font">The face.</param>
    /// <param name="map">The new number of each original glyph.</param>
    /// <param name="original">The original glyph of each new one.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(FontProgram font, ushort[] map, List<ushort> original)
    {
        var data = font.Data;
        var directory = font.Directory;
        var (glyf, loca) = BuildGlyphs(font, map, original);
        var tables = new List<(uint Tag, byte[] Bytes)>
        {
            (SfntTag.Glyf, glyf),
            (SfntTag.Loca, loca),
            (SfntTag.Hmtx, BuildMetrics(font, original)),
            (SfntTag.Hhea, Patch(directory.Table(data, SfntTag.Hhea), HheaMetricCount, (ushort)original.Count)),
            (SfntTag.Maxp, Patch(directory.Table(data, SfntTag.Maxp), MaxpGlyphs, (ushort)original.Count)),
            (SfntTag.Head, BuildHead(directory.Table(data, SfntTag.Head))),
            (SfntTag.Post, BuildPost(directory.Table(data, SfntTag.Post))),
            (SfntTag.Cmap, BuildCmap(font, map)),
        };
        foreach (var tag in CopiedTables)
        {
            var table = directory.Table(data, tag);
            if (!table.IsEmpty)
            {
                tables.Add((tag, table.ToArray()));
            }
        }

        tables.Sort(static (a, b) => a.Tag.CompareTo(b.Tag));
        return Assemble(tables);
    }

    /// <summary>Copies the kept glyphs' outlines, renumbering composite parts, and writes 32-bit locations.</summary>
    /// <param name="font">The face.</param>
    /// <param name="map">The new number of each original glyph.</param>
    /// <param name="original">The original glyph of each new one.</param>
    /// <returns>The glyph and location tables.</returns>
    private static (byte[] Glyf, byte[] Loca) BuildGlyphs(FontProgram font, ushort[] map, List<ushort> original)
    {
        var sourceLoca = font.Directory.Table(font.Data, SfntTag.Loca);
        var sourceGlyf = font.Directory.Table(font.Data, SfntTag.Glyf);
        using var glyf = new MemoryStream();
        var loca = new byte[(original.Count + 1) * Int32Bytes];
        for (var i = 0; i < original.Count; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(i * Int32Bytes), (uint)glyf.Length);
            var outline = GlyphData(original[i], sourceLoca, sourceGlyf, font.IsLongLoca);
            if (outline.IsEmpty)
            {
                continue;
            }

            var copy = outline.ToArray();
            if (copy.Length >= GlyphHeaderBytes && BinaryPrimitives.ReadInt16BigEndian(copy) < 0)
            {
                Renumber(copy, map);
            }

            glyf.Write(copy);
            while (glyf.Length % Alignment != 0)
            {
                glyf.WriteByte(0);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(original.Count * Int32Bytes), (uint)glyf.Length);
        return (glyf.ToArray(), loca);
    }

    /// <summary>Renumbers the parts of a composite glyph in place.</summary>
    /// <param name="outline">The composite glyph.</param>
    /// <param name="map">The new number of each original glyph.</param>
    private static void Renumber(byte[] outline, ushort[] map)
    {
        for (var at = GlyphHeaderBytes; ComponentAt(outline, at, out var flags, out var part); at = NextComponent(at, flags))
        {
            BinaryPrimitives.WriteUInt16BigEndian(outline.AsSpan(at + Int16Bytes), part < map.Length ? map[part] : (ushort)0);
            if ((flags & MoreComponents) == 0)
            {
                return;
            }
        }
    }

    /// <summary>Writes a long metric (advance and left side bearing) for every kept glyph.</summary>
    /// <param name="font">The face.</param>
    /// <param name="original">The original glyph of each new one.</param>
    /// <returns>The metrics table.</returns>
    private static byte[] BuildMetrics(FontProgram font, List<ushort> original)
    {
        var hmtx = font.Directory.Table(font.Data, SfntTag.Hmtx);
        var hhea = font.Directory.Table(font.Data, SfntTag.Hhea);
        var longMetrics = hhea.Length > HheaMetricCount + 1 ? BinaryPrimitives.ReadUInt16BigEndian(hhea[HheaMetricCount..]) : 0;
        var metrics = new byte[original.Count * MetricBytes];
        for (var i = 0; i < original.Count; i++)
        {
            var glyph = original[i];
            var bearingAt = glyph < longMetrics ? (glyph * MetricBytes) + Int16Bytes : (longMetrics * MetricBytes) + ((glyph - longMetrics) * Int16Bytes);
            var bearing = bearingAt + Int16Bytes <= hmtx.Length ? BinaryPrimitives.ReadInt16BigEndian(hmtx[bearingAt..]) : (short)0;
            BinaryPrimitives.WriteUInt16BigEndian(metrics.AsSpan(i * MetricBytes), (ushort)font.Advance(glyph));
            BinaryPrimitives.WriteInt16BigEndian(metrics.AsSpan((i * MetricBytes) + Int16Bytes), bearing);
        }

        return metrics;
    }

    /// <summary>Copies the font header with 32-bit locations and a cleared checksum adjustment.</summary>
    /// <param name="head">The original header.</param>
    /// <returns>The header.</returns>
    private static byte[] BuildHead(ReadOnlySpan<byte> head)
    {
        var copy = head.ToArray();
        if (copy.Length >= HeadLocaFormat + Int16Bytes)
        {
            BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(HeadChecksum), 0);
            BinaryPrimitives.WriteInt16BigEndian(copy.AsSpan(HeadLocaFormat), 1);
        }

        return copy;
    }

    /// <summary>Writes a version 3 PostScript table, keeping the underline and pitch, without glyph names.</summary>
    /// <param name="post">The original table.</param>
    /// <returns>The table.</returns>
    private static byte[] BuildPost(ReadOnlySpan<byte> post)
    {
        var copy = new byte[PostBytes];
        post[..Math.Min(post.Length, PostBytes)].CopyTo(copy);
        BinaryPrimitives.WriteUInt32BigEndian(copy, PostVersion3);
        return copy;
    }

    /// <summary>Writes a Unicode segment map from the characters the kept glyphs show, one segment per character.</summary>
    /// <param name="font">The face.</param>
    /// <param name="map">The new number of each original glyph.</param>
    /// <returns>The character map table.</returns>
    private static byte[] BuildCmap(FontProgram font, ushort[] map)
    {
        var pairs = new List<(int CodePoint, ushort Glyph)>();
        font.CharacterMap.Map(1, LastBmp, pairs);
        var kept = new List<(ushort Code, short Delta)>(pairs.Count);
        foreach (var (codePoint, glyph) in pairs)
        {
            if (kept.Count < MaxSegments && glyph < map.Length && map[glyph] != 0)
            {
                kept.Add(((ushort)codePoint, (short)(map[glyph] - codePoint)));
            }
        }

        kept.Add((LastSegment, 1));
        return WriteSegments(kept);
    }

    /// <summary>Writes a cmap table holding one format 4 subtable.</summary>
    /// <param name="segments">The one-character segments, ending with 0xFFFF.</param>
    /// <returns>The table.</returns>
    private static byte[] WriteSegments(List<(ushort Code, short Delta)> segments)
    {
        var count = segments.Count;
        var subtableLength = SegmentHeaderBytes + (count * SegmentBytes);
        var table = new byte[CmapHeaderBytes + subtableLength];
        var span = table.AsSpan();
        BinaryPrimitives.WriteUInt16BigEndian(span[Int16Bytes..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(span[PlatformAt..], WindowsPlatform);
        BinaryPrimitives.WriteUInt16BigEndian(span[EncodingAt..], UnicodeBmpEncoding);
        BinaryPrimitives.WriteUInt32BigEndian(span[SubtableOffsetAt..], CmapHeaderBytes);
        var sub = span[CmapHeaderBytes..];
        BinaryPrimitives.WriteUInt16BigEndian(sub, SegmentFormat);
        BinaryPrimitives.WriteUInt16BigEndian(sub[SubtableLengthAt..], (ushort)Math.Min(subtableLength, ushort.MaxValue));
        BinaryPrimitives.WriteUInt16BigEndian(sub[SegCountAt..], (ushort)(count * Int16Bytes));
        WriteSearchFields(sub[SearchFieldsAt..], count);
        const int ends = EndCodesAt;
        var starts = ends + (count * Int16Bytes) + Int16Bytes;
        var deltas = starts + (count * Int16Bytes);
        for (var i = 0; i < count; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(sub[(ends + (i * Int16Bytes))..], segments[i].Code);
            BinaryPrimitives.WriteUInt16BigEndian(sub[(starts + (i * Int16Bytes))..], segments[i].Code);
            BinaryPrimitives.WriteInt16BigEndian(sub[(deltas + (i * Int16Bytes))..], segments[i].Delta);
        }

        return table;
    }

    /// <summary>Writes a segment map's binary search fields.</summary>
    /// <param name="fields">The three fields.</param>
    /// <param name="count">The segment count.</param>
    private static void WriteSearchFields(Span<byte> fields, int count)
    {
        var shift = 0;
        while (shift < SearchMaxShift && (1 << (shift + 1)) <= count)
        {
            shift++;
        }

        var searchRange = (1 << shift) * Int16Bytes;
        BinaryPrimitives.WriteUInt16BigEndian(fields, (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(fields[Int16Bytes..], (ushort)shift);
        BinaryPrimitives.WriteUInt16BigEndian(fields[Int32Bytes..], (ushort)((count * Int16Bytes) - searchRange));
    }

    /// <summary>Copies a table with one 16-bit value replaced.</summary>
    /// <param name="table">The table.</param>
    /// <param name="offset">The value's offset.</param>
    /// <param name="value">The value.</param>
    /// <returns>The copy.</returns>
    private static byte[] Patch(ReadOnlySpan<byte> table, int offset, ushort value)
    {
        var copy = table.ToArray();
        if (offset + Int16Bytes <= copy.Length)
        {
            BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(offset), value);
        }

        return copy;
    }

    /// <summary>Lays tables out after a directory, each aligned, with checksums and the header's adjustment.</summary>
    /// <param name="tables">The tables, in tag order.</param>
    /// <returns>The font file.</returns>
    private static byte[] Assemble(List<(uint Tag, byte[] Bytes)> tables)
    {
        var directoryBytes = SfntDirectory.HeaderBytes + (tables.Count * SfntDirectory.RecordBytes);
        var total = directoryBytes;
        foreach (var (_, bytes) in tables)
        {
            total += Align(bytes.Length);
        }

        var file = new byte[total];
        WriteOffsetTable(file, tables.Count);
        var at = directoryBytes;
        var headAt = -1;
        for (var i = 0; i < tables.Count; i++)
        {
            var (tag, bytes) = tables[i];
            var record = file.AsSpan(SfntDirectory.HeaderBytes + (i * SfntDirectory.RecordBytes));
            BinaryPrimitives.WriteUInt32BigEndian(record, tag);
            BinaryPrimitives.WriteUInt32BigEndian(record[RecordChecksumAt..], Checksum(bytes));
            BinaryPrimitives.WriteUInt32BigEndian(record[RecordOffsetAt..], (uint)at);
            BinaryPrimitives.WriteUInt32BigEndian(record[RecordLengthAt..], (uint)bytes.Length);
            bytes.CopyTo(file.AsSpan(at));
            headAt = tag == SfntTag.Head ? at : headAt;
            at += Align(bytes.Length);
        }

        if (headAt >= 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(headAt + HeadChecksum), unchecked(ChecksumMagic - Checksum(file)));
        }

        return file;
    }

    /// <summary>Writes the offset table: the version, table count and binary search fields.</summary>
    /// <param name="file">The file.</param>
    /// <param name="count">The table count.</param>
    private static void WriteOffsetTable(Span<byte> file, int count)
    {
        BinaryPrimitives.WriteUInt32BigEndian(file, TrueTypeVersion);
        BinaryPrimitives.WriteUInt16BigEndian(file[TableCountAt..], (ushort)count);
        var shift = 0;
        while ((1 << (shift + 1)) <= count)
        {
            shift++;
        }

        var searchRange = (1 << shift) * SfntDirectory.RecordBytes;
        BinaryPrimitives.WriteUInt16BigEndian(file[SearchRangeAt..], (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(file[EntrySelectorAt..], (ushort)shift);
        BinaryPrimitives.WriteUInt16BigEndian(file[RangeShiftAt..], (ushort)((count * SfntDirectory.RecordBytes) - searchRange));
    }

    /// <summary>Sums bytes as big-endian 32-bit words, padding the end with zeros.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The checksum.</returns>
    private static uint Checksum(ReadOnlySpan<byte> bytes)
    {
        uint sum = 0;
        var whole = bytes.Length / Int32Bytes * Int32Bytes;
        for (var i = 0; i < whole; i += Int32Bytes)
        {
            sum = unchecked(sum + BinaryPrimitives.ReadUInt32BigEndian(bytes[i..]));
        }

        uint last = 0;
        for (var i = whole; i < bytes.Length; i++)
        {
            last |= (uint)bytes[i] << (ByteBits * (Int32Bytes - 1 - (i - whole)));
        }

        return unchecked(sum + last);
    }

    /// <summary>Rounds a length up to the alignment.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The aligned length.</returns>
    private static int Align(int length) => (length + Alignment - 1) / Alignment * Alignment;
}
