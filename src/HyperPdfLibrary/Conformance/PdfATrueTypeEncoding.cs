// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A simple font's <c>/Encoding</c> as the PDF/A rules read it: the name, or the <c>/BaseEncoding</c> of an encoding
/// dictionary, and whether that dictionary has <c>/Differences</c>.
/// </summary>
/// <param name="Name">The encoding name, or null when there is none.</param>
/// <param name="HasDifferences">Whether the encoding is a dictionary with a <c>/Differences</c> entry.</param>
/// <param name="Differences">The <c>/Differences</c> array, or null.</param>
internal sealed record PdfATrueTypeEncoding(string? Name, bool HasDifferences, PdfArray? Differences)
{
    /// <summary>Gets a value indicating whether the name is MacRomanEncoding or WinAnsiEncoding.</summary>
    internal bool IsStandard => Name is "MacRomanEncoding" or "WinAnsiEncoding";

    /// <summary>Reads a font's encoding.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns>The encoding.</returns>
    internal static PdfATrueTypeEncoding Read(PdfObjectStore store, PdfDictionary font)
    {
        var encoding = font.Get(KnownName.Encoding);
        if (encoding.TryGetName(out var name))
        {
            return new(store.Names.GetString(name), false, null);
        }

        return PdfAFontFacts.Dictionary(encoding) is not { } dictionary
            ? new(null, false, null)
            : new(PdfAFontFacts.NameText(store, dictionary, KnownName.BaseEncoding), dictionary.ContainsKey(KnownName.Differences), dictionary.GetArray(KnownName.Differences));
    }
}
