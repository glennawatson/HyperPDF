// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
/// A font is made once per document and shared.
/// </summary>
[DebuggerDisplay("FormFont: {Name}")]
internal sealed class FormFont
{
    /// <summary>The name used for the built-in font.</summary>
    internal const string FallbackName = "Helv";

    /// <summary>The number of codes in a simple font.</summary>
    private const int CodeCount = 256;

    /// <summary>The width, in thousandths of an em, of a code no table gives a width for.</summary>
    private const float DefaultWidth = 500;

    /// <summary>One thousand: the units of a glyph width.</summary>
    private const float GlyphUnits = 1000;

    /// <summary>The widths of a font that is measured by its loaded font.</summary>
    private static readonly float[] NoWidths = [];

    /// <summary>The widths of each code in thousandths of an em, for the built-in encoding.</summary>
    private readonly float[] _widths;

    /// <summary>The loaded font that measures and encodes text; <see langword="null"/> for the built-in encoding.</summary>
    private readonly PdfFont? _font;

    /// <summary>The codes of the loaded font; <see langword="null"/> for the built-in encoding.</summary>
    private readonly FormCodeMap? _codeMap;

    /// <summary>The resources of an appearance that uses only this font; made on first use.</summary>
    private PdfDictionary? _resources;

    /// <summary>Initializes a new instance of the <see cref="FormFont"/> class.</summary>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="key">The interned name.</param>
    /// <param name="resource">The value to store under that name: a reference or a dictionary.</param>
    /// <param name="widths">The widths of the 256 codes of the built-in encoding.</param>
    /// <param name="metrics">The ascent, descent and line height.</param>
    /// <param name="font">The loaded font, or <see langword="null"/> to use the built-in encoding.</param>
    /// <param name="codeMap">The codes of the loaded font.</param>
    private FormFont(string name, PdfName key, PdfValue resource, float[] widths, FormFontMetrics metrics, PdfFont? font, FormCodeMap? codeMap)
    {
        Name = name;
        Key = key;
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

    /// <summary>Gets <see cref="Name"/> as an interned name.</summary>
    internal PdfName Key { get; }

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

    /// <summary>Gets the document-owned buffers and standard font measurements.</summary>
    private PdfFontCache? Cache { get; init; }

    /// <summary>Finds a field's font by the name its default appearance uses.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The font's name; empty to use the built-in font.</param>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="form">The AcroForm dictionary, or <see langword="null"/>.</param>
    /// <param name="pageResources">The page's resources, or <see langword="null"/>.</param>
    /// <param name="fonts">The document's font cache, or <see langword="null"/> to measure with the font's own widths and WinAnsi.</param>
    /// <returns>The font; Helvetica when the name is not found or the font cannot be used.</returns>
    internal static FormFont Find(PdfObjectStore store, ReadOnlySpan<byte> name, PdfDictionary widget, PdfDictionary? form, PdfDictionary? pageResources, PdfFontCache? fonts)
    {
        if (name.IsEmpty)
        {
            return CreateFallback(store, fonts);
        }

        var key = store.Names.Intern(name);
        var raw = LookUp(FieldAttributes.Find(widget, KnownName.DR).AsDictionary(), key);
        raw = raw.IsNull ? LookUp(form?.GetDictionary(KnownName.DR), key) : raw;
        raw = raw.IsNull ? LookUp(pageResources, key) : raw;
        return FromResource(new(store, key, raw, fonts)) ?? CreateFallback(store, fonts);
    }

    /// <summary>Creates the built-in Helvetica font.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateFallback(PdfObjectStore store) => CreateStandard(store, StandardFont.Helvetica, FallbackName, true);

    /// <summary>Gets the built-in Helvetica font, made once for the document when it has a font cache.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fonts">The document's font cache, or <see langword="null"/>.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateFallback(PdfObjectStore store, PdfFontCache? fonts) =>
        CreateStandard(store, StandardFont.Helvetica, FallbackName, true, fonts);

    /// <summary>Creates one of the standard 14 fonts, written in WinAnsi.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name to give it in the resources' <c>/Font</c> dictionary.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateStandard(PdfObjectStore store, StandardFont font, string name) => CreateStandard(store, font, name, false);

    /// <summary>Gets one of the standard 14 fonts, made once for the document when it has a font cache.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name to give it in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="fonts">The document's font cache, or <see langword="null"/>.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateStandard(PdfObjectStore store, StandardFont font, string name, PdfFontCache? fonts) =>
        CreateStandard(store, font, name, false, fonts);

    /// <summary>Makes a standard 14 font; the font cache calls this for a font it does not hold yet.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="isFallback">Whether the font stands in for one the document does not have.</param>
    /// <returns>The font.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FormFont CreateStandard(PdfObjectStore store, StandardFont font, string name, bool isFallback) =>
        MakeStandard(store, font, name, isFallback, null);

    /// <summary>Makes a standard font owned by a document's cache.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">Its resource name.</param>
    /// <param name="isFallback">Whether the font replaces a missing font.</param>
    /// <param name="cache">The owning cache, or null for an uncached operation.</param>
    /// <returns>The font.</returns>
    internal static FormFont MakeStandard(PdfObjectStore store, StandardFont font, string name, bool isFallback, PdfFontCache? cache)
    {
        var metrics = StandardFonts.Get(font);
        var dictionary = CreateStandardResource(store, font);
        var measures = new FormFontMetrics(metrics.Ascent, metrics.Descent, LineHeightOf(metrics));
        return new(name, store.Names.Intern(name), PdfValue.FromDictionary(dictionary), GetStandardWidths(metrics, cache), measures, null, null) { IsFallback = isFallback, Cache = cache };
    }

    /// <summary>Creates a standard font dictionary without allocating text layout state.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <returns>The font dictionary.</returns>
    internal static PdfDictionary CreateStandardResource(PdfObjectStore store, StandardFont font)
    {
        var dictionary = new PdfDictionary(store);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type1));
        dictionary.Set(KnownName.BaseFont, PdfValue.FromName(store.Names.Intern(StandardFonts.Get(font).Name)));
        dictionary.Set(KnownName.Encoding, PdfValue.FromName(KnownName.WinAnsiEncoding));
        return dictionary;
    }

    /// <summary>Reads a font dictionary.</summary>
    /// <param name="request">What is needed to make the font.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns>The font; <see langword="null"/> when it cannot be written from text.</returns>
    internal static FormFont? Make(in FormFontRequest request, PdfDictionary font)
    {
        var composite = font.IsName(KnownName.Subtype, KnownName.Type0);
        var loaded = request.Fonts?.Get(font);
        var map = loaded is null || request.Fonts is null ? null : request.Fonts.GetCodeMap(loaded, request.Store.Names);
        if (composite && map is null)
        {
            return null;
        }

        return composite ? FromComposite(request, loaded!, map!) : FromSimple(request, font, loaded, map);
    }

    /// <summary>Computes the WinAnsi widths of a standard font.</summary>
    /// <param name="metrics">The font's metrics.</param>
    /// <returns>256 widths.</returns>
    internal static float[] CreateStandardWidths(StandardFontMetrics metrics)
    {
        var widths = new float[CodeCount];
        for (var code = 0; code < widths.Length; code++)
        {
            widths[code] = metrics.TryGetWidth(FontEncodings.GetGlyphName(FontEncoding.WinAnsi, code), out var width) ? width : DefaultWidth;
        }

        return widths;
    }

    /// <summary>Encodes field text for the font; line breaks are kept as single line feeds.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels encoding and font code searches.</param>
    /// <returns>One unit per character; dispose it when the text has been used.</returns>
    internal FormCodes Encode(ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _codeMap?.Prepare(text, cancellationToken);
        var unitBytes = _codeMap?.UnitBytes ?? 1;
        var feed = new FormCode(FormEncoding.LineFeed, unitBytes == 0 ? 1 : unitBytes);
        var codes = Cache?.RentFormCodes() ?? FormCodes.Rent(null);
        try
        {
            codes.Begin(CountUnits(text, cancellationToken), unitBytes);
            EncodeUnits(codes, text, feed, cancellationToken);
            return codes;
        }
        catch
        {
            codes.Dispose();
            throw;
        }
    }

    /// <summary>Decodes and encodes PDF text without an intermediate string.</summary>
    /// <param name="bytes">The PDF text bytes.</param>
    /// <param name="cancellationToken">Cancels encoding.</param>
    /// <returns>The encoded units, disposed after use.</returns>
    internal FormCodes EncodePdfText(ReadOnlySpan<byte> bytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.IsEmpty)
        {
            return Encode(default, cancellationToken);
        }

        var characters = ArrayPool<char>.Shared.Rent(PdfText.GetDecodedLength(bytes));
        try
        {
            var count = PdfText.Decode(bytes, characters);
            return Encode(characters.AsSpan(0, count), cancellationToken);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(characters);
        }
    }

    /// <summary>Gets the resources of an appearance that uses only this font, made the first time and shared.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>A resources dictionary whose <c>/Font</c> holds this font.</returns>
    internal PdfDictionary GetResources(PdfObjectStore store)
    {
        if (Volatile.Read(ref _resources) is { } existing)
        {
            return existing;
        }

        var fonts = new PdfDictionary(store);
        fonts.Set(Key, Resource);
        var made = new PdfDictionary(store);
        made.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        return Interlocked.CompareExchange(ref _resources, made, null) ?? made;
    }

    /// <summary>Finds a font in a resources dictionary's <c>/Font</c> entry.</summary>
    /// <param name="resources">The resources, or <see langword="null"/>.</param>
    /// <param name="key">The font's name.</param>
    /// <returns>The unresolved entry; null when missing.</returns>
    private static PdfValue LookUp(PdfDictionary? resources, PdfName key) => resources?.GetDictionary(KnownName.Font)?.GetRaw(key) ?? default;

    /// <summary>Gets a standard 14 font made once for the document when it has a font cache.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="isFallback">Whether the font stands in for one the document does not have.</param>
    /// <param name="fonts">The document's font cache, or <see langword="null"/>.</param>
    /// <returns>The font.</returns>
    private static FormFont CreateStandard(PdfObjectStore store, StandardFont font, string name, bool isFallback, PdfFontCache? fonts) =>
        fonts is null ? CreateStandard(store, font, name, isFallback) : fonts.GetStandardFormFont(store, font, name, isFallback);

    /// <summary>Gets the font a resources entry names, made once for the document when it has a font cache.</summary>
    /// <param name="request">What is needed to make the font, with the resources entry as its resource.</param>
    /// <returns>The font; <see langword="null"/> when the entry is missing or the font cannot be written from text.</returns>
    private static FormFont? FromResource(in FormFontRequest request)
    {
        if (request.Resource.IsNull || StoreReading.Resolve(request.Store, request.Resource).AsDictionary() is not { } font)
        {
            return null;
        }

        return request.Fonts is null ? Make(request, font) : request.Fonts.GetFormFont(font, request);
    }

    /// <summary>Measures a Type0 font from the loaded font.</summary>
    /// <param name="request">What is needed to make the font.</param>
    /// <param name="loaded">The loaded font.</param>
    /// <param name="map">The font's codes.</param>
    /// <returns>The font.</returns>
    private static FormFont FromComposite(in FormFontRequest request, PdfFont loaded, FormCodeMap map)
    {
        var ascent = loaded.Ascent * GlyphUnits;
        var descent = loaded.Descent * GlyphUnits;
        var metrics = new FormFontMetrics(ascent, descent <= 0 ? descent : -descent, ascent - descent);
        return new(request.Store.Names.GetString(request.Key), request.Key, request.Resource, NoWidths, metrics, loaded, map) { Cache = request.Fonts };
    }

    /// <summary>Measures a simple font from its dictionary, and from the loaded font when there is one.</summary>
    /// <param name="request">What is needed to make the font.</param>
    /// <param name="font">The font dictionary.</param>
    /// <param name="loaded">The loaded font, or <see langword="null"/>.</param>
    /// <param name="map">The font's codes, or <see langword="null"/>.</param>
    /// <returns>The font.</returns>
    private static FormFont FromSimple(in FormFontRequest request, PdfDictionary font, PdfFont? loaded, FormCodeMap? map)
    {
        var store = request.Store;
        var metrics = StandardFonts.TryGet(store.Names.GetSpelling(font.GetName(KnownName.BaseFont)), out var found) ? found : StandardFonts.Get(StandardFont.Helvetica);
        var widths = GetStandardWidths(metrics, request.Fonts);
        if (font.GetArray(KnownName.Widths) is { } listed)
        {
            widths = ApplyWidths(font, listed, widths);
        }

        var vertical = ReadMetrics(font.GetDictionary(KnownName.FontDescriptor), metrics);
        return new(store.Names.GetString(request.Key), request.Key, request.Resource, widths, vertical, loaded, map) { Cache = request.Fonts };
    }

    /// <summary>Reads the vertical measurements from a font descriptor, taking a standard font's for what it lacks.</summary>
    /// <param name="descriptor">The font descriptor, or <see langword="null"/>.</param>
    /// <param name="metrics">The standard font whose measurements fill the gaps.</param>
    /// <returns>The measurements.</returns>
    private static FormFontMetrics ReadMetrics(PdfDictionary? descriptor, StandardFontMetrics metrics)
    {
        if (descriptor is null)
        {
            return new(metrics.Ascent, metrics.Descent <= 0 ? metrics.Descent : -metrics.Descent, LineHeightOf(metrics));
        }

        var ascent = descriptor.ContainsKey(KnownName.Ascent) ? descriptor.GetSingle(KnownName.Ascent) : metrics.Ascent;
        var descent = descriptor.ContainsKey(KnownName.Descent) ? descriptor.GetSingle(KnownName.Descent) : metrics.Descent;
        var lineHeight = descriptor.TryGetRectangle(KnownName.FontBBox, out var box) && box.Height > 0 ? box.Height : LineHeightOf(metrics);
        return new(ascent, descent <= 0 ? descent : -descent, lineHeight);
    }

    /// <summary>Gets the line height of a standard font from its bounding box.</summary>
    /// <param name="metrics">The font's metrics.</param>
    /// <returns>The height in thousandths of an em.</returns>
    private static float LineHeightOf(StandardFontMetrics metrics) => metrics.BoundingBox.Height > 0 ? metrics.BoundingBox.Height : GlyphUnits;

    /// <summary>Gets the widths of each code from the owning cache or computes uncached widths.</summary>
    /// <param name="metrics">The font's metrics.</param>
    /// <param name="cache">The owning cache, or null.</param>
    /// <returns>256 widths.</returns>
    private static float[] GetStandardWidths(StandardFontMetrics metrics, PdfFontCache? cache) =>
        cache?.GetStandardWidths(metrics) ?? CreateStandardWidths(metrics);

    /// <summary>Makes widths from the font's own <c>/Widths</c>, over a copy of the standard ones.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="listed">The font's <c>/Widths</c>.</param>
    /// <param name="standard">The standard widths, which are not changed.</param>
    /// <returns>The widths.</returns>
    private static float[] ApplyWidths(PdfDictionary font, PdfArray listed, float[] standard)
    {
        var widths = (float[])standard.Clone();
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

        return widths;
    }

    /// <summary>Counts the characters of text, treating a carriage return and the line feed after it as one line break.</summary>
    /// <param name="text">The text.</param>
    /// <param name="cancellationToken">Cancels counting characters.</param>
    /// <returns>The number of units.</returns>
    private static int CountUnits(ReadOnlySpan<char> text, CancellationToken cancellationToken)
    {
        var count = 0;
        var previousReturn = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if ((count & (CodeCount - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            count += rune.Value == '\n' && previousReturn ? 0 : 1;
            previousReturn = rune.Value == '\r';
        }

        return count;
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

    /// <summary>Writes the normalized characters into the rented units.</summary>
    /// <param name="codes">The units.</param>
    /// <param name="text">The text.</param>
    /// <param name="feed">The code used for a line break.</param>
    /// <param name="cancellationToken">Cancels encoding.</param>
    private void EncodeUnits(FormCodes codes, ReadOnlySpan<char> text, FormCode feed, CancellationToken cancellationToken)
    {
        var index = 0;
        var previousReturn = false;
        foreach (var rune in text.EnumerateRunes())
        {
            if ((index & (CodeCount - 1)) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var isBreakAfterReturn = rune.Value == '\n' && previousReturn;
            previousReturn = rune.Value == '\r';
            if (isBreakAfterReturn)
            {
                continue;
            }

            SetUnit(codes, index, rune, feed);
            index++;
        }
    }

    /// <summary>Encodes one character into a unit.</summary>
    /// <param name="codes">The units.</param>
    /// <param name="index">The unit to set.</param>
    /// <param name="rune">The character.</param>
    /// <param name="feed">The code of a line feed.</param>
    private void SetUnit(FormCodes codes, int index, Rune rune, FormCode feed)
    {
        var kind = KindOf(rune);
        var code = kind == FormCodes.LineFeed ? feed : GetCode(kind == FormCodes.Space ? new Rune(' ') : rune);
        codes.Set(index, code, kind == FormCodes.LineFeed ? 0 : GetCodeWidth(code.Value), kind);
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
