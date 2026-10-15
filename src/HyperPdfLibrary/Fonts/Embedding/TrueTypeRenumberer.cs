// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
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
    /// <param name="state">The document's reusable embedding buffers.</param>
    /// <returns>The new font, or <see langword="null"/> when the outlines cannot be read.</returns>
    internal static SubsetProgram? Subset(SfntFont font, FontFacts facts, ReadOnlySpan<int> shown, PdfFontEmbeddingState state)
    {
        var glyf = font.Table(SfntTags.Glyf);
        var head = font.Table(SfntTags.Head);
        var longLoca = head.Length >= HeadSize && BinaryPrimitives.ReadInt16BigEndian(head[HeadLocaFormat..]) == 1;
        var count = Math.Max(facts.GlyphCount, 0);
        if (count == 0)
        {
            return null;
        }

        var offsets = ArrayPool<int>.Shared.Rent(count + 1);
        var order = ScratchList<int>.Rent(ref state.GlyphOrder, shown.Length + 1);
        try
        {
            var used = offsets.AsSpan(0, count + 1);
            if (!TrueTypeLocations.TryRead(font.Table(SfntTags.Loca), glyf.Length, count, longLoca, used))
            {
                return null;
            }

            Order(glyf, used, shown, order);
            var data = Rebuild(font, facts, glyf, used, order, state);
            return new(data, [.. order], false);
        }
        finally
        {
            ScratchList<int>.Return(ref state.GlyphOrder, order);
            ArrayPool<int>.Shared.Return(offsets);
        }
    }

    /// <summary>Lists the glyphs to keep: glyph 0, the shown glyphs, then the parts of composite glyphs.</summary>
    /// <param name="glyf">The /glyf table.</param>
    /// <param name="offsets">The glyph offsets into /glyf.</param>
    /// <param name="shown">The shown glyphs.</param>
    /// <param name="order">Receives the original glyph of each new glyph.</param>
    private static void Order(ReadOnlySpan<byte> glyf, ReadOnlySpan<int> offsets, ReadOnlySpan<int> shown, List<int> order)
    {
        var count = offsets.Length - 1;
        var seenBuffer = ArrayPool<bool>.Shared.Rent(count);
        try
        {
            var seen = seenBuffer.AsSpan(0, count);
            seen.Clear();
            order.Add(0);
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
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(seenBuffer);
        }
    }

    /// <summary>Adds a glyph to the list unless it is outside the font or already there.</summary>
    /// <param name="glyph">The glyph.</param>
    /// <param name="seen">Which glyphs are in the list.</param>
    /// <param name="order">The list.</param>
    private static void Add(int glyph, Span<bool> seen, List<int> order)
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
    /// <param name="state">The document's reusable embedding buffers.</param>
    /// <returns>The font file.</returns>
    private static byte[] Rebuild(SfntFont font, FontFacts facts, ReadOnlySpan<byte> glyf, ReadOnlySpan<int> offsets, List<int> order, PdfFontEmbeddingState state)
    {
        var mapBuffer = ArrayPool<int>.Shared.Rent(offsets.Length - 1);
        try
        {
            var map = mapBuffer.AsSpan(0, offsets.Length - 1);
            map.Clear();
            for (var i = 0; i < order.Count; i++)
            {
                map[order[i]] = i;
            }

            var newGlyf = WriteGlyphs(glyf, offsets, order, map, out var loca);
            return WriteTables(font, facts, order, newGlyf, loca, state);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(mapBuffer);
        }
    }

    /// <summary>Writes the tables of the new font using borrowed entry storage.</summary>
    /// <param name="font">The face.</param>
    /// <param name="facts">The face's facts.</param>
    /// <param name="order">The original glyph of each new glyph.</param>
    /// <param name="newGlyf">The new /glyf.</param>
    /// <param name="loca">The new /loca.</param>
    /// <param name="state">The document's reusable embedding buffers.</param>
    /// <returns>The owned font bytes.</returns>
    private static byte[] WriteTables(SfntFont font, FontFacts facts, List<int> order, byte[] newGlyf, byte[] loca, PdfFontEmbeddingState state)
    {
        var entries = ScratchList<SfntEntry>.Rent(ref state.FontTables, Tables.Length + RebuiltTables);
        try
        {
            entries.Add(new(SfntTags.Glyf, newGlyf));
            entries.Add(new(SfntTags.Loca, loca));
            entries.Add(new(SfntTags.Head, HeadTable(font)));
            entries.Add(new(SfntTags.Hhea, Header(font, SfntTags.Hhea, HheaMetricCount, order.Count, HheaSize)));
            entries.Add(new(SfntTags.Maxp, Header(font, SfntTags.Maxp, MaxpGlyphs, order.Count, 0)));
            entries.Add(new(SfntTags.Hmtx, Metrics(font, facts, order)));
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

            return font.Write(CollectionsMarshal.AsSpan(entries), CancellationToken.None);
        }
        finally
        {
            ScratchList<SfntEntry>.Return(ref state.FontTables, entries);
        }
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
    private static byte[] WriteGlyphs(ReadOnlySpan<byte> glyf, ReadOnlySpan<int> offsets, List<int> order, ReadOnlySpan<int> map, out byte[] loca)
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
    private static void WriteGlyph(ref PooledBuffer data, ReadOnlySpan<byte> glyph, ReadOnlySpan<int> map)
    {
        var walker = new CompositeWalker(glyph);
        if (!walker.MoveNext())
        {
            data.Write(glyph);
            return;
        }

        var copy = data.GetSpan(glyph.Length)[..glyph.Length];
        glyph.CopyTo(copy);
        do
        {
            BinaryPrimitives.WriteUInt16BigEndian(copy[walker.GlyphOffset..], (ushort)((uint)walker.Glyph < (uint)map.Length ? map[walker.Glyph] : 0));
        }
        while (walker.MoveNext());
        data.Advance(copy.Length);
    }
}
