// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Derives the glyph facts of a composite font as veraPDF does. A code is .notdef when the program lacks its glyph.
/// TrueType programs map the CID through /CIDToGIDMap; CID-keyed CFF programs through their charset; other CFF programs
/// take a nonzero CID as the glyph id.
/// </summary>
internal static class PdfACidGlyphs
{
    /// <summary>Gets the facts of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns>The facts.</returns>
    internal static PdfAGlyphFacts Facts(PdfAGlyphFont font, int code)
    {
        if (!font.Program.IsEmbedded || font.Codes is not { } codes)
        {
            return new(null, null, null, null);
        }

        var cid = codes.ToCid(code);
        var present = Contains(font, codes, code, cid);
        var width = Width(font, code, cid);
        var widths = font.CidWidths;
        return new(
            present ? null : PdfAEncodingTables.Notdef,
            code == 0 || present,
            width < 0 ? widths?.Default ?? 0 : width,
            widths?.Width(cid) ?? 0);
    }

    /// <summary>Gets the glyph id of a CID for a TrueType program.</summary>
    /// <param name="font">The font.</param>
    /// <param name="cid">The CID.</param>
    /// <returns>The glyph id, or 0 past the end of a map stream.</returns>
    internal static int Glyph(PdfAGlyphFont font, int cid)
    {
        if (font.CidToGid is not { } map)
        {
            return cid;
        }

        return (uint)cid < (uint)map.Length ? map[cid] : 0;
    }

    /// <summary>Determines whether the program has the glyph of a code.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The code reader.</param>
    /// <param name="code">The code.</param>
    /// <param name="cid">The code's CID.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    private static bool Contains(PdfAGlyphFont font, PdfACidCodes codes, int code, int cid)
    {
        if (font.Program.TrueType is { } tables)
        {
            var mapped = font.CidToGid is not { } map || (uint)cid < (uint)map.Length;
            return codes.ContainsCode(code) && mapped && cid != 0 && Glyph(font, cid) < tables.GlyphCount;
        }

        if (font.Program.Cff is not { } cff)
        {
            return false;
        }

        if (!cff.IsCidKeyed)
        {
            return cid != 0 ? cid < cff.GlyphCount : cff.GetGlyphByCharCode(code) >= 0;
        }

        return codes.ContainsCode(code) && cff.GetGlyphByCid(cid) > 0;
    }

    /// <summary>Gets the program's width for a code, or -1.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <param name="cid">The code's CID.</param>
    /// <returns>The width.</returns>
    private static double Width(PdfAGlyphFont font, int code, int cid)
    {
        if (font.Program.TrueType is { } tables)
        {
            return tables.Width(Glyph(font, cid));
        }

        if (font.Program.Cff is not { } cff)
        {
            return -1;
        }

        if (!cff.IsCidKeyed)
        {
            var glyph = cid != 0 ? cid : cff.GetGlyphByCharCode(code);
            return glyph >= 0 && glyph < cff.GlyphCount ? PdfAType1Glyphs.CffWidth(cff, glyph) : PdfAType1Glyphs.CffWidth(cff, 0);
        }

        var cidGlyph = cff.GetGlyphByCid(cid);
        return cidGlyph < 0 ? -1 : PdfAType1Glyphs.CffWidth(cff, cidGlyph);
    }
}
