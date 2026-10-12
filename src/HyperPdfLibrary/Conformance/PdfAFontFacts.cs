// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the font dictionary facts the PDF/A font rules test, with veraPDF's reading of malformed values.</summary>
internal static class PdfAFontFacts
{
    /// <summary>The <c>/Flags</c> bit of a symbolic font.</summary>
    private const long SymbolicFlag = 4;

    /// <summary>The names of the standard 14 fonts.</summary>
    private static readonly string[] StandardNames =
    [
        "Courier-Bold", "Courier-BoldOblique", "Courier", "Courier-Oblique", "Helvetica", "Helvetica-Bold", "Helvetica-BoldOblique",
        "Helvetica-Oblique", "Symbol", "Times-Bold", "Times-BoldItalic", "Times-Italic", "Times-Roman", "ZapfDingbats",
    ];

    /// <summary>Gets the dictionary of a resolved value when it is a dictionary and not a stream.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The dictionary, or null.</returns>
    internal static PdfDictionary? Dictionary(PdfValue value) => value.Kind == PdfKind.Dictionary ? value.AsDictionary() : null;

    /// <summary>Determines whether a font dictionary subtype is one veraPDF reads as a font.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns><see langword="true"/> for Type1, MMType1, TrueType, Type3 and Type0.</returns>
    internal static bool IsFontKind(KnownName subtype) => subtype is KnownName.Type1 or KnownName.MMType1 or KnownName.TrueType or KnownName.Type3 or KnownName.Type0;

    /// <summary>Gets the descendant CIDFont of a composite font: the first element of <c>/DescendantFonts</c>.</summary>
    /// <param name="type0">The composite font dictionary.</param>
    /// <returns>The CIDFont dictionary, or null when it is missing or not a dictionary.</returns>
    internal static PdfDictionary? Descendant(PdfDictionary type0) => type0.GetArray(KnownName.DescendantFonts) is { Count: > 0 } fonts ? Dictionary(fonts.Get(0)) : null;

    /// <summary>
    /// Gets the dictionary veraPDF reads a font's own entries from. For a composite font that is the descendant CIDFont,
    /// so <c>/Type</c>, <c>/BaseFont</c> and the descriptor come from it; an empty dictionary stands in when it is missing.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <returns>The dictionary.</returns>
    internal static PdfDictionary Primary(PdfAFontUse font) => font.Kind != PdfAFontKind.Type0 ? font.Dictionary : Descendant(font.Dictionary) ?? new(null);

    /// <summary>Gets a font's descriptor when it is a dictionary.</summary>
    /// <param name="font">The font, or descendant CIDFont, dictionary.</param>
    /// <returns>The descriptor, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary? Descriptor(PdfDictionary font) => Dictionary(font.Get(KnownName.FontDescriptor));

    /// <summary>Determines whether a descriptor's <c>/Flags</c> marks the font symbolic.</summary>
    /// <param name="descriptor">The descriptor, or null.</param>
    /// <returns><see langword="true"/> when bit 3 is set.</returns>
    internal static bool IsSymbolic(PdfDictionary? descriptor) => descriptor?.Get(KnownName.Flags) is { IsNumber: true } flags && (flags.AsInteger() & SymbolicFlag) != 0;

    /// <summary>Gets the embedded font program stream veraPDF names first: <c>/FontFile</c>, <c>/FontFile2</c>, then <c>/FontFile3</c>.</summary>
    /// <param name="descriptor">The descriptor, or null.</param>
    /// <returns>The stream, or null.</returns>
    internal static PdfStream? FirstProgram(PdfDictionary? descriptor) =>
        descriptor?.GetStream(KnownName.FontFile) ?? descriptor?.GetStream(KnownName.FontFile2) ?? descriptor?.GetStream(KnownName.FontFile3);

    /// <summary>Determines whether a dictionary entry is a number, which veraPDF reads as an integer.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The integer, or zero.</param>
    /// <returns><see langword="true"/> when the entry is a number.</returns>
    internal static bool TryGetInteger(PdfDictionary dictionary, PdfName key, out long value)
    {
        var entry = dictionary.Get(key);
        value = entry.IsNumber ? entry.AsInteger() : 0;
        return entry.IsNumber;
    }

    /// <summary>Gets the text of a name entry, or null when the entry is not a name.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The name's text, or null.</returns>
    internal static string? NameText(PdfObjectStore store, PdfDictionary dictionary, PdfName key) =>
        dictionary.Get(key).TryGetName(out var name) ? store.Names.GetString(name) : null;

    /// <summary>Determines whether a Type 1 font is one of the standard 14 without an embedded program veraPDF reads.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The Type 1 font dictionary.</param>
    /// <returns><see langword="true"/> for a standard font.</returns>
    internal static bool IsStandard(PdfObjectStore store, PdfDictionary font)
    {
        if (NameText(store, font, KnownName.BaseFont) is not { } name || !IsStandardName(name))
        {
            return false;
        }

        var descriptor = Descriptor(font);
        if (descriptor?.GetStream(KnownName.FontFile) is not null)
        {
            return false;
        }

        return descriptor?.GetStream(KnownName.FontFile3) is not { } compact
            || !(compact.Dictionary.IsName(KnownName.Subtype, KnownName.Type1C) || compact.Dictionary.IsName(KnownName.Subtype, KnownName.OpenType));
    }

    /// <summary>Determines whether a name is exactly one of the standard 14 font names.</summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsStandardName(string name) => Array.IndexOf(StandardNames, name) >= 0;

    /// <summary>Names a font for a report.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns>For example "font Helvetica".</returns>
    internal static string Context(PdfObjectStore store, PdfDictionary font) => $"font {NameText(store, font, KnownName.BaseFont) ?? "(unnamed)"}";
}
