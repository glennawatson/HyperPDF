// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on the entries of a used font dictionary (ISO 19005-1, 6.3.2 and 6.3.7; ISO 19005-2 and -3, 6.2.11.2
/// and 6.2.11.6; ISO 19005-4, 6.2.10.2 and 6.2.10.6), read as veraPDF reads them.
/// </summary>
internal static class PdfAFontDictionaryRules
{
    /// <summary>Gets the rules this class checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.FontType,
        PdfARule.FontName,
        PdfARule.FontSubtype,
        PdfARule.FontFileSubtypePdfA1,
        PdfARule.FontFileSubtype,
        PdfARule.SimpleFontFirstChar,
        PdfARule.SimpleFontLastChar,
        PdfARule.SimpleFontWidths,
        PdfARule.TrueTypeSymbolicEncoding,
        PdfARule.TrueTypeEncodingPdfA1,
    ];

    /// <summary>Checks one used font and, for a composite font, its descendant CIDFont.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    internal static void Check(PdfAValidationContext context, PdfAFontUse font)
    {
        var store = context.Document.Objects;
        var primary = PdfAFontFacts.Primary(font);
        var where = PdfAFontFacts.Context(store, primary);
        CheckEntries(context, primary, font.Kind == PdfAFontKind.Type3, where);
        if (font.Kind == PdfAFontKind.Type0)
        {
            if (PdfAFontFacts.Descendant(font.Dictionary) is { } descendant)
            {
                CheckEntries(context, descendant, false, where);
                context.Require(IsFontSubtype(descendant.GetName(KnownName.Subtype).ToKnownName()), PdfARule.FontSubtype, where);
            }

            return;
        }

        CheckSimple(context, store, font, where);
        if (font.Kind == PdfAFontKind.TrueType)
        {
            CheckTrueTypeEncoding(context, store, font.Dictionary, where);
        }
    }

    /// <summary>Checks the entries every font object carries: /Type, /BaseFont and the program subtype.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="dictionary">The dictionary veraPDF reads the entries from.</param>
    /// <param name="isType3">Whether the font is a Type 3 font, which needs no /BaseFont.</param>
    /// <param name="where">The report context.</param>
    private static void CheckEntries(PdfAValidationContext context, PdfDictionary dictionary, bool isType3, string where)
    {
        context.Require(dictionary.Get(KnownName.Type).IsName(KnownName.Font), PdfARule.FontType, where);
        context.Require(isType3 || dictionary.Get(KnownName.BaseFont).TryGetName(out _), PdfARule.FontName, where);
        var subtype = PdfAFontFacts.FirstProgram(PdfAFontFacts.Descriptor(dictionary))?.Dictionary.Get(KnownName.Subtype) ?? default;
        var known = subtype.AsName().ToKnownName();
        var named = subtype.TryGetName(out _);
        context.Require(!named || known is KnownName.Type1C or KnownName.CIDFontType0C, PdfARule.FontFileSubtypePdfA1, where);
        context.Require(!named || known is KnownName.Type1C or KnownName.CIDFontType0C or KnownName.OpenType, PdfARule.FontFileSubtype, where);
    }

    /// <summary>Checks /FirstChar, /LastChar and /Widths of a simple font, which the standard 14 Type 1 fonts may omit.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font.</param>
    /// <param name="where">The report context.</param>
    private static void CheckSimple(PdfAValidationContext context, PdfObjectStore store, PdfAFontUse font, string where)
    {
        var dictionary = font.Dictionary;
        if (font.Kind == PdfAFontKind.Type1 && PdfAFontFacts.IsStandard(store, dictionary))
        {
            return;
        }

        var hasFirst = PdfAFontFacts.TryGetInteger(dictionary, KnownName.FirstChar, out var first);
        var hasLast = PdfAFontFacts.TryGetInteger(dictionary, KnownName.LastChar, out var last);
        context.Require(hasFirst, PdfARule.SimpleFontFirstChar, where);
        context.Require(hasLast, PdfARule.SimpleFontLastChar, where);

        // veraPDF evaluates the size test in JavaScript, where a missing FirstChar or LastChar counts as zero.
        context.Require(dictionary.GetArray(KnownName.Widths) is { } widths && widths.Count == last - first + 1, PdfARule.SimpleFontWidths, where);
    }

    /// <summary>Checks the /Encoding of a TrueType font against its symbolic flag.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="store">The objects.</param>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="where">The report context.</param>
    private static void CheckTrueTypeEncoding(PdfAValidationContext context, PdfObjectStore store, PdfDictionary dictionary, string where)
    {
        var symbolic = PdfAFontFacts.IsSymbolic(PdfAFontFacts.Descriptor(dictionary));
        var encoding = PdfATrueTypeEncoding.Read(store, dictionary);
        context.Require(!symbolic || encoding.Name is null, PdfARule.TrueTypeSymbolicEncoding, where);
        context.Require(symbolic || (encoding.IsStandard && !encoding.HasDifferences), PdfARule.TrueTypeEncodingPdfA1, where);
    }

    /// <summary>Determines whether a subtype is one the font rules allow.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns><see langword="true"/> for the PDF font subtypes.</returns>
    private static bool IsFontSubtype(KnownName subtype) =>
        subtype is KnownName.Type1 or KnownName.MMType1 or KnownName.TrueType or KnownName.Type3 or KnownName.Type0 or KnownName.CIDFontType0 or KnownName.CIDFontType2;
}
