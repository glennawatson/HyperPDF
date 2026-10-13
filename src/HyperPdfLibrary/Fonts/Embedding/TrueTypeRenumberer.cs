// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Cuts a TrueType face down to the glyphs a document shows and numbers them afresh from zero, in the order given, so
/// the embedded CIDs are dense. Parts of composite glyphs follow the shown glyphs. The font keeps only the tables a PDF
/// reader draws from: font header, metrics, outlines, hinting programs and a version 3 'post' table.
/// </summary>
internal static class TrueTypeRenumberer
{
    /// <summary>The offset of the location format in /head.</summary>
    private const int HeadLocaFormat = 50;

    /// <summary>The offset of the glyph count in /maxp.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The offset of the metric count in /hhea.</summary>
    private const int HheaMetricCount = 34;

    /// <summary>The smallest /hhea table.</summary>
    private const int HheaSize = 36;

    /// <summary>The smallest /head table.</summary>
    private const int HeadSize = 54;

    /// <summary>The bytes of a version 3 'post' table, which has no glyph names.</summary>
    private const int PostBytes = 32;

    /// <summary>The 'post' version without glyph names.</summary>
    private const uint PostVersion3 = 0x00030000;

    /// <summary>The alignment of every glyph.</summary>
    private const int Alignment = 4;

    /// <summary>The number of tables written besides the hinting tables: head, hhea, maxp, hmtx, loca, glyf and post.</summary>
    private const int RebuiltTables = 7;

    /// <summary>The bytes of a glyph's metrics in the new /hmtx.</summary>
    private const int MetricBytes = 4;

    /// <summary>Gets the zero bytes that pad a glyph to the table alignment.</summary>
    private static ReadOnlySpan<byte> Padding => [0x00, 0x00, 0x00, 0x00];

    /// <summary>Gets the hinting tables copied unchanged.</summary>
    private static ReadOnlySpan<uint> Tables => [SfntTags.Cvt, SfntTags.Fpgm, SfntTags.Prep];

    /// <summary>Cuts a face down to some glyphs.</summary>
    /// <param name="font">The face; it must have TrueType outlines.</param>
    /// <param name="facts">The face's facts.</param>
    /// <param name="shown">The original glyphs to keep, which become glyphs 1 upward in this order; glyph 0 is always first.</param>
    /// <returns>The new font, or <see langword="null"/> when the outlines cannot be read.</returns>
    internal static SubsetProgram? Subset(SfntFont font, FontFacts facts, ReadOnlySpan<int> shown)
    {
        var glyf = font.Table(SfntTags.Glyf);
        var head = font.Table(SfntTags.Head);
        var longLoca = head.Length >= HeadSize && BinaryPrimitives.ReadInt16BigEndian(head[HeadLocaFormat..]) == 1;
        if (!TrueTypeLocations.TryRead(font.Table(SfntTags.Loca), glyf.Length, facts.GlyphCount, longLoca, out var offsets))
        {
            return null;
        }

        var order = Order(glyf, offsets, shown);
        var data = Rebuild(font, facts, glyf, offsets, order);
        return new(data, [.. order], false);
    }

    /// <summary>Lists the glyphs to keep: glyph 0, the shown glyphs, then the parts of composite glyphs.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="shown">The shown glyphs.</param>
    /// <returns>The original glyph of each new glyph.</returns>
    private static List<int> Order(ReadOnlySpan<byte> glyf, int[] offsets, ReadOnlySpan<int> shown)
    {
        var count = offsets.Length - 1;
        var order = new List<int>(shown.Length + 1) { 0 };
        var seen = new bool[count];
        seen[0] = true;
        foreach (var glyph in shown)
        {
            Add(glyph, seen, order);
        }

        // The list grows while it is walked: each composite glyph's parts are appended once.
        for (var i = 0; i < order.Count; i++)
        {
            var walker = new CompositeWalker(glyf[offsets[order[i]]..offsets[order[i] + 1]]);
            while (walker.MoveNext())
            {
                Add(walker.Glyph, seen, order);
            }
        }

        return order;
    }

    /// <summary>Adds a glyph to the list unless it is outside the font or already there.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="seen">Which glyphs are in the list.</param>
    /// <param name="order">The list.</param>
    private static void Add(int glyph, bool[] seen, List<int> order)
    {
        if ((uint)glyph >= (uint)seen.Length || seen[glyph])
        {
            return;
        }

        seen[glyph] = true;
        order.Add(glyph);
    }

    /// <summary>Writes the new font.</summary>
    /// <param name="font">The face.</param>
    /// <param name="facts">The face's facts.</param>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="order">The original glyph of each new glyph.</param>
    /// <returns>The font file.</returns>
    private static byte[] Rebuild(SfntFont font, FontFacts facts, ReadOnlySpan<byte> glyf, int[] offsets, List<int> order)
    {
        var map = new int[offsets.Length - 1];
        for (var i = 0; i < order.Count; i++)
        {
            map[order[i]] = i;
        }

        var newGlyf = WriteGlyphs(glyf, offsets, order, map, out var loca);
        var entries = new List<SfntEntry>(Tables.Length + RebuiltTables)
        {
            new(SfntTags.Glyf, newGlyf),
            new(SfntTags.Loca, loca),
            new(SfntTags.Head, HeadTable(font)),
            new(SfntTags.Hhea, Header(font, SfntTags.Hhea, HheaMetricCount, order.Count, HheaSize)),
            new(SfntTags.Maxp, Header(font, SfntTags.Maxp, MaxpGlyphs, order.Count, 0)),
            new(SfntTags.Hmtx, Metrics(font, facts, order)),
        };
        foreach (var tag in Tables)
        {
            if (font.TableMemory(tag) is { IsEmpty: false } table)
            {
                entries.Add(new(tag, table));
            }
        }

        if (Post(font) is { } post)
        {
            entries.Add(new(SfntTags.Post, post));
        }

        return font.Write([.. entries]);
    }

    /// <summary>Copies the font header with long glyph locations and a cleared checksum adjustment, which the writer sets last.</summary>
    /// <param name="font">The face.</param>
    /// <returns>The new /head.</returns>
    private static byte[] HeadTable(SfntFont font)
    {
        var table = Header(font, SfntTags.Head, HeadLocaFormat, 1, HeadSize);
        table.AsSpan(SfntTags.HeadChecksumAdjustment, sizeof(uint)).Clear();
        return table;
    }

    /// <summary>Copies a header table and sets one 16-bit field in it.</summary>
    /// <param name="font">The face.</param>
    /// <param name="tag">The table.</param>
    /// <param name="offset">The offset of the field.</param>
    /// <param name="value">The value to write.</param>
    /// <param name="minimum">The smallest table size to accept, or zero.</param>
    /// <returns>The new table.</returns>
    private static byte[] Header(SfntFont font, uint tag, int offset, int value, int minimum)
    {
        var table = font.Table(tag).ToArray();
        if (table.Length >= Math.Max(offset + FontBytes.U16Size, minimum))
        {
            BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(offset), (ushort)value);
        }

        return table;
    }

    /// <summary>Builds the horizontal metrics of the new glyphs.</summary>
    /// <param name="font">The face.</param>
    /// <param name="facts">The face's facts.</param>
    /// <param name="order">The original glyph of each new glyph.</param>
    /// <returns>The new /hmtx.</returns>
    private static byte[] Metrics(SfntFont font, FontFacts facts, List<int> order)
    {
        var hmtx = font.Table(SfntTags.Hmtx);
        var metrics = new byte[order.Count * MetricBytes];
        for (var i = 0; i < order.Count; i++)
        {
            var record = metrics.AsSpan(i * MetricBytes);
            BinaryPrimitives.WriteUInt16BigEndian(record, (ushort)SfntMetrics.Advance(hmtx, facts.MetricCount, order[i]));
            BinaryPrimitives.WriteInt16BigEndian(record[FontBytes.U16Size..], (short)SfntMetrics.Bearing(hmtx, facts.MetricCount, order[i]));
        }

        return metrics;
    }

    /// <summary>Makes a version 3 'post' table from the face's, keeping the italic angle, underline and pitch.</summary>
    /// <param name="font">The face.</param>
    /// <returns>The table, or <see langword="null"/> when the face has none.</returns>
    private static byte[]? Post(SfntFont font)
    {
        var post = font.Table(SfntTags.Post);
        if (post.Length < PostBytes)
        {
            return null;
        }

        var table = post[..PostBytes].ToArray();
        BinaryPrimitives.WriteUInt32BigEndian(table, PostVersion3);
        return table;
    }

    /// <summary>Copies the kept glyphs into a new /glyf, each padded to four bytes, with composite glyph parts renumbered.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="order">The original glyph of each new glyph.</param>
    /// <param name="map">The new number of each original glyph.</param>
    /// <param name="loca">Receives the long /loca table.</param>
    /// <returns>The new /glyf.</returns>
    private static byte[] WriteGlyphs(ReadOnlySpan<byte> glyf, int[] offsets, List<int> order, int[] map, out byte[] loca)
    {
        loca = new byte[(order.Count + 1) * TrueTypeLocations.LongEntry];
        var data = default(PooledBuffer);
        try
        {
            for (var i = 0; i < order.Count; i++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(i * TrueTypeLocations.LongEntry), (uint)data.Length);
                var glyph = glyf[offsets[order[i]]..offsets[order[i] + 1]];
                if (glyph.IsEmpty)
                {
                    continue;
                }

                WriteGlyph(ref data, glyph, map);
                data.Write(Padding[..((Alignment - (data.Length % Alignment)) % Alignment)]);
            }

            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(order.Count * TrueTypeLocations.LongEntry), (uint)data.Length);
            return data.ToArray();
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>Writes one glyph, renumbering the parts of a composite glyph.</summary>
    /// <param name="data">The new /glyf.</param>
    /// <param name="glyph">The original glyph's data.</param>
    /// <param name="map">The new number of each original glyph.</param>
    private static void WriteGlyph(ref PooledBuffer data, ReadOnlySpan<byte> glyph, int[] map)
    {
        var walker = new CompositeWalker(glyph);
        if (!walker.MoveNext())
        {
            data.Write(glyph);
            return;
        }

        var copy = glyph.ToArray();
        do
        {
            BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(walker.GlyphOffset), (ushort)((uint)walker.Glyph < (uint)map.Length ? map[walker.Glyph] : 0));
        }
        while (walker.MoveNext());
        data.Write(copy);
    }
}
