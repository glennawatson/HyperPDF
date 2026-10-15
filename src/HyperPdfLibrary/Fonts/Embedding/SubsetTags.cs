// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>Makes the six-letter tag that marks a subset font's name (ISO 32000-2, 9.6.4).</summary>
internal static class SubsetTags
{
    /// <summary>The number of letters in a tag.</summary>
    private const int TagLength = 6;

    /// <summary>The number of letters to choose from.</summary>
    private const int Letters = 26;

    /// <summary>The FNV-1a 64-bit offset basis.</summary>
    private const ulong OffsetBasis = 14_695_981_039_346_656_037UL;

    /// <summary>The FNV-1a 64-bit prime.</summary>
    private const ulong Prime = 1_099_511_628_211UL;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes of a font name hashed on the stack.</summary>
    private const int StackNameBytes = 256;

    /// <summary>Gets the tag and plus sign for a font and its glyphs; the same inputs give the same tag.</summary>
    /// <param name="name">The font's PostScript name.</param>
    /// <param name="glyphs">The original glyphs the subset holds.</param>
    /// <returns>The prefix, such as <c>ABCDEF+</c>.</returns>
    internal static string Create(string name, ReadOnlySpan<int> glyphs)
    {
        var hash = OffsetBasis;
        Span<byte> stack = stackalloc byte[StackNameBytes];
        byte[]? rented = null;
        var bytes = Encoding.UTF8.GetMaxByteCount(name.Length) <= StackNameBytes ? stack : (rented = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(name.Length)));
        try
        {
            foreach (var b in bytes[..Encoding.UTF8.GetBytes(name, bytes)])
            {
                hash = (hash ^ b) * Prime;
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        foreach (var glyph in glyphs)
        {
            for (var shift = 0; shift < sizeof(int) * ByteBits; shift += ByteBits)
            {
                hash = (hash ^ (byte)(glyph >> shift)) * Prime;
            }
        }

        return string.Create(TagLength + 1, hash, static (span, state) =>
        {
            for (var i = 0; i < TagLength; i++)
            {
                span[i] = (char)('A' + (int)(state % Letters));
                state /= Letters;
            }

            span[TagLength] = '+';
        });
    }

    /// <summary>Determines whether a name already starts with a subset tag.</summary>
    /// <param name="name">The font name.</param>
    /// <returns><see langword="true"/> for a name such as <c>ABCDEF+Name</c>.</returns>
    internal static bool HasTag(ReadOnlySpan<char> name)
    {
        if (name.Length <= TagLength || name[TagLength] != '+')
        {
            return false;
        }

        foreach (var c in name[..TagLength])
        {
            if (c is < 'A' or > 'Z')
            {
                return false;
            }
        }

        return true;
    }
}
