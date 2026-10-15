// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Decides what goes into an embedded font once its glyphs are known: the program, the widths, the texts and the descriptor.</summary>
internal static class EmbeddedFontPlanner
{
    /// <summary>Thousandths in one em, the unit of PDF metrics.</summary>
    private const float Thousand = 1000F;

    /// <summary>The descriptor flag for a fixed pitch font.</summary>
    private const int FixedPitchFlag = 1;

    /// <summary>The descriptor flag for a serif font.</summary>
    private const int SerifFlag = 2;

    /// <summary>The descriptor flag for a symbolic font, which one without a Unicode character map is.</summary>
    private const int SymbolicFlag = 4;

    /// <summary>The descriptor flag for a nonsymbolic font.</summary>
    private const int NonsymbolicFlag = 32;

    /// <summary>The descriptor flag for an italic font.</summary>
    private const int ItalicFlag = 64;

    /// <summary>The stem width of a regular weight font.</summary>
    private const float RegularStem = 80F;

    /// <summary>How much the stem grows for each unit of weight above regular.</summary>
    private const float StemPerWeight = 0.2F;

    /// <summary>The smallest stem width written.</summary>
    private const int MinimumStem = 20;

    /// <summary>The regular weight.</summary>
    private const int RegularWeight = 400;

    /// <summary>A tag of the right length, which stands in the CFF Name INDEX until the glyphs are known and the real tag is written over it.</summary>
    private const string PlaceholderTag = "AAAAAA+";

    /// <summary>The bytes of a glyph in the CID to glyph map.</summary>
    private const int MapEntry = 2;

    /// <summary>Plans a TrueType font.</summary>
    /// <param name="source">The font.</param>
    /// <param name="use">The glyphs shown.</param>
    /// <param name="writeCidSet">Whether to write a <c>/CIDSet</c>.</param>
    /// <param name="state">The document's reusable embedding buffers.</param>
    /// <returns>The font's parts.</returns>
    internal static EmbeddedFontParts PlanTrueType(PdfFontSource source, in GlyphUseSnapshot use, bool writeCidSet, PdfFontEmbeddingState state)
    {
        var subset = source.CanSubset ? TrueTypeRenumberer.Subset(source.Font, source.Facts, use.Glyphs, state) : null;
        var glyphs = With(0, use.Glyphs);
        var cids = With(0, use.Codes);
        var baseFont = subset is null ? source.PostScriptName : SubsetTags.Create(source.PostScriptName, subset.Original) + source.PostScriptName;
        byte[]? cidSet = null;
        if (writeCidSet)
        {
            cidSet = subset is not null ? CidSets.FromCount(subset.Original.Length) : CidSets.FromCids(cids);
        }

        var program = new EmbeddedProgramData(EmbeddedProgramKind.TrueType, subset?.Data ?? source.Font.ToStandalone(), cidSet, subset is null ? CidToGidMap(cids, glyphs) : null);
        return Describe(source, baseFont, program, cids, Widths(source, glyphs), use);
    }

    /// <summary>Plans a font with CFF outlines: the glyphs shown are cut out into a CFF font of their own when the licence allows, otherwise the whole OpenType file is embedded.</summary>
    /// <param name="source">The font.</param>
    /// <param name="use">The glyphs shown.</param>
    /// <param name="writeCidSet">Whether to write a <c>/CIDSet</c>.</param>
    /// <returns>The font's parts.</returns>
    internal static EmbeddedFontParts PlanCff(PdfFontSource source, in GlyphUseSnapshot use, bool writeCidSet)
    {
        var glyphs = With(0, use.Glyphs);
        var cids = With(0, use.Codes);
        var subset = source.CanSubset ? CffSubsetter.Renumber(source.Font.TableMemory(SfntTags.Cff), use.Glyphs, PlaceholderTag + source.PostScriptName) : null;
        if (subset is null)
        {
            var whole = new EmbeddedProgramData(EmbeddedProgramKind.OpenTypeCff, source.Font.ToStandalone(), writeCidSet ? CidSets.FromCids(cids) : null, null);
            return Describe(source, source.PostScriptName, whole, cids, Widths(source, glyphs), use);
        }

        var baseFont = SubsetTags.Create(source.PostScriptName, subset.Original) + source.PostScriptName;
        CffWriting.SetName(subset.Data, baseFont);

        // A name-keyed program has no CIDs of its own for a reader to match a /CIDSet against, so none is written (it is optional, ISO 32000-2, 9.8.3).
        var program = new EmbeddedProgramData(EmbeddedProgramKind.CompactFont, subset.Data, writeCidSet && subset.IsCidKeyed ? CidSets.FromCount(subset.Original.Length) : null, null);
        return Describe(source, baseFont, program, cids, Widths(source, glyphs), use);
    }

    /// <summary>Fills in the parts every kind of font shares: the descriptor values and the <c>/ToUnicode</c> map.</summary>
    /// <param name="source">The font.</param>
    /// <param name="baseFont">The name, with a subset tag if any.</param>
    /// <param name="program">The font program and its CID maps.</param>
    /// <param name="cids">The CIDs shown, ascending.</param>
    /// <param name="widths">The width of each CID.</param>
    /// <param name="use">The glyphs shown.</param>
    /// <returns>The parts.</returns>
    internal static EmbeddedFontParts Describe(PdfFontSource source, string baseFont, in EmbeddedProgramData program, int[] cids, float[] widths, in GlyphUseSnapshot use)
    {
        var facts = source.Facts;
        var scale = Thousand / facts.UnitsPerEm;
        var box = new PdfRectangle(facts.XMin * scale, facts.YMin * scale, facts.XMax * scale, facts.YMax * scale);
        return new(baseFont, program.Kind, program.Data, box, cids, widths)
        {
            Flags = Flags(facts),
            ItalicAngle = facts.ItalicAngle,
            Ascent = facts.Ascent * scale,
            Descent = facts.Descent * scale,
            CapHeight = facts.CapHeight * scale,
            StemV = Math.Max(MinimumStem, (int)(RegularStem + ((facts.WeightClass - RegularWeight) * StemPerWeight))),
            ToUnicode = ToUnicode(use.Texts),
            CidSet = program.CidSet,
            CidToGidMap = program.CidToGidMap,
        };
    }

    /// <summary>Gets the width of each glyph, rounded to a whole thousandth.</summary>
    /// <param name="source">The font.</param>
    /// <param name="glyphs">The glyphs.</param>
    /// <returns>The widths.</returns>
    internal static float[] Widths(PdfFontSource source, ReadOnlySpan<int> glyphs)
    {
        var widths = new float[glyphs.Length];
        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] = MathF.Round(source.GetAdvance(glyphs[i]));
        }

        return widths;
    }

    /// <summary>Puts a value in front of an array.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="rest">The rest.</param>
    /// <returns>The joined array.</returns>
    internal static int[] With(int first, int[] rest)
    {
        var joined = new int[rest.Length + 1];
        joined[0] = first;
        rest.CopyTo(joined, 1);
        return joined;
    }

    /// <summary>Gets the descriptor flags.</summary>
    /// <param name="facts">The font's facts.</param>
    /// <returns>The flags.</returns>
    private static int Flags(FontFacts facts)
    {
        var flags = (facts.Style & FaceStyleFlags.UnicodeCmap) != 0 ? NonsymbolicFlag : SymbolicFlag;
        flags |= (facts.Style & FaceStyleFlags.FixedPitch) != 0 ? FixedPitchFlag : 0;
        flags |= (facts.Style & FaceStyleFlags.Serif) != 0 ? SerifFlag : 0;
        flags |= (facts.Style & FaceStyleFlags.Italic) != 0 ? ItalicFlag : 0;
        return flags;
    }

    /// <summary>Builds the <c>/ToUnicode</c> CMap from the recorded texts.</summary>
    /// <param name="texts">The text of each code.</param>
    /// <returns>The CMap program, or <see langword="null"/> when no text was recorded.</returns>
    private static byte[]? ToUnicode(Dictionary<int, string> texts)
    {
        if (texts.Count == 0)
        {
            return null;
        }

        var last = 0;
        foreach (var entry in texts)
        {
            last = Math.Max(last, entry.Key);
        }

        var rented = ArrayPool<string?>.Shared.Rent(last + 1);
        var byCode = rented.AsSpan(0, last + 1);
        byCode.Clear();
        try
        {
            foreach (var (code, text) in texts)
            {
                byCode[code] = text;
            }

            return PdfToUnicodeMaps.Write(byCode);
        }
        finally
        {
            ArrayPool<string?>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <summary>Builds the <c>/CIDToGIDMap</c> data that sends each CID to its glyph.</summary>
    /// <param name="cids">The CIDs, ascending.</param>
    /// <param name="glyphs">The glyph of each CID.</param>
    /// <returns>Two bytes of glyph for each CID up to the last.</returns>
    private static byte[] CidToGidMap(int[] cids, int[] glyphs)
    {
        var map = new byte[(cids[^1] + 1) * MapEntry];
        for (var i = 0; i < cids.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(map.AsSpan(cids[i] * MapEntry), (ushort)glyphs[i]);
        }

        return map;
    }
}
