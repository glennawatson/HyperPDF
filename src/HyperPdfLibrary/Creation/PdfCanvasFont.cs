// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// A font a page can draw text with: either an embedded font, whose glyphs are cut down to the ones used when the document
/// is finished, or one of the 14 built-in fonts, which a viewer supplies. Text in a built-in font is limited to Windows
/// ANSI characters, and a file that uses one does not meet PDF/A or PDF/UA, which need every font embedded.
/// </summary>
[DebuggerDisplay("PdfCanvasFont: {Name}, embedded {IsEmbedded}")]
public sealed class PdfCanvasFont
{
    /// <summary>The number of font units in an em for the metrics of <see cref="PdfFontSource"/>.</summary>
    private const float Thousand = 1000F;

    /// <summary>The embedded font, or <see langword="null"/> for a built-in one.</summary>
    private readonly PdfEmbeddedFont? _embedded;

    /// <summary>The built-in font.</summary>
    private readonly AppearanceFont _builtIn;

    /// <summary>Initializes a new instance of the <see cref="PdfCanvasFont"/> class for an embedded font.</summary>
    /// <param name="font">The font.</param>
    /// <param name="fallbacks">The ordered fallback fonts.</param>
    /// <param name="fallbackHost">The document creator providing lazy default fallback fonts, if enabled.</param>
    private PdfCanvasFont(PdfEmbeddedFont font, IReadOnlyList<PdfCanvasFont> fallbacks, PdfDocumentCreator? fallbackHost)
    {
        _embedded = font;
        Id = font.Id;
        Fallbacks = fallbacks;
        FallbackHost = fallbackHost;
    }

    /// <summary>Initializes a new instance of the <see cref="PdfCanvasFont"/> class for a built-in font.</summary>
    /// <param name="font">The font.</param>
    /// <param name="id">The font dictionary's object.</param>
    /// <param name="fallbacks">The ordered fallback fonts.</param>
    /// <param name="fallbackHost">The document creator providing lazy default fallback fonts, if enabled.</param>
    private PdfCanvasFont(AppearanceFont font, PdfObjectId id, IReadOnlyList<PdfCanvasFont> fallbacks, PdfDocumentCreator? fallbackHost)
    {
        _builtIn = font;
        Id = id;
        Fallbacks = fallbacks;
        FallbackHost = fallbackHost;
    }

    /// <summary>Gets the object id of the font dictionary.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets a value indicating whether the font program is embedded in the file.</summary>
    public bool IsEmbedded => _embedded is not null;

    /// <summary>Gets the font's PostScript name.</summary>
    public string Name => _embedded?.Source.PostScriptName ?? Encoding.ASCII.GetString(AppearanceFontMetrics.GetBaseFontName(_builtIn));

    /// <summary>Gets the ordered fallback fonts consulted when this font lacks a glyph for a character or cluster.</summary>
    public IReadOnlyList<PdfCanvasFont> Fallbacks { get; }

    /// <summary>Gets the document creator that supplies lazy default backup fonts when a glyph is still missing after <see cref="Fallbacks"/>.</summary>
    internal PdfDocumentCreator? FallbackHost { get; }

    /// <summary>Wraps an embedded font.</summary>
    /// <param name="font">The font, created in the document that will hold the page.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    public static PdfCanvasFont FromEmbedded(PdfEmbeddedFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        return new(font, [], fallbackHost: null);
    }

    /// <summary>Adds a built-in font to a document.</summary>
    /// <param name="sink">The document the font dictionary is added to.</param>
    /// <param name="font">The built-in font to use.</param>
    /// <returns>The font, ready to draw with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public static PdfCanvasFont FromBuiltIn(IPdfObjectSink sink, AppearanceFont font)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var dictionary = new PdfDictionary(sink.Owner);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Font));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Type1));
        dictionary.Set(KnownName.BaseFont, PdfValue.FromName(sink.Names.Intern(AppearanceFontMetrics.GetBaseFontName(font))));
        dictionary.Set(KnownName.Encoding, PdfValue.FromName(KnownName.WinAnsiEncoding));
        return new(font, sink.Add(PdfValue.FromDictionary(dictionary)), [], fallbackHost: null);
    }

    /// <summary>Returns a view of this font that consults <paramref name="fallbacks"/> in order whenever a glyph is missing from this font.</summary>
    /// <param name="fallbacks">The ordered fallback fonts to consult.</param>
    /// <returns>A <see cref="PdfCanvasFont"/> sharing the same underlying PDF font object and configured with <paramref name="fallbacks"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fallbacks"/> is <see langword="null"/>.</exception>
    public PdfCanvasFont WithFallbacks(IReadOnlyList<PdfCanvasFont> fallbacks)
    {
        ArgumentNullException.ThrowIfNull(fallbacks);
        if (fallbacks.Count == 0)
        {
            return CloneWith([], FallbackHost);
        }

        var copy = new PdfCanvasFont[fallbacks.Count];
        for (var i = 0; i < fallbacks.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(fallbacks[i]);
            copy[i] = fallbacks[i];
        }

        return CloneWith(copy, FallbackHost);
    }

    /// <summary>Returns a view of this font that consults <paramref name="fallbacks"/> in order whenever a glyph is missing from this font.</summary>
    /// <param name="fallbacks">The ordered fallback fonts to consult.</param>
    /// <returns>A <see cref="PdfCanvasFont"/> sharing the same underlying PDF font object and configured with <paramref name="fallbacks"/>.</returns>
    public PdfCanvasFont WithFallbacks(params ReadOnlySpan<PdfCanvasFont> fallbacks)
    {
        if (fallbacks.IsEmpty)
        {
            return CloneWith([], FallbackHost);
        }

        var copy = new PdfCanvasFont[fallbacks.Length];
        for (var i = 0; i < fallbacks.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(fallbacks[i]);
            copy[i] = fallbacks[i];
        }

        return CloneWith(copy, FallbackHost);
    }

    /// <summary>Returns a view of this font with document-level default backup fonts disabled, so only this font and its explicit <see cref="Fallbacks"/> are consulted.</summary>
    /// <returns>A <see cref="PdfCanvasFont"/> without automatic default backup fonts.</returns>
    public PdfCanvasFont WithoutDefaultFallbacks() =>
        FallbackHost is null ? this : CloneWith(Fallbacks, fallbackHost: null);

    /// <summary>Determines whether this font itself contains a glyph for <paramref name="codePoint"/> (excluding <c>.notdef</c>).</summary>
    /// <param name="codePoint">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when this font maps <paramref name="codePoint"/> to a non-zero glyph.</returns>
    public bool HasGlyph(int codePoint) =>
        _embedded is not null
            ? _embedded.HasGlyph(codePoint)
            : AppearanceFontMetrics.TryEncode(codePoint, out var code)
              && AppearanceFontMetrics.GetAdvance(_builtIn, code) > 0F;

    /// <summary>Determines whether this font itself contains a glyph for <paramref name="rune"/> (excluding <c>.notdef</c>).</summary>
    /// <param name="rune">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when this font maps <paramref name="rune"/> to a non-zero glyph.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasGlyph(Rune rune) => HasGlyph(rune.Value);

    /// <summary>Splits <paramref name="text"/> into contiguous runs resolved against this font, its <see cref="Fallbacks"/>, and any document-level default backup fonts.</summary>
    /// <param name="text">The text to split.</param>
    /// <param name="runs">The list to populate with resolved runs (cleared before adding).</param>
    /// <exception cref="ArgumentNullException"><paramref name="runs"/> is <see langword="null"/>.</exception>
    public void SplitRuns(ReadOnlySpan<char> text, List<PdfFontFallbackRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);
        FontFallbackResolver.SplitRuns(this, text, runs);
    }

    /// <summary>Splits <paramref name="text"/> into contiguous runs resolved against this font, its <see cref="Fallbacks"/>, and any document-level default backup fonts.</summary>
    /// <param name="text">The text to split.</param>
    /// <returns>The resolved font runs in order.</returns>
    public IReadOnlyList<PdfFontFallbackRun> SplitRuns(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return [];
        }

        var runs = new List<PdfFontFallbackRun>();
        FontFallbackResolver.SplitRuns(this, text, runs);
        return runs;
    }

    /// <summary>Measures how far text advances across this font and any resolved fallback runs.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The font size in points.</param>
    /// <returns>The advance in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Measure(ReadOnlySpan<char> text, float size) => FontFallbackResolver.Measure(this, text, size);

    /// <summary>Gets how far the font rises above the baseline.</summary>
    /// <param name="size">The font size in points.</param>
    /// <returns>The ascent in points.</returns>
    public float GetAscent(float size) => _embedded is { } font ? font.Source.Ascent * size / Thousand : AppearanceFontMetrics.GetAscent(_builtIn) * size;

    /// <summary>Gets how far the font falls below the baseline.</summary>
    /// <param name="size">The font size in points.</param>
    /// <returns>The descent in points, as a negative number.</returns>
    public float GetDescent(float size) => _embedded is { } font ? font.Source.Descent * size / Thousand : AppearanceFontMetrics.GetDescent(_builtIn) * size;

    /// <summary>Returns a copy of this font attached to <paramref name="fallbackHost"/> for lazy default fallback resolution.</summary>
    /// <param name="fallbackHost">The document creator hosting default backup fonts.</param>
    /// <returns>A <see cref="PdfCanvasFont"/> linked to <paramref name="fallbackHost"/>.</returns>
    internal PdfCanvasFont WithFallbackHost(PdfDocumentCreator? fallbackHost) =>
        ReferenceEquals(FallbackHost, fallbackHost) ? this : CloneWith(Fallbacks, fallbackHost);

    /// <summary>Measures how far text advances using only this primary font (without consulting fallback fonts).</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The font size in points.</param>
    /// <returns>The advance in points.</returns>
    internal float MeasurePrimary(ReadOnlySpan<char> text, float size)
    {
        if (_embedded is not null)
        {
            return _embedded.Measure(text, size);
        }

        var codes = ArrayPool<byte>.Shared.Rent(text.Length);
        try
        {
            var count = AppearanceFontMetrics.Encode(text, codes);
            return AppearanceFontMetrics.MeasureAdvance(_builtIn, codes.AsSpan(0, count), size);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(codes);
        }
    }

    /// <summary>Writes text into a content stream, inside a text object with this font selected.</summary>
    /// <param name="builder">The content stream.</param>
    /// <param name="text">The text, without line breaks.</param>
    /// <param name="size">The font size selected with <c>Tf</c>.</param>
    internal void Show(ref PdfContentBuilder builder, ReadOnlySpan<char> text, float size)
    {
        if (_embedded is not null)
        {
            _embedded.ShowText(ref builder, text, size);
            return;
        }

        var codes = ArrayPool<byte>.Shared.Rent(text.Length);
        try
        {
            var count = AppearanceFontMetrics.Encode(text, codes);
            builder.ShowText(codes.AsSpan(0, count));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(codes);
        }
    }

    /// <summary>Creates a new <see cref="PdfCanvasFont"/> wrapping the same underlying font with updated fallback configuration.</summary>
    /// <param name="fallbacks">The ordered fallback fonts.</param>
    /// <param name="fallbackHost">The document creator providing lazy default fallback fonts.</param>
    /// <returns>The cloned <see cref="PdfCanvasFont"/>.</returns>
    private PdfCanvasFont CloneWith(IReadOnlyList<PdfCanvasFont> fallbacks, PdfDocumentCreator? fallbackHost) =>
        _embedded is not null
            ? new(_embedded, fallbacks, fallbackHost)
            : new(_builtIn, Id, fallbacks, fallbackHost);
}
