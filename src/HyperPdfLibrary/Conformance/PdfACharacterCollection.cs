// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>A <c>/CIDSystemInfo</c> as the PDF/A rules compare it; a missing supplement reads as zero, as in veraPDF.</summary>
/// <param name="Registry">The registry, or null.</param>
/// <param name="Ordering">The ordering, or null.</param>
/// <param name="Supplement">The supplement.</param>
internal readonly record struct PdfACharacterCollection(string? Registry, string? Ordering, long Supplement)
{
    /// <summary>Reads a <c>/CIDSystemInfo</c> dictionary.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The collection; empty when the value is not a dictionary.</returns>
    internal static PdfACharacterCollection FromDictionary(PdfValue value)
    {
        if (PdfAFontFacts.Dictionary(value) is not { } info)
        {
            return default;
        }

        _ = PdfAFontFacts.TryGetInteger(info, KnownName.Supplement, out var supplement);
        return new(Text(info, KnownName.Registry), Text(info, KnownName.Ordering), supplement);
    }

    /// <summary>Reads the collection of a CMap: from the predefined table for a name, or from an embedded CMap's dictionary.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="cmap">The resolved CMap value.</param>
    /// <param name="part">The PDF/A part, which selects the PDF version of the predefined table.</param>
    /// <returns>The collection.</returns>
    internal static PdfACharacterCollection FromCMap(PdfObjectStore store, PdfValue cmap, int part)
    {
        if (cmap.TryGetName(out var name))
        {
            return PdfAPredefinedCMaps.Collection(store.Names.GetString(name), part) is { } known ? new(PdfAPredefinedCMaps.Registry, known.Ordering, known.Supplement) : default;
        }

        if (cmap.AsStream() is not { } stream)
        {
            return default;
        }

        var info = stream.Dictionary.Get(KnownName.CIDSystemInfo);
        return FromDictionary(info.AsArray() is { Count: > 0 } array ? array.Get(0) : info);
    }

    /// <summary>Reads a string entry as text.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or null when the entry is not a string.</returns>
    private static string? Text(PdfDictionary dictionary, PdfName key) => dictionary.Get(key).Kind == PdfKind.String ? dictionary.GetText(key) : null;
}
