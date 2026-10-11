// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>Loads /Type0 fonts as PDFium's CPDF_CIDFont::Load does.</summary>
internal static class CompositeFontLoader
{
    /// <summary>The default /DW.</summary>
    private const float DefaultWidth = 1000;

    /// <summary>The default vertical origin y of /DW2.</summary>
    private const float DefaultOriginY = 880;

    /// <summary>The default vertical advance of /DW2.</summary>
    private const float DefaultVerticalAdvance = -1000;

    /// <summary>The glyph space units per text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>Loads a composite font.</summary>
    /// <param name="font">The /Type0 font dictionary.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The font, or <see langword="null"/> when it has no single descendant or no /Encoding, as PDFium refuses it.</returns>
    internal static PdfCompositeFont? Load(PdfDictionary font, CancellationToken cancellationToken = default)
    {
        if (font.GetArray(KnownName.DescendantFonts) is not { Count: 1 } descendants || descendants.GetDictionary(0) is not { } cidFont)
        {
            return null;
        }

        if (ReadCMap(font, cancellationToken) is not { } cmap)
        {
            return null;
        }

        var descriptor = FontDescriptor.Read(cidFont.GetDictionary(KnownName.FontDescriptor));
        var program = EmbeddedFontLoader.Load(descriptor, cancellationToken);
        var toUnicode = ToUnicodeLoader.Load(font, cancellationToken);
        var baseFont = PdfNames.BaseFontOf(cidFont);
        var collection = ReadCollection(cmap, cidFont);
        GlyphSource source = program is not null
            ? new ProgramGlyphSource(program)
            : SystemFontMatcher.Match(new(baseFont, StandardFont.None, descriptor.Flags, descriptor.Weight, collection));
        var route = program is null ? CidGlyphRoute.Identity : ChooseRoute(cidFont, program, cancellationToken);
        var metrics = new FontMetrics(
            (descriptor.Ascent != 0 ? descriptor.Ascent : source.Ascent) / GlyphUnits,
            (descriptor.Descent != 0 ? descriptor.Descent : source.Descent) / GlyphUnits,
            FontStyle.IsBold(descriptor.Flags, descriptor.Weight, baseFont),
            false);
        return new(font, new(
            cmap,
            source,
            route,
            CidMetricsTable.ReadWidths(cidFont.GetArray(KnownName.W)),
            cidFont.Get(KnownName.DW).IsNumber ? cidFont.GetSingle(KnownName.DW) : DefaultWidth,
            cmap.IsVertical ? CidMetricsTable.ReadVertical(cidFont.GetArray(KnownName.W2)) : CidMetricsTable.Empty,
            ReadVerticalDefault(cidFont.GetArray(KnownName.DW2)),
            toUnicode,
            CidToUnicodeTable.Get(collection),
            metrics));
    }

    /// <summary>Reads /Encoding: a predefined CMap name or an embedded CMap stream.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The CMap, or <see langword="null"/> when /Encoding is missing or damaged.</returns>
    private static CompositeCMap? ReadCMap(PdfDictionary font, CancellationToken cancellationToken)
    {
        var encoding = font.Get(KnownName.Encoding);
        if (encoding.TryGetName(out var name))
        {
            return PredefinedCMaps.Get(PdfNames.Spell(font, name));
        }

        if (encoding.AsStream() is not { } stream)
        {
            return null;
        }

        try
        {
            // PDFium gives embedded CMaps no coding, so their codes have no text without /ToUnicode.
            var map = CMap.Parse(stream.DecodeToArray(cancellationToken), static name => PredefinedCMaps.Find(name)?.Map);
            return new(map, CidCoding.Unknown, CjkScript.None);
        }
        catch (Exception ex) when (FontLoadErrors.IsDamagedData(ex))
        {
            return null;
        }
    }

    /// <summary>Chooses how CIDs become glyph ids in an embedded program.</summary>
    /// <param name="cidFont">The descendant font.</param>
    /// <param name="program">The program.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The route.</returns>
    private static CidGlyphRoute ChooseRoute(PdfDictionary cidFont, FontProgram program, CancellationToken cancellationToken)
    {
        if (cidFont.GetStream(KnownName.CIDToGIDMap) is { } table)
        {
            try
            {
                return new(table.DecodeToArray(cancellationToken), null);
            }
            catch (Exception ex) when (FontLoadErrors.IsDamagedData(ex))
            {
                return CidGlyphRoute.Identity;
            }
        }

        // PDFium uses the CID as the glyph id of an embedded TrueType program, whatever the CMap.
        return program is CffProgram { IsCidKeyed: true } cff ? new(null, cff) : CidGlyphRoute.Identity;
    }

    /// <summary>Finds the font's character collection as PDFium's charset_: the predefined CMap's, else /CIDSystemInfo /Ordering.</summary>
    /// <param name="cmap">The CMap.</param>
    /// <param name="cidFont">The descendant font.</param>
    /// <returns>The collection.</returns>
    private static CjkScript ReadCollection(CompositeCMap cmap, PdfDictionary cidFont) =>
        cmap.Collection != CjkScript.None ? cmap.Collection : ReadScript(cidFont);

    /// <summary>Reads the CJK script from /CIDSystemInfo /Ordering.</summary>
    /// <param name="cidFont">The descendant font.</param>
    /// <returns>The script.</returns>
    private static CjkScript ReadScript(PdfDictionary cidFont)
    {
        if (cidFont.GetDictionary(KnownName.CIDSystemInfo) is not { } info)
        {
            return CjkScript.None;
        }

        var ordering = info.GetStringBytes(KnownName.Ordering);
        if (ordering.SequenceEqual("Japan1"u8))
        {
            return CjkScript.Japanese;
        }

        if (ordering.SequenceEqual("GB1"u8))
        {
            return CjkScript.SimplifiedChinese;
        }

        if (ordering.SequenceEqual("CNS1"u8))
        {
            return CjkScript.TraditionalChinese;
        }

        return ordering.SequenceEqual("Korea1"u8) ? CjkScript.Korean : CjkScript.None;
    }

    /// <summary>Reads /DW2.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <returns>The vertical defaults.</returns>
    private static VerticalDefault ReadVerticalDefault(PdfArray? array) =>
        array is { Count: >= 1 + 1 } ? new(array.GetSingle(0), array.GetSingle(1)) : new(DefaultOriginY, DefaultVerticalAdvance);
}
