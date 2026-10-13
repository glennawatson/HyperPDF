// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>The font a signature appearance draws its text with: a built-in font or a simple font already in the document.</summary>
[DebuggerDisplay("PdfSignatureFont: {_builtIn}")]
internal sealed class PdfSignatureFont
{
    /// <summary>The number of codes of a simple font.</summary>
    private const int SimpleFontCodes = 256;

    /// <summary>The code shown for a character the font cannot show.</summary>
    private const byte Fallback = (byte)'?';

    /// <summary>The built-in font, used when there is no document font.</summary>
    private readonly AppearanceFont _builtIn;

    /// <summary>The document's font, or <see langword="null"/>.</summary>
    private readonly PdfSimpleFont? _document;

    /// <summary>The code of each character of the document's font.</summary>
    private readonly Dictionary<char, byte>? _codes;

    /// <summary>The font reference, or the font dictionary of a built-in font.</summary>
    private readonly PdfValue _value;

    /// <summary>Initializes a new instance of the <see cref="PdfSignatureFont"/> class for a built-in font.</summary>
    /// <param name="font">The font.</param>
    /// <param name="dictionary">The font dictionary.</param>
    private PdfSignatureFont(AppearanceFont font, PdfDictionary dictionary)
    {
        _builtIn = font;
        _value = PdfValue.FromDictionary(dictionary);
    }

    /// <summary>Initializes a new instance of the <see cref="PdfSignatureFont"/> class for a font in the document.</summary>
    /// <param name="font">The loaded font.</param>
    /// <param name="reference">The font's reference.</param>
    private PdfSignatureFont(PdfSimpleFont font, PdfObjectId reference)
    {
        _document = font;
        _value = PdfValue.FromReference(reference);
        _codes = MapCharacters(font);
    }

    /// <summary>Gets the value to store under the font's name in the resources.</summary>
    internal PdfValue Value => _value;

    /// <summary>Gets the distance from the baseline to the top of the tallest glyph, per unit of font size.</summary>
    internal float Ascent => _document?.Ascent ?? AppearanceFontMetrics.GetAscent(_builtIn);

    /// <summary>Gets the distance from the baseline to the bottom of the lowest glyph, as a positive number, per unit of font size.</summary>
    internal float Descent => _document is null ? AppearanceFontMetrics.GetDescent(_builtIn) : -_document.Descent;

    /// <summary>Opens the font a signature appearance asks for.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="appearance">The appearance.</param>
    /// <returns>The font.</returns>
    /// <exception cref="ArgumentException">The document font is missing or is not a simple font.</exception>
    internal static PdfSignatureFont Open(PdfObjectStore store, PdfSignatureAppearance appearance)
    {
        if (!appearance.DocumentFont.IsValid)
        {
            return new(appearance.Font, AppearanceFontMetrics.CreateFontDictionary(store, appearance.Font));
        }

        var dictionary = StoreReading.GetDictionary(store, appearance.DocumentFont);
        if (dictionary is null || PdfFontLoader.Load(dictionary) is not PdfSimpleFont font)
        {
            throw new ArgumentException("The document font is missing or is not a simple (single byte) font.", nameof(appearance));
        }

        return new(font, appearance.DocumentFont);
    }

    /// <summary>Encodes text as the font's single-byte codes, showing a question mark for a character the font lacks.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The codes.</returns>
    internal byte[] Encode(string text)
    {
        var codes = new List<byte>(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            codes.Add(CodeOf(rune));
        }

        return [.. codes];
    }

    /// <summary>Measures text.</summary>
    /// <param name="codes">The codes.</param>
    /// <param name="size">The font size.</param>
    /// <returns>The width in points.</returns>
    internal float Measure(ReadOnlySpan<byte> codes, float size)
    {
        if (_document is null)
        {
            return AppearanceFontMetrics.MeasureAdvance(_builtIn, codes, size);
        }

        var width = 0F;
        foreach (var code in codes)
        {
            width += _document.GetWidth(code);
        }

        return width * size;
    }

    /// <summary>Finds the code of each character a document font can show.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The codes by character.</returns>
    private static Dictionary<char, byte> MapCharacters(PdfSimpleFont font)
    {
        var codes = new Dictionary<char, byte>();
        var buffer = new char[PdfFont.MaxUnicodeLength];
        for (var code = 0; code < SimpleFontCodes; code++)
        {
            if (font.GetUnicode(code, buffer) == 1)
            {
                _ = codes.TryAdd(buffer[0], (byte)code);
            }
        }

        return codes;
    }

    /// <summary>Gets the code of a character.</summary>
    /// <param name="rune">The character.</param>
    /// <returns>The code, or a question mark when the font cannot show it.</returns>
    private byte CodeOf(System.Text.Rune rune)
    {
        if (_codes is not null)
        {
            return rune.IsBmp && _codes.TryGetValue((char)rune.Value, out var found) ? found : Fallback;
        }

        return AppearanceFontMetrics.TryEncode(rune.Value, out var code) ? code : Fallback;
    }
}
