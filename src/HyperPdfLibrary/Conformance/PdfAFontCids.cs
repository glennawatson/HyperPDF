// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The CIDs a composite font's program has, as veraPDF counts them for /CIDSet: a TrueType program has each nonzero CID
/// its /CIDToGIDMap maps below the glyph count, every long-metric glyph when the map is the identity; a CID-keyed CFF
/// program has the CIDs of its charset; other CFF programs have none.
/// </summary>
internal static class PdfAFontCids
{
    /// <summary>The CIDs a CFF charset can hold.</summary>
    private const int CidCount = ushort.MaxValue + 1;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Determines whether the program has a CID.</summary>
    /// <param name="font">The composite font.</param>
    /// <param name="cid">The CID.</param>
    /// <returns><see langword="true"/> when it has.</returns>
    internal static bool Contains(PdfAGlyphFont font, int cid)
    {
        if (font.Program.TrueType is { } tables)
        {
            var mapped = font.CidToGid is not { } map || (uint)cid < (uint)map.Length;
            return mapped && cid != 0 && PdfACidGlyphs.Glyph(font, cid) < tables.GlyphCount;
        }

        return font.Program.Cff is { IsCidKeyed: true } cff && cff.GetGlyphByCid(cid) > 0;
    }

    /// <summary>Determines whether /CIDSet sets exactly the program's nonzero CIDs.</summary>
    /// <param name="font">The composite font.</param>
    /// <param name="bits">The /CIDSet bytes.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool ListsAll(PdfAGlyphFont font, byte[] bits)
    {
        foreach (var cid in Cids(font))
        {
            if (cid != 0 && !PdfAFontSubsetRules.IsSet(bits, cid))
            {
                return false;
            }
        }

        for (var cid = 1; cid < bits.Length * ByteBits; cid++)
        {
            if (PdfAFontSubsetRules.IsSet(bits, cid) && !Contains(font, cid))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lists the program's CIDs.</summary>
    /// <param name="font">The composite font.</param>
    /// <returns>The CIDs.</returns>
    private static List<int> Cids(PdfAGlyphFont font)
    {
        var cids = new List<int>();
        if (font.Program.TrueType is { } tables)
        {
            var count = font.CidToGid?.Length ?? tables.Advances.Length;
            for (var cid = 0; cid < count; cid++)
            {
                if (font.CidToGid is null || Contains(font, cid))
                {
                    cids.Add(cid);
                }
            }
        }
        else if (font.Program.Cff is { IsCidKeyed: true } cff)
        {
            for (var cid = 0; cid < CidCount; cid++)
            {
                if (cff.GetGlyphByCid(cid) >= 0)
                {
                    cids.Add(cid);
                }
            }
        }

        return cids;
    }
}
