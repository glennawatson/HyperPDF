// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>Reads name spellings for the font loaders.</summary>
internal static class PdfNames
{
    /// <summary>The most base font names kept; later names are not kept.</summary>
    private const int MaxKnownBaseFonts = 1024;

    /// <summary>The characters of a base font name read on the stack; longer names are not kept.</summary>
    private const int StackNameChars = 256;

    /// <summary>Gets the spelling of a name used in a dictionary.</summary>
    /// <param name="owner">The dictionary the name came from, whose document holds the name table.</param>
    /// <param name="name">The name.</param>
    /// <returns>The UTF-8 bytes, or empty when the name cannot be spelled.</returns>
    internal static ReadOnlySpan<byte> Spell(PdfDictionary owner, PdfName name)
    {
        if (name.IsNone)
        {
            return [];
        }

        if (name.IsKnown)
        {
            return PdfNameTable.GetKnownSpelling(name.ToKnownName());
        }

        return owner.Owner is { } store ? store.Names.GetSpelling(name) : [];
    }

    /// <summary>Gets a name entry's spelling.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The UTF-8 bytes, or empty when the entry is not a name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ReadOnlySpan<byte> SpellEntry(PdfDictionary dictionary, PdfName key) => Spell(dictionary, dictionary.GetName(key));

    /// <summary>Gets a font's /BaseFont without its subset tag, as text.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The name, or empty.</returns>
    internal static string BaseFontOf(PdfDictionary dictionary)
    {
        // Fonts that share a name share one string, so loading a font allocates the name only the first time.
        var spelled = StandardFonts.StripSubsetTag(SpellEntry(dictionary, KnownName.BaseFont));
        if (spelled.IsEmpty)
        {
            return string.Empty;
        }

        if (Encoding.UTF8.GetMaxCharCount(spelled.Length) > StackNameChars)
        {
            return Encoding.UTF8.GetString(spelled);
        }

        Span<char> chars = stackalloc char[StackNameChars];
        var name = chars[..Encoding.UTF8.GetChars(spelled, chars)];
        if (dictionary.Owner?.Context is not { } context)
        {
            return new(name);
        }

        var names = context.DrawingServices.FontResources.BaseFontNames.Known;
        var known = names.GetAlternateLookup<ReadOnlySpan<char>>();
        if (known.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var created = new string(name);
        if (names.Count < MaxKnownBaseFonts)
        {
            _ = names.TryAdd(created, created);
        }

        return created;
    }
}
