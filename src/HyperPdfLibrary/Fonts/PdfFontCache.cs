// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Fonts;

/// <summary>Loads each font dictionary of a document once with its configured factory.</summary>
internal sealed class PdfFontCache
{
    /// <summary>The number of standard font values, including none.</summary>
    private const int StandardFormFontSlots = 16;

    /// <summary>The ways a standard field font is kept: as itself and as the stand-in for a missing font.</summary>
    private const int FallbackVariants = 2;

    /// <summary>The number of idle encoded-text buffers kept per document.</summary>
    private const int FormCodeSlots = 16;

    /// <summary>The number of idle rich-text layouts kept per document.</summary>
    private const int RichBlockSlots = 4;

    /// <summary>The factory selected when the document was opened.</summary>
    private readonly Func<PdfDictionary, PdfFont?>? _factory;

    /// <summary>The loaded fonts by dictionary.</summary>
    private readonly ObjectCache<PdfDictionary, PdfFont> _fonts = new();

    /// <summary>The form-encoding maps of fonts used by this cache, created only when a form needs one.</summary>
    private ObjectCache<PdfFont, FormCodeMap>? _formCodeMaps;

    /// <summary>The field fonts made from font dictionaries, created only when a form needs one.</summary>
    private ObjectCache<FormFontKey, FormFont>? _formFonts;

    /// <summary>The standard 14 field fonts, by font and whether it stands in for a missing font; created only when a form needs one.</summary>
    private FormFont?[]? _standardFormFonts;

    /// <summary>The document's reusable encoded-text buffers.</summary>
    private FormCodes?[]? _formCodes;

    /// <summary>The document's reusable rich-text layouts.</summary>
    private RichBlock?[]? _richBlocks;

    /// <summary>The document's standard font width tables.</summary>
    private float[]?[]? _standardWidths;

    /// <summary>The most recently parsed rich-text value, bounded by its source size.</summary>
    private RichTextSnapshot? _richText;

    /// <summary>The most recently used combination of appearance fonts and an optional icon.</summary>
    private FormResourceSet? _appearanceResources;

    /// <summary>Initializes a new instance of the <see cref="PdfFontCache"/> class.</summary>
    /// <param name="factory">The document's font loader.</param>
    internal PdfFontCache(Func<PdfDictionary, PdfFont?>? factory) => _factory = factory;

    /// <summary>Gets a font.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> when no factory is set or it cannot load the font.</returns>
    internal PdfFont? Get(PdfDictionary dictionary) => _factory is null ? null : _fonts.GetOrCreate(dictionary, _factory, Load);

    /// <summary>Gets the writable character codes of a loaded form font.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="names">The document's names.</param>
    /// <returns>The map, or <see langword="null"/> when the cache cannot make one.</returns>
    internal FormCodeMap? GetCodeMap(PdfFont font, PdfNameTable names)
    {
        var maps = Volatile.Read(ref _formCodeMaps);
        if (maps is null)
        {
            var created = new ObjectCache<PdfFont, FormCodeMap>();
            maps = Interlocked.CompareExchange(ref _formCodeMaps, created, null) ?? created;
        }

        return maps.GetOrCreate(font, names, FormCodeMap.Create);
    }

    /// <summary>Gets the field font made for a font dictionary, making it the first time.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="request">What is needed to make the font.</param>
    /// <returns>The font; <see langword="null"/> when it cannot be written from text.</returns>
    internal FormFont? GetFormFont(PdfDictionary font, in FormFontRequest request)
    {
        var fonts = Volatile.Read(ref _formFonts);
        if (fonts is null)
        {
            var created = new ObjectCache<FormFontKey, FormFont>();
            fonts = Interlocked.CompareExchange(ref _formFonts, created, null) ?? created;
        }

        return fonts.GetOrCreate(new(font, request.Key), request, static (key, state) => FormFont.Make(state, key.Dictionary));
    }

    /// <summary>Gets a standard 14 field font, making it the first time.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="font">The standard font.</param>
    /// <param name="name">The name in the resources' <c>/Font</c> dictionary.</param>
    /// <param name="isFallback">Whether the font stands in for one the document does not have.</param>
    /// <returns>The font.</returns>
    internal FormFont GetStandardFormFont(PdfObjectStore store, StandardFont font, string name, bool isFallback)
    {
        var slots = Volatile.Read(ref _standardFormFonts);
        if (slots is null)
        {
            var created = new FormFont?[StandardFormFontSlots * FallbackVariants];
            slots = Interlocked.CompareExchange(ref _standardFormFonts, created, null) ?? created;
        }

        var slot = (((int)font & (StandardFormFontSlots - 1)) * FallbackVariants) + (isFallback ? 1 : 0);
        if (Volatile.Read(ref slots[slot]) is { } existing && string.Equals(existing.Name, name, StringComparison.Ordinal))
        {
            return existing;
        }

        var made = FormFont.MakeStandard(store, font, name, isFallback, this);
        var published = Interlocked.CompareExchange(ref slots[slot], made, null);
        return published is not null && string.Equals(published.Name, name, StringComparison.Ordinal) ? published : made;
    }

    /// <summary>Rents encoded-text buffers from this document.</summary>
    /// <returns>The buffers, returned by disposal.</returns>
    internal FormCodes RentFormCodes()
    {
        var slots = Volatile.Read(ref _formCodes);
        if (slots is null)
        {
            var created = new FormCodes?[FormCodeSlots];
            slots = Interlocked.CompareExchange(ref _formCodes, created, null) ?? created;
        }

        return FormCodes.Rent(slots);
    }

    /// <summary>Rents a rich-text layout from this document.</summary>
    /// <param name="runs">The number of runs to lay out.</param>
    /// <returns>The layout, returned by disposal.</returns>
    internal RichBlock RentRichBlock(int runs)
    {
        var slots = Volatile.Read(ref _richBlocks);
        if (slots is null)
        {
            var created = new RichBlock?[RichBlockSlots];
            slots = Interlocked.CompareExchange(ref _richBlocks, created, null) ?? created;
        }

        return RichBlock.Rent(runs, slots);
    }

    /// <summary>Gets the standard font widths held by this document.</summary>
    /// <param name="metrics">The standard font's metrics.</param>
    /// <returns>The immutable width table.</returns>
    internal float[] GetStandardWidths(StandardFontMetrics metrics)
    {
        var slots = Volatile.Read(ref _standardWidths);
        if (slots is null)
        {
            var created = new float[]?[StandardFormFontSlots];
            slots = Interlocked.CompareExchange(ref _standardWidths, created, null) ?? created;
        }

        var slot = (int)metrics.Font & (StandardFormFontSlots - 1);
        if (Volatile.Read(ref slots[slot]) is { } existing)
        {
            return existing;
        }

        var widths = FormFont.CreateStandardWidths(metrics);
        return Interlocked.CompareExchange(ref slots[slot], widths, null) ?? widths;
    }

    /// <summary>Copies the last parsed rich-text value when its XML and default style are unchanged.</summary>
    /// <param name="source">The XML bytes.</param>
    /// <param name="style">The default style bytes.</param>
    /// <param name="text">Receives the parsed text.</param>
    /// <returns>Whether the text was present.</returns>
    internal bool TryReadRichText(ReadOnlySpan<byte> source, ReadOnlySpan<byte> style, ref RichText text) =>
        Volatile.Read(ref _richText)?.TryCopy(source, style, ref text) ?? false;

    /// <summary>Keeps a bounded parsed rich-text value for repeated appearance generation.</summary>
    /// <param name="source">The XML bytes.</param>
    /// <param name="style">The default style bytes.</param>
    /// <param name="text">The parsed text.</param>
    internal void KeepRichText(ReadOnlySpan<byte> source, ReadOnlySpan<byte> style, ref RichText text)
    {
        const int maxBytes = 64 * 1024;
        const int maxItems = 4096;
        if (source.Length <= maxBytes && style.Length <= maxBytes && text.CharCount <= maxBytes && text.RunCount <= maxItems && text.ParagraphCount <= maxItems)
        {
            Volatile.Write(ref _richText, new(source, style, ref text));
        }
    }

    /// <summary>Gets shared resources for a bounded combination of fonts and an optional icon.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fonts">The fonts.</param>
    /// <param name="icon">The icon, or no value.</param>
    /// <returns>The appearance resources.</returns>
    internal PdfDictionary GetAppearanceResources(PdfObjectStore store, ReadOnlySpan<FormFont> fonts, PdfValue icon)
    {
        if (Volatile.Read(ref _appearanceResources) is { } existing && existing.Matches(fonts, icon))
        {
            return existing.Resources;
        }

        if (fonts.Length > StandardFormFontSlots)
        {
            return FormAppearanceResources.CreateResources(store, fonts, icon);
        }

        var created = new FormResourceSet(store, fonts, icon);
        Volatile.Write(ref _appearanceResources, created);
        return created.Resources;
    }

    /// <summary>Forgets every loaded font. Renders that already hold a font keep using it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear()
    {
        _fonts.Clear();
        Volatile.Read(ref _formCodeMaps)?.Clear();
        Volatile.Read(ref _formFonts)?.Clear();
        _ = Interlocked.Exchange(ref _standardFormFonts, null);
        _ = Interlocked.Exchange(ref _formCodes, null);
        _ = Interlocked.Exchange(ref _richBlocks, null);
        _ = Interlocked.Exchange(ref _standardWidths, null);
        _ = Interlocked.Exchange(ref _richText, null);
        _ = Interlocked.Exchange(ref _appearanceResources, null);
    }

    /// <summary>Loads a font, treating a damaged font as missing.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="load">The factory.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private static PdfFont? Load(PdfDictionary dictionary, Func<PdfDictionary, PdfFont?> load)
    {
        try
        {
            return load(dictionary);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
