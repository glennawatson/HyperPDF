// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Conformance;

/// <summary>Looks glyph names up in the Adobe Glyph List as the PDF/A rules do: by exact name, with .notdef listed as U+0000.</summary>
internal static class PdfAGlyphNames
{
    /// <summary>The longest glyph name held on the stack; a longer one gets an array.</summary>
    private const int StackLimit = 64;

    /// <summary>Gets the name of the missing glyph.</summary>
    private static ReadOnlySpan<byte> Notdef => ".notdef"u8;

    /// <summary>Determines whether the Adobe Glyph List names a glyph.</summary>
    /// <param name="name">The glyph name's bytes.</param>
    /// <returns><see langword="true"/> when the list has the exact name.</returns>
    internal static bool IsListed(ReadOnlySpan<byte> name) => name.SequenceEqual(Notdef) || !GlyphList.GetListedText(GlyphNames.Find(name)).IsEmpty;

    /// <summary>Determines whether the Adobe Glyph List names a glyph, without making the text.</summary>
    /// <param name="name">The glyph name.</param>
    /// <returns><see langword="true"/> when the list has the exact name.</returns>
    internal static bool IsListed(string name)
    {
        Span<byte> bytes = name.Length <= StackLimit ? stackalloc byte[StackLimit] : new byte[name.Length];
        return IsListed(bytes[..Encoding.Latin1.GetBytes(name, bytes)]);
    }

    /// <summary>Gets the Unicode text the Adobe Glyph List gives a name.</summary>
    /// <param name="name">The glyph name.</param>
    /// <returns>The text, or null when the list does not have the exact name.</returns>
    internal static string? Text(string name)
    {
        Span<byte> bytes = name.Length <= StackLimit ? stackalloc byte[StackLimit] : new byte[name.Length];
        var length = Encoding.Latin1.GetBytes(name, bytes);
        if (Notdef.SequenceEqual(bytes[..length]))
        {
            return "\0";
        }

        var text = GlyphList.GetListedText(GlyphNames.Find(bytes[..length]));
        return text.IsEmpty ? null : new string(text);
    }

    /// <summary>Gets the first code point the Adobe Glyph List gives a name.</summary>
    /// <param name="name">The glyph name.</param>
    /// <returns>The code point, or -1 when the list does not have the exact name.</returns>
    internal static int FirstCodePoint(string name)
    {
        Span<byte> bytes = name.Length <= StackLimit ? stackalloc byte[StackLimit] : new byte[name.Length];
        var span =bytes[..Encoding.Latin1.GetBytes(name, bytes)];
        if (Notdef.SequenceEqual(span))
        {
            return 0;
        }

        var text = GlyphList.GetListedText(GlyphNames.Find(span));
        return text.IsEmpty ? -1 : text[0];
    }
}
