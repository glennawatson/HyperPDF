// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>Reads CFF charsets, encodings and FDSelect tables into lookup arrays, once per font.</summary>
internal static class CffCharsets
{
    /// <summary>The predefined ISOAdobe charset.</summary>
    private const int IsoAdobeCharset = 0;

    /// <summary>The predefined Expert charset.</summary>
    private const int ExpertCharset = 1;

    /// <summary>The predefined Expert Subset charset.</summary>
    private const int ExpertSubsetCharset = 2;

    /// <summary>The last SID of the ISOAdobe charset.</summary>
    private const int IsoAdobeLastSid = 228;

    /// <summary>The charset format with one SID per glyph.</summary>
    private const int ListFormat = 0;

    /// <summary>The charset format with 8-bit range counts.</summary>
    private const int ShortRangeFormat = 1;

    /// <summary>The charset format with 16-bit range counts.</summary>
    private const int LongRangeFormat = 2;

    /// <summary>The predefined Standard encoding.</summary>
    private const int StandardEncodingId = 0;

    /// <summary>The predefined Expert encoding.</summary>
    private const int ExpertEncodingId = 1;

    /// <summary>The encoding format bit that adds supplements.</summary>
    private const int SupplementBit = 0x80;

    /// <summary>The mask of the encoding format.</summary>
    private const int FormatMask = 0x7F;

    /// <summary>The size of one supplement.</summary>
    private const int SupplementSize = 3;

    /// <summary>The size of a format 1 encoding range.</summary>
    private const int EncodingRangeSize = 2;

    /// <summary>The FDSelect format with one byte per glyph.</summary>
    private const int FdListFormat = 0;

    /// <summary>The FDSelect format with ranges.</summary>
    private const int FdRangeFormat = 3;

    /// <summary>The size of one FDSelect range.</summary>
    private const int FdRangeSize = 3;

    /// <summary>The size of an 8-bit-count charset range.</summary>
    private const int ShortRangeSize = 3;

    /// <summary>The size of a 16-bit-count charset range.</summary>
    private const int LongRangeSize = 4;

    /// <summary>Reads a charset.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="offset">The charset offset, or a predefined charset id.</param>
    /// <param name="glyphCount">The number of glyphs.</param>
    /// <param name="isCid">Whether the font is CID-keyed, which has no predefined charsets.</param>
    /// <returns>The SID or CID of each glyph.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static ushort[] ReadCharset(ReadOnlySpan<byte> data, int offset, int glyphCount, bool isCid)
    {
        ValidateCharsetOffset(data, offset, isCid);
        var charset = new ushort[glyphCount];
        if (!isCid && offset <= ExpertSubsetCharset)
        {
            FillPredefined(charset, offset);
            return charset;
        }

        var format = FontBytes.U8(data, offset);
        var position = offset + 1;
        if (format == ListFormat)
        {
            for (var glyph = 1; glyph < glyphCount; glyph++)
            {
                charset[glyph] = (ushort)FontBytes.U16(data, position);
                position += FontBytes.U16Size;
            }

            return charset;
        }

        if (format is ShortRangeFormat or LongRangeFormat)
        {
            FillRanges(data, position, charset, format == LongRangeFormat);
        }

        return charset;
    }

    /// <summary>Builds the CID-to-glyph table of a CID-keyed font.</summary>
    /// <param name="charset">The CID of each glyph.</param>
    /// <returns>The glyph of each CID, zero when unmapped.</returns>
    internal static ushort[] InvertCharset(ushort[] charset)
    {
        var max = 0;
        foreach (var cid in charset)
        {
            max = Math.Max(max, cid);
        }

        var cidToGlyph = new ushort[max + 1];
        for (var glyph = charset.Length - 1; glyph > 0; glyph--)
        {
            cidToGlyph[charset[glyph]] = (ushort)glyph;
        }

        return cidToGlyph;
    }

    /// <summary>Reads an FDSelect table.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="offset">The table offset, or -1.</param>
    /// <param name="glyphCount">The number of glyphs.</param>
    /// <param name="fontDictCount">The number of Font DICTs; larger indexes are replaced by zero.</param>
    /// <returns>The Font DICT of each glyph.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static byte[] ReadFdSelect(ReadOnlySpan<byte> data, int offset, int glyphCount, int fontDictCount)
    {
        if (offset < -1 || (offset > 0 && offset >= data.Length))
        {
            throw new PdfException(PdfError.Format, "The CFF font dictionary selection offset exceeds its data.");
        }

        var select = new byte[glyphCount];
        var format = offset > 0 ? FontBytes.U8(data, offset) : -1;
        if (format == FdListFormat)
        {
            FontBytes.Slice(data, offset + 1, glyphCount).CopyTo(select);
        }
        else if (format == FdRangeFormat)
        {
            FillFdRanges(data, offset + 1, select);
        }

        for (var i = 0; i < select.Length; i++)
        {
            select[i] = select[i] < fontDictCount ? select[i] : (byte)0;
        }

        return select;
    }

    /// <summary>Reads the built-in encoding of a font that is not CID-keyed.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="offset">The encoding offset, or a predefined encoding id.</param>
    /// <param name="program">The program, used to name glyphs.</param>
    /// <returns>The glyph of each code, -1 when unmapped.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    internal static short[] ReadEncoding(ReadOnlySpan<byte> data, int offset, CffProgram program)
    {
        if (offset < 0 || (offset > ExpertEncodingId && offset >= data.Length))
        {
            throw new PdfException(PdfError.Format, "The CFF encoding offset exceeds its data.");
        }

        var encoding = new short[FontEncodings.CodeCount];
        encoding.AsSpan().Fill(FontProgram.NotFound);
        if (offset is StandardEncodingId or ExpertEncodingId)
        {
            FillPredefinedEncoding(encoding, offset == StandardEncodingId ? FontEncoding.Standard : FontEncoding.Expert, program);
            return encoding;
        }

        var format = FontBytes.U8(data, offset);
        var position = (format & FormatMask) == 0
            ? FillCodeList(data, offset + 1, encoding, program.GlyphCount)
            : FillCodeRanges(data, offset + 1, encoding, program.GlyphCount);
        if ((format & SupplementBit) != 0)
        {
            FillSupplements(data, position, encoding, program);
        }

        return encoding;
    }

    /// <summary>Bounds a charset offset without confusing predefined charset IDs with file offsets.</summary>
    /// <param name="data">The CFF bytes.</param>
    /// <param name="offset">The charset offset or predefined ID.</param>
    /// <param name="isCid">Whether the font is CID-keyed.</param>
    /// <exception cref="PdfException">The offset is negative or exceeds the CFF bytes.</exception>
    private static void ValidateCharsetOffset(ReadOnlySpan<byte> data, int offset, bool isCid)
    {
        if (offset < 0 || ((offset > ExpertSubsetCharset || isCid) && offset >= data.Length))
        {
            throw new PdfException(PdfError.Format, "The CFF charset offset exceeds its data.");
        }
    }

    /// <summary>Fills a predefined charset.</summary>
    /// <param name="charset">The charset to fill.</param>
    /// <param name="id">The predefined charset id.</param>
    private static void FillPredefined(Span<ushort> charset, int id)
    {
        var source = id switch
        {
            ExpertCharset => CffStandardTables.ExpertCharset,
            ExpertSubsetCharset => CffStandardTables.ExpertSubsetCharset,
            _ => [],
        };

        for (var glyph = 0; glyph < charset.Length; glyph++)
        {
            if (id == IsoAdobeCharset)
            {
                charset[glyph] = glyph <= IsoAdobeLastSid ? (ushort)glyph : (ushort)0;
            }
            else
            {
                charset[glyph] = glyph < source.Length ? source[glyph] : (ushort)0;
            }
        }
    }

    /// <summary>Fills a charset from format 1 or 2 ranges.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="position">The offset of the first range.</param>
    /// <param name="charset">The charset to fill.</param>
    /// <param name="longCounts">Whether range counts are 16-bit.</param>
    private static void FillRanges(ReadOnlySpan<byte> data, int position, Span<ushort> charset, bool longCounts)
    {
        var glyph = 1;
        while (glyph < charset.Length && position < data.Length)
        {
            var first = FontBytes.U16(data, position);
            var left = longCounts ? FontBytes.U16(data, position + FontBytes.U16Size) : FontBytes.U8(data, position + FontBytes.U16Size);
            position += longCounts ? LongRangeSize : ShortRangeSize;
            for (var i = 0; i <= left && glyph < charset.Length; i++)
            {
                charset[glyph] = (ushort)(first + i);
                glyph++;
            }
        }
    }

    /// <summary>Fills an FDSelect table from format 3 ranges.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="position">The offset of the range count.</param>
    /// <param name="select">The table to fill.</param>
    private static void FillFdRanges(ReadOnlySpan<byte> data, int position, Span<byte> select)
    {
        var count = FontBytes.U16(data, position);
        position += FontBytes.U16Size;
        for (var range = 0; range < count; range++)
        {
            var first = FontBytes.U16(data, position);
            var fd = (byte)FontBytes.U8(data, position + FontBytes.U16Size);
            var next = FontBytes.U16(data, position + FdRangeSize);
            position += FdRangeSize;
            var end = Math.Min(next, select.Length);
            if (first < end)
            {
                select[first..end].Fill(fd);
            }
        }
    }

    /// <summary>Maps codes to glyphs through a predefined encoding's glyph names.</summary>
    /// <param name="encoding">The table to fill.</param>
    /// <param name="kind">The predefined encoding.</param>
    /// <param name="program">The program, used to name glyphs.</param>
    private static void FillPredefinedEncoding(Span<short> encoding, FontEncoding kind, CffProgram program)
    {
        for (var glyph = 1; glyph < program.GlyphCount; glyph++)
        {
            var code = FontEncodings.GetCode(kind, program.GetGlyphName(glyph));
            if (code >= 0 && encoding[code] < 0)
            {
                encoding[code] = (short)glyph;
            }
        }
    }

    /// <summary>Fills an encoding from a format 0 code list.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="position">The offset of the code count.</param>
    /// <param name="encoding">The table to fill.</param>
    /// <param name="glyphCount">The number of glyphs.</param>
    /// <returns>The offset after the list.</returns>
    private static int FillCodeList(ReadOnlySpan<byte> data, int position, Span<short> encoding, int glyphCount)
    {
        var count = FontBytes.U8(data, position);
        for (var i = 0; i < count && i + 1 < glyphCount; i++)
        {
            encoding[FontBytes.U8(data, position + 1 + i)] = (short)(i + 1);
        }

        return position + 1 + count;
    }

    /// <summary>Fills an encoding from format 1 ranges.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="position">The offset of the range count.</param>
    /// <param name="encoding">The table to fill.</param>
    /// <param name="glyphCount">The number of glyphs.</param>
    /// <returns>The offset after the ranges.</returns>
    private static int FillCodeRanges(ReadOnlySpan<byte> data, int position, Span<short> encoding, int glyphCount)
    {
        var count = FontBytes.U8(data, position);
        var glyph = 1;
        for (var range = 0; range < count; range++)
        {
            var at = position + 1 + (range * EncodingRangeSize);
            var first = FontBytes.U8(data, at);
            var left = FontBytes.U8(data, at + 1);
            for (var code = first; code <= first + left && code < encoding.Length && glyph < glyphCount; code++)
            {
                encoding[code] = (short)glyph;
                glyph++;
            }
        }

        return position + 1 + (count * EncodingRangeSize);
    }

    /// <summary>Applies encoding supplements, which map extra codes to glyphs by SID.</summary>
    /// <param name="data">The CFF data.</param>
    /// <param name="position">The offset of the supplement count.</param>
    /// <param name="encoding">The table to fill.</param>
    /// <param name="program">The program, used to find glyphs by SID.</param>
    private static void FillSupplements(ReadOnlySpan<byte> data, int position, Span<short> encoding, CffProgram program)
    {
        var count = FontBytes.U8(data, position);
        for (var i = 0; i < count; i++)
        {
            var at = position + 1 + (i * SupplementSize);
            var code = FontBytes.U8(data, at);
            var glyph = program.GetGlyphBySid(FontBytes.U16(data, at + 1));
            if (glyph > 0)
            {
                encoding[code] = (short)glyph;
            }
        }
    }
}
