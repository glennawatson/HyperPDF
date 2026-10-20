// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Gives font names the subset tag ISO 32000-2 9.6.4 asks for: six capital letters and a plus sign before the name of a font
/// whose program holds only some of its glyphs. The letters are made from the program and the glyphs kept, so the same
/// subset always gets the same tag and different subsets of one font get different tags.
/// </summary>
internal static class FontNaming
{
    /// <summary>The number of letters in a subset tag.</summary>
    internal const int TagLength = 6;

    /// <summary>The separator between a subset tag and the font name.</summary>
    internal const byte Separator = (byte)'+';

    /// <summary>The number of letters in the alphabet.</summary>
    private const int Letters = 26;

    /// <summary>The bytes one glyph number takes in the hash input.</summary>
    private const int GlyphBytes = sizeof(int);

    /// <summary>Makes the tag of a subset.</summary>
    /// <param name="program">The embedded program's number.</param>
    /// <param name="kept">The glyphs kept.</param>
    /// <returns>Six capital letters.</returns>
    internal static string Tag(int program, CodeBitSet kept)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> number = stackalloc byte[GlyphBytes];
        BinaryPrimitives.WriteInt32BigEndian(number, program);
        hash.AppendData(number);
        for (var bit = kept.NextBit(0); bit != CodeBitSet.NoBit; bit = kept.NextBit(bit + 1))
        {
            BinaryPrimitives.WriteInt32BigEndian(number, bit);
            hash.AppendData(number);
        }

        if (kept.Overflow is { } overflow)
        {
            foreach (var glyph in overflow)
            {
                BinaryPrimitives.WriteInt32BigEndian(number, glyph);
                hash.AppendData(number);
            }
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        _ = hash.GetHashAndReset(digest);
        return string.Create(TagLength, digest.ToArray(), static (span, bytes) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = (char)('A' + (bytes[i] % Letters));
            }
        });
    }

    /// <summary>Gets a font name as text without any subset tag.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="name">The name, or none.</param>
    /// <returns>The bare name; empty when there is none.</returns>
    internal static string BareName(PdfNameTable names, PdfName name)
    {
        if (name.IsNone)
        {
            return string.Empty;
        }

        var spelling = names.GetSpelling(name);
        return Encoding.ASCII.GetString(HasTag(spelling) ? spelling[(TagLength + 1)..] : spelling);
    }

    /// <summary>Puts a tag on a name, replacing any tag it has.</summary>
    /// <param name="name">The name's spelling.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>The tagged name.</returns>
    internal static byte[] WithTag(ReadOnlySpan<byte> name, string tag)
    {
        var bare = HasTag(name) ? name[(TagLength + 1)..] : name;
        var result = new byte[TagLength + 1 + bare.Length];
        _ = Encoding.ASCII.GetBytes(tag, result);
        result[TagLength] = Separator;
        bare.CopyTo(result.AsSpan(TagLength + 1));
        return result;
    }

    /// <summary>Applies a rename to a font object.</summary>
    /// <param name="source">The object's dictionary.</param>
    /// <param name="rename">What to rename.</param>
    /// <param name="names">The document's name table.</param>
    /// <returns>A copy with the tagged names.</returns>
    internal static PdfDictionary Apply(PdfDictionary source, FontRename rename, PdfNameTable names)
    {
        var copy = source.Clone();
        switch (rename.Kind)
        {
            case FontRenameKind.Descriptor:
                {
                    Retag(copy, KnownName.FontName, rename.Tag, names);
                    break;
                }

            case FontRenameKind.Descendant:
                {
                    Retag(copy, KnownName.BaseFont, rename.Tag, names);
                    RetagDescriptor(copy, rename.Tag, names);
                    break;
                }

            default:
                {
                    Retag(copy, KnownName.BaseFont, rename.Tag, names);
                    RetagDescendant(copy, rename.Tag, names);
                    RetagDescriptor(copy, rename.Tag, names);
                    break;
                }
        }

        return copy;
    }

    /// <summary>Determines whether a name starts with a subset tag.</summary>
    /// <param name="name">The name's spelling.</param>
    /// <returns><see langword="true"/> for six capital letters and a plus sign.</returns>
    private static bool HasTag(ReadOnlySpan<byte> name)
    {
        if (name.Length <= TagLength || name[TagLength] != Separator)
        {
            return false;
        }

        foreach (var letter in name[..TagLength])
        {
            if (letter is < (byte)'A' or > (byte)'Z')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Replaces a name entry with its tagged form.</summary>
    /// <param name="dictionary">The dictionary, changed in place.</param>
    /// <param name="key">The entry.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="names">The name table.</param>
    private static void Retag(PdfDictionary dictionary, KnownName key, string tag, PdfNameTable names)
    {
        var current = dictionary.GetName(key);
        if (current.IsNone)
        {
            return;
        }

        var tagged = WithTag(names.GetSpelling(current), tag);
        dictionary.Set(key, PdfValue.FromName(names.Intern(tagged)));
    }

    /// <summary>Tags the descriptor stored inside a dictionary, if the descriptor is not a separate object.</summary>
    /// <param name="holder">The font or descendant font, changed in place.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="names">The name table.</param>
    private static void RetagDescriptor(PdfDictionary holder, string tag, PdfNameTable names)
    {
        if (holder.GetRaw(KnownName.FontDescriptor).IsReference || holder.GetDictionary(KnownName.FontDescriptor) is not { } descriptor)
        {
            return;
        }

        var copy = descriptor.Clone();
        Retag(copy, KnownName.FontName, tag, names);
        holder.Set(KnownName.FontDescriptor, PdfValue.FromDictionary(copy));
    }

    /// <summary>Tags the descendant font stored inside a Type 0 font, if it is not a separate object.</summary>
    /// <param name="font">The Type 0 font, changed in place.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="names">The name table.</param>
    private static void RetagDescendant(PdfDictionary font, string tag, PdfNameTable names)
    {
        if (font.GetRaw(KnownName.DescendantFonts).IsReference || font.GetArray(KnownName.DescendantFonts) is not { } array || array.GetRaw(0).IsReference)
        {
            return;
        }

        if (array.GetDictionary(0) is not { } descendant)
        {
            return;
        }

        var copy = descendant.Clone();
        Retag(copy, KnownName.BaseFont, tag, names);
        RetagDescriptor(copy, tag, names);
        var items = array.Clone();
        items.SetAt(0, PdfValue.FromDictionary(copy));
        font.Set(KnownName.DescendantFonts, PdfValue.FromArray(items));
    }
}
