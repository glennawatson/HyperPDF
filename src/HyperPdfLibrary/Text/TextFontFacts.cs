// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>What a font says about itself: its name without the subset tag, its weight and its descriptor flags.</summary>
/// <param name="Name">The font's name, or empty.</param>
/// <param name="Weight">The weight from 100 to 900, 400 for regular and 700 for bold.</param>
/// <param name="Flags">The font descriptor flags, with italic and force bold added from the font's name and weight.</param>
[DebuggerDisplay("TextFontFacts: {Name} {Weight} {Flags}")]
internal readonly record struct TextFontFacts(string Name, int Weight, FontFlags Flags)
{
    /// <summary>Gets the facts of a font that has no dictionary entries to read.</summary>
    internal static TextFontFacts None { get; } = new(string.Empty, ParsedFontName.NormalWeight, FontFlags.None);

    /// <summary>Reads the facts of a font from its dictionary and its descendant font.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The facts.</returns>
    internal static TextFontFacts Read(PdfFont font)
    {
        var dictionary = font.Dictionary;
        var name = PdfNames.BaseFontOf(dictionary);
        var descriptor = FontDescriptor.Read(FindDescriptor(dictionary));
        var parsed = ParsedFontName.Parse(name);
        var weight = Math.Max(descriptor.Weight, parsed.Weight);
        weight = weight > 0 ? weight : ParsedFontName.NormalWeight;
        var bold = font.IsBold || weight >= ParsedFontName.BoldWeight;
        var flags = descriptor.Flags | (parsed.Italic ? FontFlags.Italic : FontFlags.None);
        return new(name, bold ? Math.Max(weight, ParsedFontName.BoldWeight) : weight, flags);
    }

    /// <summary>Finds a font's descriptor, in the font itself or in the first descendant of a composite font.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The descriptor, or <see langword="null"/>.</returns>
    private static PdfDictionary? FindDescriptor(PdfDictionary dictionary) =>
        dictionary.GetDictionary(KnownName.FontDescriptor)
        ?? dictionary.GetArray(KnownName.DescendantFonts)?.GetDictionary(0)?.GetDictionary(KnownName.FontDescriptor);
}
