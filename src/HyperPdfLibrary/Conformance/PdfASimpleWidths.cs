// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the width a simple font dictionary gives a code, as veraPDF does: /Widths when /FirstChar and /LastChar bound the
/// code, else the descriptor's /MissingWidth when it has one, else null for Type 1 and Type 3 fonts and zero otherwise.
/// </summary>
internal static class PdfASimpleWidths
{
    /// <summary>Gets a code's width from the font dictionary.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <param name="nullWithoutEntry">Whether the font type reads null rather than zero without an entry.</param>
    /// <returns>The width, or null.</returns>
    internal static double? Width(PdfAGlyphFont font, int code, bool nullWithoutEntry)
    {
        if (TryGetListed(font.Primary, code, out var listed))
        {
            return listed;
        }

        if (font.HasMissingWidth)
        {
            return font.MissingWidth;
        }

        return nullWithoutEntry ? null : 0;
    }

    /// <summary>Reads a code's /Widths entry when /FirstChar and /LastChar bound it.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="code">The code.</param>
    /// <param name="width">The entry, or null when it is not a number.</param>
    /// <returns><see langword="true"/> when /Widths covers the code.</returns>
    private static bool TryGetListed(PdfDictionary dictionary, int code, out double? width)
    {
        width = null;
        if (!dictionary.ContainsKey(KnownName.Widths) || !PdfAFontFacts.TryGetInteger(dictionary, KnownName.FirstChar, out var first)
            || !PdfAFontFacts.TryGetInteger(dictionary, KnownName.LastChar, out var last))
        {
            return false;
        }

        if (dictionary.GetArray(KnownName.Widths) is not { Count: > 0 } widths || code < first || code > last)
        {
            return false;
        }

        var index = (int)(code - first);
        width = index < widths.Count && widths.Get(index) is { IsNumber: true } entry ? entry.AsNumber() : null;
        return true;
    }
}
