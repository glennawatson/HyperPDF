// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Loads /Type1, /MMType1 and /TrueType fonts (ISO 32000-2 clauses 9.6.2 and 9.6.3): descriptor, widths, encoding,
/// then the glyph map.
/// </summary>
internal static class SimpleFontLoader
{
    /// <summary>The width of every glyph of the Courier standard fonts.</summary>
    private const float CourierWidth = 600;

    /// <summary>The glyph space units per text space unit.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The code of the space character.</summary>
    private const int SpaceCode = 32;

    /// <summary>The distance from a lowercase letter to its capital.</summary>
    private const int CaseOffset = 32;

    /// <summary>Gets the ranges of lowercase codes an AllCap font draws with capitals: a to z, à to ö, ø to ý.</summary>
    private static ReadOnlySpan<byte> LowercaseRanges => [0x61, 0x7A, 0xE0, 0xF6, 0xF8, 0xFD];

    /// <summary>Loads a simple font.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="trueType">Whether the /Subtype is /TrueType.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfSimpleFont Load(PdfDictionary font, bool trueType, CancellationToken cancellationToken = default) =>
        Load(font, trueType, PdfFontResources.For(font), cancellationToken);

    /// <summary>Loads a simple font with resources selected by the caller.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="trueType">Whether the /Subtype is /TrueType.</param>
    /// <param name="resources">The selected resources.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The font.</returns>
    internal static PdfSimpleFont Load(PdfDictionary font, bool trueType, PdfFontResources resources, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var facts = ReadFacts(font, trueType, cancellationToken);
        var encoding = SimpleEncoding.Read(font, new(InitialEncoding(facts), facts.Program is not null, facts.HasTrueTypeFace, facts.IsSymbolic, IsSymbolName(facts)));
        var toUnicode = ToUnicodeLoader.Load(font, cancellationToken);
        GlyphSource source;
        SimpleGlyphMap map;
        if (facts.Program is { } program)
        {
            source = new ProgramGlyphSource(program);
            map = trueType && program is TrueTypeProgram tt
                ? TrueTypeGlyphMapper.Map(resources, tt, encoding, facts.Flags, toUnicode, font.GetInt32(KnownName.FirstChar))
                : Type1GlyphMapper.Map(resources, program, encoding, facts.IsSymbolic, encoding.Base == FontEncoding.ZapfDingbats);
        }
        else
        {
            var matcher = SystemFontMatcher.For(font, resources);
            var face = matcher.Match(new(facts.BaseFont, facts.Standard, facts.Flags, facts.Descriptor.Weight, CjkScript.None));
            source = face;
            map = SubstituteGlyphMapper.Map(resources, face, encoding, facts.SymbolEncoding, facts.IsSymbolic, toUnicode);
        }

        var hasWidths = font.GetArray(KnownName.Widths) is not null;
        var widths = ReadWidths(facts, encoding, map, source);
        ApplyAllCaps(facts, map, widths);
        var metrics = new FontMetrics(
            ChooseAscent(facts, source),
            ChooseDescent(facts, source),
            IsBold(facts),
            hasWidths && source is SubstituteFace { IsRequestedFamily: false } && facts.Standard == StandardFont.None);
        return new(font, new(source, map.Glyphs, map.Texts, widths, toUnicode, metrics, PuaSymbols.KindOf(facts.BaseFont, facts.Standard)), resources);
    }

    /// <summary>Reads the dictionary, descriptor and program.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="trueType">Whether the /Subtype is /TrueType.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The facts.</returns>
    private static SimpleFontFacts ReadFacts(PdfDictionary font, bool trueType, CancellationToken cancellationToken)
    {
        var baseFont = PdfNames.BaseFontOf(font);
        var standard = trueType ? StandardFont.None : StandardFonts.Find(Encoding.UTF8.GetBytes(baseFont));
        var descriptor = FontDescriptor.Read(font.GetDictionary(KnownName.FontDescriptor));
        var program = EmbeddedFontLoader.Load(descriptor, cancellationToken);
        var flags = descriptor.Flags;
        if (standard != StandardFont.None && !descriptor.HasFlags)
        {
            flags = standard is StandardFont.Symbol or StandardFont.ZapfDingbats ? FontFlags.Symbolic : FontFlags.Nonsymbolic;
        }

        return new(font, baseFont, standard, descriptor, flags, program, trueType);
    }

    /// <summary>Chooses the base encoding before /Encoding is read: symbolic fonts start from the font program's own encoding, others from StandardEncoding (ISO 32000-2, 9.6.5.1).</summary>
    /// <param name="facts">The facts.</param>
    /// <returns>The encoding; <see cref="FontEncoding.None"/> for built-in.</returns>
    private static FontEncoding InitialEncoding(SimpleFontFacts facts)
    {
        var symbolEncoding = facts.SymbolEncoding;
        if (symbolEncoding != FontEncoding.None)
        {
            return facts.IsSymbolic ? symbolEncoding : FontEncoding.Standard;
        }

        return facts.IsSymbolic ? FontEncoding.None : FontEncoding.Standard;
    }

    /// <summary>Determines whether the font is named Symbol, which changes how /Encoding is read.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns><see langword="true"/> for the Symbol font.</returns>
    private static bool IsSymbolName(SimpleFontFacts facts) =>
        facts.Standard == StandardFont.Symbol || string.Equals(facts.BaseFont, "Symbol", StringComparison.Ordinal);

    /// <summary>Reads the widths: /Widths with /MissingWidth, else standard metrics or the glyph advances.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="map">The glyph map.</param>
    /// <param name="source">The glyph source.</param>
    /// <returns>The widths in text space units.</returns>
    private static float[] ReadWidths(SimpleFontFacts facts, SimpleEncoding encoding, SimpleGlyphMap map, GlyphSource source)
    {
        var widths = new float[FontEncodings.CodeCount];
        if (facts.Standard is >= StandardFont.Courier and <= StandardFont.CourierOblique)
        {
            widths.AsSpan().Fill(CourierWidth / GlyphUnits);
        }

        if (facts.Font.GetArray(KnownName.Widths) is { } array)
        {
            ReadWidthArray(facts, array, widths);
            return widths;
        }

        for (var code = 0; code < widths.Length; code++)
        {
            widths[code] = FontWidth(facts, encoding, map, source, code) / GlyphUnits;
        }

        if (facts.Program is null)
        {
            // A code the substitute face cannot draw and that has no width keeps the advance of the space, so
            // the text after it does not collapse onto it.
            for (var code = 0; code < widths.Length; code++)
            {
                if (map.Glyphs[code] < 0 && code != SpaceCode && widths[code] == 0)
                {
                    widths[code] = widths[SpaceCode];
                }
            }
        }

        return widths;
    }

    /// <summary>Reads /FirstChar, /LastChar and /Widths, with /MissingWidth for the other codes.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="array">The /Widths array.</param>
    /// <param name="widths">The widths to fill.</param>
    private static void ReadWidthArray(SimpleFontFacts facts, PdfArray array, float[] widths)
    {
        if (facts.Descriptor.MissingWidth is { } missing)
        {
            widths.AsSpan().Fill(missing / GlyphUnits);
        }

        var first = facts.Font.GetInt32(KnownName.FirstChar);
        var last = facts.Font.GetInt32(KnownName.LastChar);
        if ((uint)first >= FontEncodings.CodeCount || array.Count == 0)
        {
            return;
        }

        if (last == 0 || last >= first + array.Count)
        {
            last = first + array.Count - 1;
        }

        last = Math.Min(last, FontEncodings.CodeCount - 1);
        for (var code = first; code <= last; code++)
        {
            widths[code] = array.GetSingle(code - first) / GlyphUnits;
        }
    }

    /// <summary>Gets a code's width from the standard metrics or the glyph source, in glyph units.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="encoding">The encoding.</param>
    /// <param name="map">The glyph map.</param>
    /// <param name="source">The glyph source.</param>
    /// <param name="code">The code.</param>
    /// <returns>The width.</returns>
    private static float FontWidth(SimpleFontFacts facts, SimpleEncoding encoding, SimpleGlyphMap map, GlyphSource source, int code)
    {
        if (facts.Program is null && facts.Standard != StandardFont.None)
        {
            var metrics = StandardFonts.Get(facts.Standard);
            var name = encoding.GetName(code);
            if (!name.IsEmpty ? metrics.TryGetWidth(name, out var named) : metrics.TryGetWidth(code, out named))
            {
                return named;
            }
        }

        var glyph = map.Glyphs[code];
        return glyph >= 0 ? source.GetAdvance(glyph) : 0;
    }

    /// <summary>Draws the lowercase codes of an AllCap font with the capitals' glyphs, for the AllCap font flag (ISO 32000-2, 9.8.2, Table 121).</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="map">The glyph map.</param>
    /// <param name="widths">The widths.</param>
    private static void ApplyAllCaps(SimpleFontFacts facts, SimpleGlyphMap map, float[] widths)
    {
        if ((facts.Flags & FontFlags.AllCap) == 0)
        {
            return;
        }

        var ranges = LowercaseRanges;
        for (var range = 0; range < ranges.Length; range += 1 + 1)
        {
            for (int code = ranges[range]; code <= ranges[range + 1]; code++)
            {
                if (map.Glyphs[code] >= 0 && facts.Program is not null)
                {
                    continue;
                }

                map.Glyphs[code] = map.Glyphs[code - CaseOffset];
                if (widths[code - CaseOffset] != 0)
                {
                    widths[code] = widths[code - CaseOffset];
                }
            }
        }
    }

    /// <summary>Chooses the ascent: the descriptor's, the standard face's, then the glyph source's.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="source">The glyph source.</param>
    /// <returns>The ascent in text space units.</returns>
    private static float ChooseAscent(SimpleFontFacts facts, GlyphSource source)
    {
        if (facts.Descriptor.Ascent != 0)
        {
            return facts.Descriptor.Ascent / GlyphUnits;
        }

        return (DrawnWithStandardFace(facts) ? StandardFonts.GetAscent(facts.Standard) : source.Ascent) / GlyphUnits;
    }

    /// <summary>Chooses the descent: the descriptor's, the standard face's, then the glyph source's.</summary>
    /// <param name="facts">The facts.</param>
    /// <param name="source">The glyph source.</param>
    /// <returns>The descent in text space units.</returns>
    private static float ChooseDescent(SimpleFontFacts facts, GlyphSource source)
    {
        if (facts.Descriptor.Descent != 0)
        {
            return facts.Descriptor.Descent / GlyphUnits;
        }

        return (DrawnWithStandardFace(facts) ? StandardFonts.GetDescent(facts.Standard) : source.Descent) / GlyphUnits;
    }

    /// <summary>
    /// Determines whether the font is a standard 14 font with no embedded program, so the published Adobe metrics
    /// give its ascent and descent.
    /// </summary>
    /// <param name="facts">The facts.</param>
    /// <returns><see langword="true"/> for a standard font drawn with a standard face.</returns>
    private static bool DrawnWithStandardFace(SimpleFontFacts facts) => facts.Standard != StandardFont.None && facts.Program is null;

    /// <summary>Determines whether the font is bold, from its flags, weight or name.</summary>
    /// <param name="facts">The facts.</param>
    /// <returns><see langword="true"/> when bold.</returns>
    private static bool IsBold(SimpleFontFacts facts) => FontStyle.IsBold(facts.Flags, facts.Descriptor.Weight, facts.BaseFont)
        || (facts.Standard != StandardFont.None && StandardFonts.IsBold(facts.Standard));
}
