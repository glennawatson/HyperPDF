// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on embedded font programs (ISO 19005-1, 6.3.4 and 6.3.7; ISO 19005-2 and -3, 6.2.11.3.2, 6.2.11.4.1
/// and 6.2.11.6; ISO 19005-4, 6.2.10.3.2, 6.2.10.4.1 and 6.2.10.6): programs are embedded, TrueType CIDFonts map CIDs
/// to glyphs, and TrueType 'cmap' subtables and encodings suit the symbolic flag.
/// </summary>
internal static class PdfAFontProgramRules
{
    /// <summary>The rendering modes other than 3, as bits.</summary>
    private const int RenderedModes = 0xF7;

    /// <summary>The Microsoft platform.</summary>
    private const int Microsoft = 3;

    /// <summary>The Macintosh platform.</summary>
    private const int Macintosh = 1;

    /// <summary>The Microsoft Unicode BMP encoding.</summary>
    private const int UnicodeBmp = 1;

    /// <summary>Gets the rules this class checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.FontEmbedded,
        PdfARule.CidFontToGidMap,
        PdfARule.TrueTypeSymbolicCmapPdfA4,
        PdfARule.TrueTypeSymbolicCmapPdfA1,
        PdfARule.TrueTypeSymbolicCmap,
        PdfARule.TrueTypeCmap,
        PdfARule.TrueTypeCmapPdfA4,
        PdfARule.TrueTypeEncoding,
    ];

    /// <summary>Checks a used font's program.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    internal static void Check(PdfAValidationContext context, PdfAFontUse font)
    {
        if (font.Kind == PdfAFontKind.Type3)
        {
            return;
        }

        var store = context.Document.Objects;
        var program = PdfAFontPrograms.Get(context, font);
        if (font.Kind == PdfAFontKind.Type0)
        {
            CheckDescendant(context, store, font, program);
            return;
        }

        var where = PdfAFontFacts.Context(store, font.Dictionary);
        context.Require((font.Modes & RenderedModes) == 0 || program.IsEmbedded, PdfARule.FontEmbedded, where);
        if (font.Kind == PdfAFontKind.TrueType)
        {
            CheckTrueType(context, store, font.Dictionary, program, where);
        }
    }

    /// <summary>Checks the program of a composite font's descendant CIDFont.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="font">The composite font.</param>
    /// <param name="program">The descendant's program.</param>
    private static void CheckDescendant(PdfAValidationContext context, PdfObjectStore store, PdfAFontUse font, PdfAFontProgram program)
    {
        if (PdfAFontFacts.Descendant(font.Dictionary) is not { } descendant)
        {
            return;
        }

        var where = PdfAFontFacts.Context(store, descendant);
        var subtype = descendant.GetName(KnownName.Subtype).ToKnownName();
        context.Require((font.Modes & RenderedModes) == 0 || subtype is KnownName.Type3 or KnownName.Type0 || program.IsEmbedded, PdfARule.FontEmbedded, where);
        context.Require(subtype != KnownName.CIDFontType2 || PdfACompositeFontRules.HasCidToGidMap(descendant) || !program.IsEmbedded, PdfARule.CidFontToGidMap, where);
    }

    /// <summary>Checks a TrueType font's 'cmap' subtables and its encoding.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="program">The program.</param>
    /// <param name="where">The report context.</param>
    private static void CheckTrueType(PdfAValidationContext context, PdfObjectStore store, PdfDictionary dictionary, PdfAFontProgram program, string where)
    {
        var symbolic = PdfAFontFacts.IsSymbolic(PdfAFontFacts.Descriptor(dictionary));
        var encoding = PdfATrueTypeEncoding.Read(store, dictionary);
        context.Require(symbolic || (encoding.IsStandard && (!encoding.HasDifferences || DifferencesAreUnicode(store, encoding, program))), PdfARule.TrueTypeEncoding, where);
        if (program.IsEmbedded && program.TrueType is { } tables)
        {
            CheckCmaps(context, tables, symbolic, where);
        }
    }

    /// <summary>Checks a TrueType program's 'cmap' subtables against the symbolic flag.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="tables">The program's tables.</param>
    /// <param name="symbolic">Whether the font is symbolic.</param>
    /// <param name="where">The report context.</param>
    private static void CheckCmaps(PdfAValidationContext context, PdfATrueTypeTables tables, bool symbolic, string where)
    {
        var count = tables.Cmaps.Length;
        var symbol = tables.Cmap(Microsoft, 0) is not null;
        var unicode = tables.Cmap(Microsoft, UnicodeBmp) is not null;
        var roman = tables.Cmap(Macintosh, 0) is not null;
        context.Require(!symbolic || symbol || roman, PdfARule.TrueTypeSymbolicCmapPdfA4, where);
        context.Require(!symbolic || count == 1, PdfARule.TrueTypeSymbolicCmapPdfA1, where);
        context.Require(!symbolic || count == 1 || symbol, PdfARule.TrueTypeSymbolicCmap, where);
        context.Require(symbolic || (symbol ? count > 1 : count > 0), PdfARule.TrueTypeCmap, where);
        context.Require(symbolic || unicode || roman, PdfARule.TrueTypeCmapPdfA4, where);
    }

    /// <summary>
    /// Determines whether the /Differences of a non-symbolic TrueType font can be looked up through Unicode: the program
    /// has a Microsoft Unicode 'cmap' and every name is in the Adobe Glyph List. A font with no program passes.
    /// </summary>
    /// <param name="store">The objects.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="program">The program.</param>
    /// <returns><see langword="true"/> when they can.</returns>
    private static bool DifferencesAreUnicode(PdfObjectStore store, PdfATrueTypeEncoding encoding, PdfAFontProgram program)
    {
        if (!program.Present)
        {
            return true;
        }

        if (!program.Parsed || program.TrueType?.Cmap(Microsoft, UnicodeBmp) is null)
        {
            return false;
        }

        var differences = encoding.Differences;
        for (var i = 0; differences is not null && i < differences.Count; i++)
        {
            if (differences.Get(i).TryGetName(out var name) && !PdfAGlyphNames.IsListed(store.Names.GetSpelling(name)))
            {
                return false;
            }
        }

        return true;
    }
}
