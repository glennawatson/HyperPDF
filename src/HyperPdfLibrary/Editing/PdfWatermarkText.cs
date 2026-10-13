// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>A line of text drawn as a watermark.</summary>
/// <param name="Text">The text.</param>
/// <param name="Style">The font, size and colour.</param>
[DebuggerDisplay("PdfWatermarkText: {Text}")]
internal sealed record PdfWatermarkText(string Text, PdfTextWatermarkStyle Style) : PdfWatermarkContent
{
    /// <summary>The number of codes in a simple font.</summary>
    private const int SimpleFontCodes = 256;

    /// <inheritdoc/>
    internal override bool HasTransparency => false;

    /// <inheritdoc/>
    internal override bool UsesUnembeddedFont => !Style.DocumentFont.IsValid;

    /// <summary>Gets the resource name of the font in the watermark's form.</summary>
    private static ReadOnlySpan<byte> FontName => "WmFont"u8;

    /// <inheritdoc/>
    internal override PdfWatermarkForm Build(PdfDocument document, PdfEditTransaction transaction, PdfObjectId group)
    {
        var store = document.Objects;
        var run = Style.DocumentFont.IsValid ? MeasureDocumentFont(store) : MeasureBuiltInFont();
        var fontReference = Style.DocumentFont.IsValid
            ? PdfValue.FromReference(Style.DocumentFont)
            : PdfValue.FromReference(transaction.Add(PdfValue.FromDictionary(AppearanceFontMetrics.CreateFontDictionary(store, Style.Font))));
        var height = run.Ascent - run.Descent;
        var builder = default(PdfContentBuilder);
        try
        {
            builder.BeginText();
            builder.SetFont(FontName, Style.FontSize);
            builder.SetFillRgb(Style.Red, Style.Green, Style.Blue);
            builder.MoveText(0, -run.Descent);
            builder.ShowText(run.Codes);
            builder.EndText();
            var fonts = new PdfDictionary(store);
            fonts.Set(store.Names.Intern(FontName), fontReference);
            var resources = new PdfDictionary(store);
            resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
            var form = builder.ToFormXObject(store, new(0, 0, run.Width, height), resources);
            return new(PdfWatermarkForms.AddForm(transaction, form, group), run.Width, height);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Finds the code that shows each character of a simple font.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The code of each character.</returns>
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

    /// <summary>Measures the text in a built-in font.</summary>
    /// <returns>The codes and metrics.</returns>
    /// <exception cref="ArgumentException">The font cannot show every character.</exception>
    private TextRun MeasureBuiltInFont()
    {
        var codes = new byte[Text.Length];
        var count = AppearanceFontMetrics.Encode(Text, codes);
        var runes = 0;
        foreach (var rune in Text.EnumerateRunes())
        {
            _ = rune;
            runes++;
        }

        if (count != runes)
        {
            throw new ArgumentException("The built-in font cannot show every character of the text.", nameof(Text));
        }

        var used = codes.AsSpan(0, count).ToArray();
        return new(
            used,
            AppearanceFontMetrics.MeasureAdvance(Style.Font, used, Style.FontSize),
            AppearanceFontMetrics.GetAscent(Style.Font) * Style.FontSize,
            AppearanceFontMetrics.GetDescent(Style.Font) * Style.FontSize);
    }

    /// <summary>Measures the text in a simple font that is already in the document.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The codes and metrics.</returns>
    /// <exception cref="ArgumentException">The font is missing, not a simple font, or cannot show every character.</exception>
    private TextRun MeasureDocumentFont(PdfObjectStore store)
    {
        var dictionary = StoreReading.GetDictionary(store, Style.DocumentFont);
        if (dictionary is null || PdfFontLoader.Load(dictionary) is not PdfSimpleFont font)
        {
            throw new ArgumentException("The document font is missing or is not a simple (single byte) font.", nameof(Style));
        }

        var codes = MapCharacters(font);
        var bytes = new byte[Text.Length];
        var width = 0F;
        for (var i = 0; i < Text.Length; i++)
        {
            if (!codes.TryGetValue(Text[i], out var code))
            {
                throw new ArgumentException($"The document font has no character '{Text[i]}'.", nameof(Text));
            }

            bytes[i] = code;
            width += font.GetWidth(code);
        }

        return new(bytes, width * Style.FontSize, font.Ascent * Style.FontSize, font.Descent * Style.FontSize);
    }

    /// <summary>Text encoded for a font, with its metrics in points.</summary>
    /// <param name="Codes">The string bytes.</param>
    /// <param name="Width">The advance width.</param>
    /// <param name="Ascent">The height above the baseline.</param>
    /// <param name="Descent">The depth below the baseline, a negative number.</param>
    private readonly record struct TextRun(byte[] Codes, float Width, float Ascent, float Descent);
}
