// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Rewrites ToUnicode CMaps whose bfrange entries cross a 256-code block, which the CMap format does not allow
/// (Adobe Technical Note #5014, 7.2) and which validators read as missing text (ISO 19005-2 §6.2.11.7.2). The new map holds
/// the text the library reads from the old one, code by code, so nothing a reader can extract is lost.
/// </summary>
internal static class PdfAConversionToUnicodeRepair
{
    /// <summary>The number of two-byte codes.</summary>
    private const int TwoByteCodes = 65_536;

    /// <summary>The number of one-byte codes.</summary>
    private const int OneByteCodes = 256;

    /// <summary>The most UTF-16 units one code's text is read into.</summary>
    private const int MaxText = 32;

    /// <summary>Rewrites the broken maps.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var graph = PdfObjectGraph.Collect(session.Store, false, session.CancellationToken);
        var repaired = 0;
        for (var number = 1; number <= graph.Count; number++)
        {
            if (graph.GetValue(number).Kind == PdfKind.Dictionary && graph.GetValue(number).AsDictionary() is { } font)
            {
                repaired += Repair(session, new(graph.GetOldNumber(number), 0), font) ? 1 : 0;
            }
        }

        if (repaired > 0)
        {
            session.Changed("Rewrote damaged character-to-text maps of some fonts, so their text can be read.", PdfARule.GlyphToUnicode);
        }
    }

    /// <summary>
    /// Builds a ToUnicode CMap for a font from the text the library finds for each of its codes. A code that an existing map
    /// already maps keeps that text.
    /// </summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="existing">The font's current map, whose text wins, or <see langword="null"/>.</param>
    /// <returns>The CMap bytes, or <see langword="null"/> when the font's codes have no known width or the font cannot be loaded.</returns>
    internal static byte[]? Build(PdfDictionary font, PdfAToUnicode? existing)
    {
        if (CodeCount(font) is not (> 0 and var codes) || PdfFontLoader.Load(font) is not { } loaded)
        {
            return null;
        }

        var texts = ReadTexts(loaded, codes, existing);
        return codes == OneByteCodes ? PdfAConversionToUnicode.Write(OneByte(texts)) : PdfToUnicodeMaps.Write(texts);
    }

    /// <summary>Rewrites one font's map when it is broken and the font's codes have a known width.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="id">The font object.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns><see langword="true"/> when the map was rewritten.</returns>
    private static bool Repair(PdfAConversionSession session, PdfObjectId id, PdfDictionary font)
    {
        var store = session.Store;
        var raw = font.GetRaw(KnownName.ToUnicode);
        if (StoreReading.Resolve(store, raw).AsStream() is not { } map || CodeCount(font) == 0)
        {
            return false;
        }

        if (!PdfAConversionBfRanges.HasCrossingRange(map.DecodeToArray(session.CancellationToken)) || Build(font, null) is not { } bytes)
        {
            return false;
        }

        var stream = PdfValue.FromStream(new(new PdfDictionary(store, 0), bytes));
        if (raw.IsReference)
        {
            StoreEditing.Replace(store, raw.AsReference(), stream);
            return true;
        }

        // A stream is an indirect object, so a map held directly in the font dictionary moves into an object of its own.
        var copy = font.Clone();
        copy.Set(KnownName.ToUnicode, PdfValue.FromReference(StoreEditing.Add(store, stream)));
        StoreEditing.Replace(store, id, PdfValue.FromDictionary(copy));
        return true;
    }

    /// <summary>Reads the text the library finds for each code, leaving out characters a ToUnicode map may not hold.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="codes">The number of codes.</param>
    /// <param name="existing">The font's current map, whose text wins, or <see langword="null"/>.</param>
    /// <returns>The text of each code, or <see langword="null"/>.</returns>
    private static string?[] ReadTexts(PdfFont font, int codes, PdfAToUnicode? existing)
    {
        var texts = new string?[codes];
        Span<char> text = stackalloc char[MaxText];
        for (var code = 0; code < codes; code++)
        {
            var length = Math.Min(font.GetUnicode(code, text), MaxText);
            texts[code] = existing?.Text(code) ?? (length > 0 && text[..length].IndexOfAny('\0', '￾', '﻿') < 0 ? text[..length].ToString() : null);
        }

        return texts;
    }

    /// <summary>Gets the size of a font's code space: 256 for simple fonts, 65,536 for Identity composite fonts.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <returns>The number of codes, or 0 for fonts whose codes this repair does not handle.</returns>
    private static int CodeCount(PdfDictionary font) => font.GetName(KnownName.Subtype).ToKnownName() switch
    {
        KnownName.Type1 or KnownName.MMType1 or KnownName.TrueType or KnownName.Type3 => OneByteCodes,
        KnownName.Type0 when font.Get(KnownName.Encoding).IsName(KnownName.IdentityH) || font.Get(KnownName.Encoding).IsName(KnownName.IdentityV) => TwoByteCodes,
        _ => 0,
    };

    /// <summary>Collects the one-byte codes that have text.</summary>
    /// <param name="texts">The text of each code.</param>
    /// <returns>The codes and texts.</returns>
    private static SortedDictionary<int, string> OneByte(string?[] texts)
    {
        var map = new SortedDictionary<int, string>();
        for (var code = 0; code < texts.Length; code++)
        {
            if (texts[code] is { } text)
            {
                map[code] = text;
            }
        }

        return map;
    }
}
