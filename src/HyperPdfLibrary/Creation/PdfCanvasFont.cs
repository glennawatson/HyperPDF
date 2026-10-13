// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
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
    private PdfCanvasFont(PdfEmbeddedFont font)
    {
        _embedded = font;
        Id = font.Id;
    }

    /// <summary>Initializes a new instance of the <see cref="PdfCanvasFont"/> class for a built-in font.</summary>
    /// <param name="font">The font.</param>
    /// <param name="id">The font dictionary's object.</param>
    private PdfCanvasFont(AppearanceFont font, PdfObjectId id)
    {
        _builtIn = font;
        Id = id;
    }

    /// <summary>Gets the object id of the font dictionary.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets a value indicating whether the font program is embedded in the file.</summary>
    public bool IsEmbedded => _embedded is not null;

    /// <summary>Gets the font's PostScript name.</summary>
    public string Name => _embedded?.Source.PostScriptName ?? System.Text.Encoding.ASCII.GetString(AppearanceFontMetrics.GetBaseFontName(_builtIn));

    /// <summary>Wraps an embedded font.</summary>
    /// <param name="font">The font, created in the document that will hold the page.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="font"/> is <see langword="null"/>.</exception>
    public static PdfCanvasFont FromEmbedded(PdfEmbeddedFont font)
    {
        ArgumentNullException.ThrowIfNull(font);
        return new(font);
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
        return new(font, sink.Add(PdfValue.FromDictionary(dictionary)));
    }

    /// <summary>Measures how far text advances.</summary>
    /// <param name="text">The text.</param>
    /// <param name="size">The font size in points.</param>
    /// <returns>The advance in points.</returns>
    public float Measure(ReadOnlySpan<char> text, float size)
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

    /// <summary>Gets how far the font rises above the baseline.</summary>
    /// <param name="size">The font size in points.</param>
    /// <returns>The ascent in points.</returns>
    public float GetAscent(float size) => _embedded is { } font ? font.Source.Ascent * size / Thousand : AppearanceFontMetrics.GetAscent(_builtIn) * size;

    /// <summary>Gets how far the font falls below the baseline.</summary>
    /// <param name="size">The font size in points.</param>
    /// <returns>The descent in points, as a negative number.</returns>
    public float GetDescent(float size) => _embedded is { } font ? font.Source.Descent * size / Thousand : AppearanceFontMetrics.GetDescent(_builtIn) * size;

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
}
