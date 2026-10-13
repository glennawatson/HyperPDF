// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.TextLayer;

/// <summary>
/// A built in Latin font with WinAnsi encoding, which needs no embedded font program. OCR text is usually
/// written in Helvetica.
/// </summary>
[DebuggerDisplay("PdfStandardTextLayerFont: {_font}")]
public sealed class PdfStandardTextLayerFont : IPdfTextLayerFont
{
    /// <summary>Thousandths of an em.</summary>
    private const float Thousand = 1000;

    /// <summary>The built in font.</summary>
    private readonly AppearanceFont _font;

    /// <summary>The font dictionary.</summary>
    private readonly PdfValue _resource;

    /// <summary>Initializes a new instance of the <see cref="PdfStandardTextLayerFont"/> class.</summary>
    /// <param name="store">The document the font belongs to.</param>
    /// <param name="font">The built in font.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public PdfStandardTextLayerFont(PdfObjectStore store, AppearanceFont font)
    {
        ArgumentNullException.ThrowIfNull(store);
        _font = font;
        _resource = PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(store, font));
    }

    /// <inheritdoc/>
    public PdfValue Resource => _resource;

    /// <inheritdoc/>
    public float Ascent => AppearanceFontMetrics.GetAscent(_font);

    /// <inheritdoc/>
    public float Descent => -AppearanceFontMetrics.GetDescent(_font);

    /// <summary>Determines whether the built in fonts can show every character of some text.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when they can.</returns>
    public static bool Covers(ReadOnlySpan<char> text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!AppearanceFontMetrics.TryEncode(rune.Value, out _))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public int Encode(ReadOnlySpan<char> text, Span<byte> codes, out PdfTextExtent extent)
    {
        var count = AppearanceFontMetrics.Encode(text, codes);
        var shown = codes[..count];
        var ink = AppearanceFontMetrics.MeasureInk(_font, shown, Thousand);
        extent = new(AppearanceFontMetrics.MeasureAdvance(_font, shown, Thousand), ink.Left, ink.Right);
        return count;
    }
}
