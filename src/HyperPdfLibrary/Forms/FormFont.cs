// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// The font a field's text is drawn in: the entry to put in the appearance's resources and the measurements to lay
/// text out. Text is written in the font's own encoding, so fonts with <c>/Differences</c>, Mac or symbolic encodings and
/// Type0 (CJK) fonts show what was typed. A font that is not in the document, or cannot be read, is drawn as Helvetica.
/// </summary>
[DebuggerDisplay("FormFont: {Name}")]
internal sealed class FormFont
{
    /// <summary>The name used for the built-in font.</summary>
    internal const string FallbackName = "Helv";

    /// <summary>The number of codes in a simple font.</summary>
    private const int CodeCount = 256;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The width, in thousandths of an em, of a code no table gives a width for.</summary>
    private const float DefaultWidth = 500;

    /// <summary>One thousand: the units of a glyph width.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The widths of each code in thousandths of an em, for the built-in encoding.</summary>
    private readonly float[] _widths;

    /// <summary>The loaded font that measures and encodes text; <see langword="null"/> for the built-in encoding.</summary>
    private readonly PdfFont? _font;

    /// <summary>The codes of the loaded font; <see langword="null"/> for the built-in encoding.</summary>
    private readonly FormCodeMap? _codeMap;

    /// <summary>Initializes a new instance of the <see cref="FormFont"/> class.</summary>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="resource">The value to store under that name: a reference or a dictionary.</param>
    /// <param name="widths">The widths of the 256 codes of the built-in encoding.</param>
    /// <param name="metrics">The ascent, descent and line height.</param>
    /// <param name="font">The loaded font, or <see langword="null"/> to use the built-in encoding.</param>
    /// <param name="codeMap">The codes of the loaded font.</param>
    private FormFont(string name, PdfValue resource, float[] widths, FormFontMetrics metrics, PdfFont? font, FormCodeMap? codeMap)
    {
        Name = name;
        NameBytes = Encoding.UTF8.GetBytes(name);
        Resource = resource;
        _widths = widths;
        Ascent = metrics.Ascent;
        Descent = metrics.Descent;
        LineHeight = metrics.LineHeight;
        _font = font;
        _codeMap = codeMap;
    }

    /// <summary>Gets the name in the resources' <c>/Font</c> dictionary.</summary>
    internal string Name { get; }

    /// <summary>Gets the UTF-8 bytes of <see cref="Name"/>, to show the font in content.</summary>
    internal byte[] NameBytes { get; }

    /// <summary>Gets a value indicating whether this is the built-in Helvetica standing in for a font the document does not have.</summary>
    internal bool IsFallback { get; init; }

    /// <summary>Gets the value to store under <see cref="Name"/>.</summary>
    internal PdfValue Resource { get; }

    /// <summary>Gets the ascent in thousandths of an em.</summary>
    internal float Ascent { get; }

    /// <summary>Gets the descent, a negative number, in thousandths of an em.</summary>
    internal float Descent { get; }

    /// <summary>Gets the height of a line of text in thousandths of an em.</summary>
    internal float LineHeight { get; }

    /// <summary>Finds a field's font by the name its default appearance uses.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The font's name; empty to use the built-in font.</param>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="form">The AcroForm dictionary, or <see langword="null"/>.</param>
    /// <param name="pageResources">The page's resources, or <see langword="null"/>.</param>
    /// <param name="fonts">The document's font cache, or <see langword="null"/> to measure with the font's own widths and WinAnsi.</param>
    /// <returns>The font; Helvetica when the name is not found or the font cannot be used.</returns>
    internal static FormFont Find(PdfObjectStore store, string name, PdfDictionary widget, PdfDictionary? form, PdfDictionary? pageResources, PdfFontCache? fonts)
    {
        if (name.Length > 0)
        {
            var key = store.Names.Intern(name);
            var raw = LookUp(FieldAttributes.Find(widget, KnownName.DR).AsDictionary(), key);
            raw = raw.IsNull ? LookUp(form?.GetDictionary(KnownName.DR), key) : raw;
            raw = raw.IsNull ? LookUp(pageResources, key) : raw;
            if (!raw.IsNull && StoreReading.Resolve(store, raw).AsDictionary() is { } dictionary && FromDictionary(store, name, raw, dictionary, fonts) is { } found)
            {
                return found;
            }
        }

        return CreateFallback(store);
    }

    /// <summary>Creates the built-in Helvetica font.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateFallback(PdfObjectStore store) =>CreateStandard(store, StandardFont.Helvetica, FallbackName, true);

    /// <summary>Creates one of the standard 14 fonts, written in WinAnsi.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name to give it in the resources' <c>/Font</c> dictionary.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateStandard(PdfObjectStore store, StandardFont font, string name) =>CreateStandard(store, font, name, false);

    /// <summary>Encodes field text for the font; line breaks are kept as single line feeds.</summary>
    /// <param name="text">The text.</param>
    /// <returns>One unit per character.</returns>
    internal FormCodes Encode(string text)
    {
        var unitBytes = _codeMap?.UnitBytes ?? 1;
        var variable = unitBytes == 0;
        var feed = new FormCode(FormEncoding.LineFeed, variable ? 1 : unitBytes);
        var runes = SplitRunes(text);
        var widths = new float[runes.Count];
        var kinds = new byte[runes.Count];
        var codes = new FormCode[runes.Count];
        var offsets = new int[runes.Count + 1];
        for (var i = 0; i < runes.Count; i++)
        {
            var kind = KindOf(runes[i]);
            codes[i] = kind == FormCodes.LineFeed ? feed : GetCode(kind == FormCodes.Space ? new Rune(' ') : runes[i]);
            widths[i] = kind == FormCodes.LineFeed ? 0 : GetCodeWidth(codes[i].Value);
            kinds[i] = kind;
            offsets[i + 1] = offsets[i] + codes[i].Length;
        }

        var bytes = new byte[offsets[runes.Count]];
        for (var i = 0; i < codes.Length; i++)
        {
            WriteUnit(bytes, offsets[i], codes[i]);
        }

        return variable ? new(bytes, offsets, widths, kinds) : new(bytes, unitBytes, widths, kinds);
    }

    /// <summary>Creates a standard 14 font.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="isFallback">Whether the font stands in for one the document does not have.</param>
    /// <returns>The font.</returns>
    private static FormFont CreateStandard(PdfObjectStore store, StandardFont font, string name, bool isFallback)
    {
        var metrics = StandardFonts.Get(font);
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type1));
        dictionary.Set(KnownName.BaseFont, PdfValue.FromName(store.Names.Intern(metrics.Name)));
        dictionary.Set(KnownName.Encoding, PdfValue.FromName(KnownName.WinAnsiEncoding));
        return new(name, PdfValue.FromDictionary(dictionary), BuildStandardWidths(metrics), new(metrics.Ascent, metrics.Descent, LineHeightOf(metrics)), null, null) { IsFallback = isFallback };
    }

    /// <summary>Finds a font in a resources dictionary's <c>/Font</c> entry.</summary>
    /// <param name="resources">The resources, or <see langword="null"/>.</param>
    /// <param name="key">The font's name.</param>
    /// <returns>The unresolved entry; null when missing.</returns>
    private static PdfValue LookUp(PdfDictionary? resources, PdfName key) => resources?.GetDictionary(KnownName.Font)?.GetRaw(key) ?? default;

    /// <summary>Reads a font dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The font's name in the resources.</param>
    /// <param name="raw">The resources entry.</param>
    /// <param name="font">The font dictionary.</param>
    /// <param name="fonts">The document's font cache, or <see langword="null"/>.</param>
    /// <returns>The font; <see langword="null"/> when it cannot be written from text.</returns>
    private static FormFont? FromDictionary(PdfObjectStore store, string name, PdfValue raw, PdfDictionary font, PdfFontCache? fonts)
    {
        var composite = font.IsName(KnownName.Subtype, KnownName.Type0);
        var loaded = fonts?.Get(font);
        var map = loaded is null || fonts is null ? null : fonts.GetCodeMap(loaded, store.Names);
        if (composite && map is null)
        {
            return null;
        }

        return composite ? FromComposite(name, raw, loaded!, map!) : FromSimple(store, name, raw, font, loaded, map);
    }

    /// <summary>Measures a Type0 font from the loaded font.</summary>
    /// <param name="name">The font's name in the resources.</param>
    /// <param name="raw">The resources entry.</param>
    /// <param name="loaded">The loaded font.</param>
    /// <param name="map">The font's codes.</param>
    /// <returns>The font.</returns>
    private static FormFont FromComposite(string name, PdfValue raw, PdfFont loaded, FormCodeMap map)
    {
        var ascent = loaded.Ascent * GlyphUnits;
        var descent = loaded.Descent * GlyphUnits;
        return new(name, raw, new float[CodeCount], new(ascent, descent <= 0 ? descent : -descent, ascent - descent), loaded, map);
    }

    /// <summary>Measures a simple font from its dictionary, and from the loaded font when there is one.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The font's name in the resources.</param>
    /// <param name="raw">The resources entry.</param>
    /// <param name="font">The font dictionary.</param>
    /// <param name="loaded">The loaded font, or <see langword="null"/>.</param>
    /// <param name="map">The font's codes, or <see langword="null"/>.</param>
    /// <returns>The font.</returns>
    private static FormFont FromSimple(PdfObjectStore store, string name, PdfValue raw, PdfDictionary font, PdfFont? loaded, FormCodeMap? map)
    {
        var baseName = store.Names.GetSpelling(font.GetName(KnownName.BaseFont));
        var metrics = StandardFonts.TryGet(baseName, out var found) ? found : StandardFonts.Get(StandardFont.Helvetica);
        var widths = BuildStandardWidths(metrics);
        ApplyWidths(font, widths);
        var descriptor = font.GetDictionary(KnownName.FontDescriptor);
        var ascent = descriptor is not null && descriptor.ContainsKey(KnownName.Ascent) ? descriptor.GetSingle(KnownName.Ascent) : metrics.Ascent;
        var descent = descriptor is not null && descriptor.ContainsKey(KnownName.Descent) ? descriptor.GetSingle(KnownName.Descent) : metrics.Descent;
        var lineHeight = descriptor is not null && descriptor.TryGetRectangle(KnownName.FontBBox, out var box) && box.Height > 0 ? box.Height : LineHeightOf(metrics);
        return new(name, raw, widths, new(ascent, descent <= 0 ? descent : -descent, lineHeight), loaded, map);
    }

    /// <summary>Gets the line height of a standard font from its bounding box.</summary>
    /// <param name="metrics">The font's metrics.</param>
    /// <returns>The height in thousandths of an em.</returns>
    private static float LineHeightOf(StandardFontMetrics metrics) => metrics.BoundingBox.Height > 0 ? metrics.BoundingBox.Height : GlyphUnits;

    /// <summary>Reads the widths of each code from a standard font's metrics.</summary>
    /// <param name="metrics">The font's metrics.</param>
    /// <returns>256 widths.</returns>
    private static float[] BuildStandardWidths(StandardFontMetrics metrics)
    {
        var widths = new float[CodeCount];
        for (var code = 0; code < widths.Length; code++)
        {
            widths[code] = metrics.TryGetWidth(FontEncodings.GetGlyphName(FontEncoding.WinAnsi, code), out var width) ? width : DefaultWidth;
        }

        return widths;
    }

    /// <summary>Overwrites the widths with the font's own <c>/Widths</c>.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="widths">The widths, changed in place.</param>
    private static void ApplyWidths(PdfDictionary font, float[] widths)
    {
        if (font.GetArray(KnownName.Widths) is not { } listed)
        {
            return;
        }

        var first = font.GetInt32(KnownName.FirstChar);
        var missing = font.GetDictionary(KnownName.FontDescriptor)?.GetSingle(KnownName.MissingWidth) ?? 0;
        for (var code = 0; code < widths.Length; code++)
        {
            var slot = code - first;
            if ((uint)slot < (uint)listed.Count)
            {
                widths[code] = listed.GetSingle(slot);
            }
            else if (font.ContainsKey(KnownName.FirstChar) || missing > 0)
            {
                widths[code] = missing;
            }
        }
    }

    /// <summary>Splits text into characters, treating a carriage return and the line feed after it as one line break.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The characters.</returns>
    private static List<Rune> SplitRunes(string text)
    {
        var runes = new List<Rune>(text.Length);
        var previousReturn = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!(rune.Value == '\n' && previousReturn))
            {
                runes.Add(rune);
            }

            previousReturn = rune.Value == '\r';
        }

        return runes;
    }

    /// <summary>Works out whether a character is a line break, a space or an ordinary character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The kind.</returns>
    private static byte KindOf(Rune rune)
    {
        if (rune.Value is '\r' or '\n')
        {
            return FormCodes.LineFeed;
        }

        return rune.Value is ' ' or '\t' ? FormCodes.Space : FormCodes.Ordinary;
    }

    /// <summary>Writes one code, high byte first.</summary>
    /// <param name="bytes">The encoded bytes.</param>
    /// <param name="offset">Where the code goes.</param>
    /// <param name="code">The code and its length.</param>
    private static void WriteUnit(byte[] bytes, int offset, FormCode code)
    {
        for (var i = 0; i < code.Length; i++)
        {
            bytes[offset + code.Length - 1 - i] = (byte)(code.Value >> (i * BitsPerByte));
        }
    }

    /// <summary>Finds the code a character is shown with.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The code; a question mark, or the font's first code, when the font has no such character.</returns>
    private FormCode GetCode(Rune rune)
    {
        if (_codeMap is null)
        {
            return new(rune.IsBmp ? FormEncoding.EncodeCharacter((char)rune.Value) : '?', 1);
        }

        if (_codeMap.TryGetCode(rune, out var code))
        {
            return code;
        }

        return _codeMap.TryGetCode(new('?'), out var question) ? question : new(0, Math.Max(1, _codeMap.UnitBytes));
    }

    /// <summary>Gets the width of a code.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The width in thousandths of an em.</returns>
    private float GetCodeWidth(int code) => _font is null ? _widths[code & (CodeCount - 1)] : _font.GetWidth(code) * GlyphUnits;
}
