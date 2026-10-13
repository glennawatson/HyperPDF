// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Builds the pieces of a subset CFF font (Adobe Technical Note #5176): charstrings, pruned subroutines, charset, FDSelect
/// and Private DICTs. A font that is not CID-keyed stays so, with glyph names kept, so the CID of a glyph is its glyph
/// number. A CID-keyed font stays CID-keyed.
/// </summary>
internal static class CffSubsetWriter
{
    /// <summary>The charstring operator that ends a glyph.</summary>
    private const byte EndChar = 14;

    /// <summary>The charstring operator that returns from a subroutine.</summary>
    private const byte Return = 11;

    /// <summary>The charset format that lists every glyph.</summary>
    private const byte ListFormat = 0;

    /// <summary>The charset format that holds ranges with a 16-bit count.</summary>
    private const byte LongRangeFormat = 2;

    /// <summary>The FDSelect format that holds ranges.</summary>
    private const byte RangeFormat = 3;

    /// <summary>The bytes of a charset range with a 16-bit count.</summary>
    private const int LongRangeBytes = 4;

    /// <summary>The bytes of a 16-bit number.</summary>
    private const int ShortBytes = 2;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Gets the registry name of a renumbered CID-keyed font.</summary>
    private static ReadOnlySpan<byte> AdobeName => "Adobe"u8;

    /// <summary>Gets the ordering name of a renumbered CID-keyed font.</summary>
    private static ReadOnlySpan<byte> IdentityName => "Identity"u8;

    /// <summary>Writes the subset.</summary>
    /// <param name="face">The font being cut down.</param>
    /// <param name="order">The glyph of the font that each output glyph is, glyph zero first.</param>
    /// <param name="body">Whether each output glyph keeps its outline; blank glyphs are only an <c>endchar</c>.</param>
    /// <param name="walker">What the kept charstrings call.</param>
    /// <param name="renumber">Whether CIDs are renumbered to the output glyph numbers rather than kept.</param>
    /// <param name="isCid">Whether the output is CID-keyed: always for a CID-keyed source, and for a name-keyed source when renumbering makes its glyph numbers the CIDs.</param>
    /// <param name="fontName">The name written to the Name INDEX.</param>
    /// <returns>The CFF font.</returns>
    internal static byte[] Write(CffFace face, int[] order, bool[] body, CffSubrWalker walker, bool renumber, bool isCid, string fontName)
    {
        var strings = new CffStringTable(face);
        var map = FontDictMap(face, order);
        var charset = isCid ? CidCharset(face, order, renumber) : NameCharset(face, order, strings);
        var ros = isCid ? Ros(face, renumber, strings) : [];
        var blocks = PrivateBlocks(face, walker, map);
        var sections = new CffSections(
            CffWriting.SingleIndex(Encoding.ASCII.GetBytes(fontName)),
            CffWriting.Index(strings.ToItems()),
            CffWriting.Index(Subrs(face.GlobalSubrs, face, walker, -1)),
            charset,
            isCid ? FdSelect(face, order, map) : [],
            CffWriting.Index(CharStrings(face, order, body)),
            blocks,
            map,
            renumber ? order.Length : MaxCid(face, order) + 1,
            ros,
            isCid);
        return CffLayout.Assemble(face, sections, InverseOf(map, blocks.Length));
    }

    /// <summary>Maps each output Font DICT to its source Font DICT.</summary>
    /// <param name="map">The output number of each source Font DICT.</param>
    /// <param name="count">The number of output Font DICTs.</param>
    /// <returns>The source Font DICT of each output Font DICT.</returns>
    private static int[] InverseOf(int[] map, int count)
    {
        var inverse = new int[count];
        for (var source = 0; source < map.Length; source++)
        {
            if (map[source] >= 0)
            {
                inverse[map[source]] = source;
            }
        }

        return inverse;
    }

    /// <summary>Maps the Font DICTs the output glyphs use to consecutive numbers.</summary>
    /// <param name="face">The font.</param>
    /// <param name="order">The glyphs.</param>
    /// <returns>For each source Font DICT, its output number or -1 when unused; a font that is not CID-keyed uses its one.</returns>
    private static int[] FontDictMap(CffFace face, int[] order)
    {
        var map = new int[face.Dicts.Length];
        map.AsSpan().Fill(-1);
        if (!face.IsCid)
        {
            map[0] = 0;
            return map;
        }

        foreach (var glyph in order)
        {
            map[face.DictOf(glyph)] = 0;
        }

        var next = 0;
        for (var i = 0; i < map.Length; i++)
        {
            if (map[i] != 0)
            {
                continue;
            }

            map[i] = next;
            next++;
        }

        return map;
    }

    /// <summary>Gets the largest CID of the output glyphs.</summary>
    /// <param name="face">The font.</param>
    /// <param name="order">The glyphs.</param>
    /// <returns>The largest CID, or zero for a font that is not CID-keyed.</returns>
    private static int MaxCid(CffFace face, int[] order)
    {
        var max = 0;
        if (!face.IsCid)
        {
            return max;
        }

        foreach (var glyph in order)
        {
            max = Math.Max(max, face.Charset[glyph]);
        }

        return max;
    }

    /// <summary>Gets the charstrings of the output glyphs.</summary>
    /// <param name="face">The font.</param>
    /// <param name="order">The glyphs.</param>
    /// <param name="body">Whether each keeps its outline.</param>
    /// <returns>The charstrings.</returns>
    private static byte[][] CharStrings(CffFace face, int[] order, bool[] body)
    {
        var items = new byte[order.Length][];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = body[i] ? face.CharStrings.Get(face.Data.Span, order[i]).ToArray() : [EndChar];
        }

        return items;
    }

    /// <summary>Gets a subroutine INDEX's objects with the ones nothing calls cut down to a bare <c>return</c>, so numbering is unchanged.</summary>
    /// <param name="index">The subroutines.</param>
    /// <param name="face">The font.</param>
    /// <param name="walker">What the kept charstrings call.</param>
    /// <param name="dict">The Font DICT, or -1 for the global subroutines.</param>
    /// <returns>The objects.</returns>
    private static byte[][] Subrs(CffIndex index, CffFace face, CffSubrWalker walker, int dict)
    {
        var items = new byte[index.Count][];
        for (var i = 0; i < items.Length; i++)
        {
            var used = dict < 0 ? walker.IsGlobalUsed(i) : walker.IsLocalUsed(dict, i);
            items[i] = used ? index.Get(face.Data.Span, i).ToArray() : [Return];
        }

        return items;
    }

    /// <summary>Builds the charset of a font that is not CID-keyed: the SID of each glyph after the first.</summary>
    /// <param name="face">The font.</param>
    /// <param name="order">The glyphs.</param>
    /// <param name="strings">The strings the glyph names use.</param>
    /// <returns>The charset, in format 0.</returns>
    private static byte[] NameCharset(CffFace face, int[] order, CffStringTable strings)
    {
        var charset = new byte[1 + ((order.Length - 1) * ShortBytes)];
        charset[0] = ListFormat;
        for (var i = 1; i < order.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(charset.AsSpan(1 + ((i - 1) * ShortBytes)), (ushort)strings.Sid(face.Charset[order[i]]));
        }

        return charset;
    }

    /// <summary>Builds the charset of a CID-keyed font.</summary>
    /// <param name="face">The font.</param>
    /// <param name="order">The glyphs.</param>
    /// <param name="renumber">Whether the CIDs become the glyph numbers, which one range holds.</param>
    /// <returns>The charset.</returns>
    private static byte[] CidCharset(CffFace face, int[] order, bool renumber)
    {
        if (renumber && order.Length > 1)
        {
            var range = new byte[1 + LongRangeBytes];
            range[0] = LongRangeFormat;
            BinaryPrimitives.WriteUInt16BigEndian(range.AsSpan(1), 1);
            BinaryPrimitives.WriteUInt16BigEndian(range.AsSpan(1 + ShortBytes), (ushort)(order.Length - ShortBytes));
            return range;
        }

        var charset = new byte[1 + ((order.Length - 1) * ShortBytes)];
        charset[0] = ListFormat;
        for (var i = 1; i < order.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(charset.AsSpan(1 + ((i - 1) * ShortBytes)), face.Charset[order[i]]);
        }

        return charset;
    }

    /// <summary>Builds the FDSelect table in range format.</summary>
    /// <param name="face">The font.</param>
    /// <param name="order">The glyphs.</param>
    /// <param name="map">The output number of each Font DICT.</param>
    /// <returns>The table.</returns>
    private static byte[] FdSelect(CffFace face, int[] order, int[] map)
    {
        var ranges = new List<byte>(order.Length);
        var current = -1;
        var count = 0;
        for (var i = 0; i < order.Length; i++)
        {
            var dict = map[face.DictOf(order[i])];
            if (dict == current)
            {
                continue;
            }

            current = dict;
            count++;
            ranges.Add((byte)(i >> ByteBits));
            ranges.Add((byte)i);
            ranges.Add((byte)dict);
        }

        var table = new byte[1 + ShortBytes + ranges.Count + ShortBytes];
        table[0] = RangeFormat;
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(1), (ushort)count);
        ranges.CopyTo(table, 1 + ShortBytes);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(table.Length - ShortBytes), (ushort)order.Length);
        return table;
    }

    /// <summary>Gets the registry, ordering and supplement the output declares.</summary>
    /// <param name="face">The font.</param>
    /// <param name="renumber">Whether the CIDs are renumbered, which makes the collection Adobe-Identity.</param>
    /// <param name="strings">The output strings, which receive the names.</param>
    /// <returns>The registry SID, ordering SID and supplement.</returns>
    private static int[] Ros(CffFace face, bool renumber, CffStringTable strings) => renumber
        ? [strings.Add(AdobeName.ToArray()), strings.Add(IdentityName.ToArray()), 0]
        : [strings.Sid(face.Ros[0]), strings.Sid(face.Ros[1]), face.Ros[2]];

    /// <summary>Builds each output Font DICT's Private DICT followed by its local subroutines.</summary>
    /// <param name="face">The font.</param>
    /// <param name="walker">What the kept charstrings call.</param>
    /// <param name="map">The output number of each Font DICT.</param>
    /// <returns>One block per output Font DICT.</returns>
    private static byte[][] PrivateBlocks(CffFace face, CffSubrWalker walker, int[] map)
    {
        var blocks = new List<byte[]>(face.Dicts.Length);
        for (var dict = 0; dict < face.Dicts.Length; dict++)
        {
            if (map[dict] < 0)
            {
                continue;
            }

            ref readonly var source = ref face.Dicts[dict];
            var subrs = source.Subrs.Count > 0 ? CffWriting.Index(Subrs(source.Subrs, face, walker, dict)) : [];
            blocks.Add([.. CffLayout.PrivateDict(in source), .. subrs]);
        }

        return [.. blocks];
    }
}
