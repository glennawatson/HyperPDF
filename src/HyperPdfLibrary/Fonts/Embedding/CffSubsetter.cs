// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Cuts a CFF font down to the glyphs a document shows: the charstrings of the glyphs, the subroutines they call, the
/// charset, and the FDArray and FDSelect of a CID-keyed font (Adobe Technical Note #5176). The result is valid CFF that
/// <see cref="CffProgram"/> reads. A glyph built from two others by the accented-character form of <c>endchar</c> brings
/// those two along.
/// </summary>
internal static class CffSubsetter
{
    /// <summary>Cuts a font down to some glyphs and numbers them from zero in the order given, so a glyph's CID is its place.</summary>
    /// <param name="cff">The CFF data (a bare CFF font or an OpenType font's 'CFF ' table).</param>
    /// <param name="shown">The glyphs to keep besides glyph zero, which is always kept; unknown glyphs are ignored.</param>
    /// <param name="fontName">The name written to the Name INDEX.</param>
    /// <returns>The subset and the glyph of the font that each of its glyphs is; <see langword="null"/> when the font cannot be read.</returns>
    internal static SubsetProgram? Renumber(ReadOnlyMemory<byte> cff, ReadOnlySpan<int> shown, string fontName)
    {
        if (!CffFaceReader.TryRead(cff, out var face))
        {
            return null;
        }

        var inSet = new bool[face.GlyphCount];
        var order = new List<int>(shown.Length + 1) { 0 };
        inSet[0] = true;
        foreach (var glyph in shown)
        {
            Add(glyph, order, inSet);
        }

        var walker = Follow(face, cff, order, inSet);
        var glyphs = order.ToArray();
        var body = new bool[glyphs.Length];
        body.AsSpan().Fill(true);

        // A name-keyed font is written CID-keyed so veraPDF and other readers find its CIDs; an accented glyph finds its parts by glyph name, so a font that has one stays name-keyed.
        var cidKeyed = face.IsCid || !walker.AnyAccent;
        return new(CffSubsetWriter.Write(face, glyphs, body, walker, true, cidKeyed, fontName), glyphs, cidKeyed);
    }

    /// <summary>Cuts the outlines of unused glyphs out of a font while every glyph keeps its number and CID, so widths, maps and content stay valid.</summary>
    /// <param name="cff">The CFF data.</param>
    /// <param name="keep">The glyphs to keep besides glyph zero; unknown glyphs are ignored.</param>
    /// <param name="fontName">The name written to the Name INDEX.</param>
    /// <returns>The subset; <see langword="null"/> when the font cannot be read.</returns>
    internal static byte[]? KeepNumbers(ReadOnlyMemory<byte> cff, IEnumerable<int> keep, string fontName)
    {
        if (!CffFaceReader.TryRead(cff, out var face))
        {
            return null;
        }

        var kept = new bool[face.GlyphCount];
        kept[0] = true;
        var pending = new List<int> { 0 };
        foreach (var glyph in keep)
        {
            Add(glyph, pending, kept);
        }

        var walker = Follow(face, cff, pending, kept);
        var order = new int[face.GlyphCount];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        return CffSubsetWriter.Write(face, order, kept, walker, false, face.IsCid, fontName);
    }

    /// <summary>Follows the charstrings of the glyphs kept, adding the glyphs that accented-character forms need.</summary>
    /// <param name="face">The font.</param>
    /// <param name="cff">The CFF data.</param>
    /// <param name="pending">The glyphs to follow; glyphs found on the way are added.</param>
    /// <param name="inSet">Which glyphs are in the set.</param>
    /// <returns>The walker that saw the calls.</returns>
    private static CffSubrWalker Follow(CffFace face, ReadOnlyMemory<byte> cff, List<int> pending, bool[] inSet)
    {
        var walker = new CffSubrWalker(face);
        CffProgram? program = null;
        for (var i = 0; i < pending.Count; i++)
        {
            walker.Walk(pending[i]);
            if (!walker.HasAccent || face.IsCid)
            {
                continue;
            }

            if (program is null && !CffProgram.TryParse(cff, out program))
            {
                continue;
            }

            Add(program.GetStandardGlyph(walker.BaseCode), pending, inSet);
            Add(program.GetStandardGlyph(walker.AccentCode), pending, inSet);
        }

        return walker;
    }

    /// <summary>Adds a glyph that is not in the set yet.</summary>
    /// <param name="glyph">The glyph, or -1.</param>
    /// <param name="pending">The glyphs to follow, which also holds a renumbered subset's glyphs in order.</param>
    /// <param name="inSet">Which glyphs are in the set.</param>
    private static void Add(int glyph, List<int> pending, bool[] inSet)
    {
        if ((uint)glyph >= (uint)inSet.Length || inSet[glyph])
        {
            return;
        }

        inSet[glyph] = true;
        pending.Add(glyph);
    }
}
