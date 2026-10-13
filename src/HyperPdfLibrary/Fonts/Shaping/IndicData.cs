// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>
/// Character data for the Indic scripts, taken from the layout the Unicode blocks of Devanagari, Bengali, Gurmukhi, Gujarati,
/// Oriya, Tamil, Telugu, Kannada and Malayalam share (each block is 128 characters in the order of ISCII), and from the
/// script-specific rules of the OpenType Indic script development specifications.
/// </summary>
internal static class IndicData
{
    /// <summary>The first Indic block, Devanagari.</summary>
    private const int FirstBlock = 0x0900;

    /// <summary>The end of the last Indic block, Malayalam.</summary>
    private const int LastBlockEnd = 0x0D7F;

    /// <summary>The mask that gives the start of a 128-character block.</summary>
    private const int BlockMask = ~0x7F;

    /// <summary>The mask that gives a character's place in its block.</summary>
    private const int OffsetMask = 0x7F;

    /// <summary>The number of characters in a block.</summary>
    private const int BlockLength = 0x80;

    /// <summary>The Devanagari block.</summary>
    private const int Devanagari = 0x0900;

    /// <summary>The Bengali block.</summary>
    private const int Bengali = 0x0980;

    /// <summary>The Gurmukhi block.</summary>
    private const int Gurmukhi = 0x0A00;

    /// <summary>The Tamil block.</summary>
    private const int Tamil = 0x0B80;

    /// <summary>The Telugu block.</summary>
    private const int Telugu = 0x0C00;

    /// <summary>The Kannada block.</summary>
    private const int Kannada = 0x0C80;

    /// <summary>The Malayalam block.</summary>
    private const int Malayalam = 0x0D00;

    /// <summary>The zero width non-joiner.</summary>
    private const int ZeroWidthNonJoiner = 0x200C;

    /// <summary>The zero width joiner.</summary>
    private const int ZeroWidthJoiner = 0x200D;

    /// <summary>The offset of the Devanagari prishthamatra e, a vowel sign written before the consonant.</summary>
    private const int PrishthamatraOffset = 0x4E;

    /// <summary>The offset of the vowel sign i, written before the consonant in Devanagari, Bengali and Gurmukhi.</summary>
    private const int SignIOffset = 0x3F;

    /// <summary>The offset of the first of the vowel signs e, ee and ai.</summary>
    private const int SignEOffset = 0x46;

    /// <summary>The offset of the Bengali vowel sign e.</summary>
    private const int BengaliSignEOffset = 0x47;

    /// <summary>The offset of the last of the Bengali and Dravidian vowel signs written before the consonant.</summary>
    private const int LastPreSignOffset = 0x48;

    /// <summary>The offset of ra in a block, which is the letter that forms a reph.</summary>
    private const int RaOffset = 0x30;

    /// <summary>The offset of ya in a block.</summary>
    private const int YaOffset = 0x2F;

    /// <summary>The offset of the first Devanagari extension consonant.</summary>
    private const int FirstExtendedConsonant = 0x79;

    /// <summary>The offset of the first Devanagari extension vowel.</summary>
    private const int FirstExtendedVowel = 0x72;

    /// <summary>The offset of the last Devanagari extension vowel.</summary>
    private const int LastExtendedVowel = 0x77;

    /// <summary>The category of each place in a block.</summary>
    private static readonly IndicCategory[] OffsetCategories = BuildOffsetCategories();

    /// <summary>The two-part vowel signs and the two characters each is written as (Unicode canonical decompositions).</summary>
    private static readonly SplitSign[] Splits =
    [
        new(0x09CB, 0x09C7, 0x09BE),
        new(0x09CC, 0x09C7, 0x09D7),
        new(0x0B4B, 0x0B47, 0x0B3E),
        new(0x0B4C, 0x0B47, 0x0B57),
        new(0x0BCA, 0x0BC6, 0x0BBE),
        new(0x0BCB, 0x0BC7, 0x0BBE),
        new(0x0BCC, 0x0BC6, 0x0BD7),
        new(0x0D4A, 0x0D46, 0x0D3E),
        new(0x0D4B, 0x0D47, 0x0D3E),
        new(0x0D4C, 0x0D46, 0x0D57),
    ];

    /// <summary>Gets the category of a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The category; <see cref="IndicCategory.Other"/> for characters outside the Indic blocks.</returns>
    internal static IndicCategory CategoryOf(int codePoint)
    {
        if (codePoint == ZeroWidthJoiner)
        {
            return IndicCategory.Joiner;
        }

        if (codePoint == ZeroWidthNonJoiner)
        {
            return IndicCategory.NonJoiner;
        }

        if (codePoint is < FirstBlock or > LastBlockEnd)
        {
            return IndicCategory.Other;
        }

        var offset = codePoint & OffsetMask;
        var block = codePoint & BlockMask;
        var category = OffsetCategories[offset];
        if (category == IndicCategory.Matra && IsMatraBefore(block, offset))
        {
            return IndicCategory.MatraBefore;
        }

        return category == IndicCategory.Other && block == Devanagari ? DevanagariOther(offset) : category;
    }

    /// <summary>Determines whether a character is ra, the consonant that forms a reph before a virama.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> for ra.</returns>
    internal static bool IsRa(int codePoint) => codePoint is >= FirstBlock and <= LastBlockEnd && (codePoint & OffsetMask) == RaOffset;

    /// <summary>Determines whether a script writes the consonants after a virama below the first consonant, so the base is the first consonant.</summary>
    /// <param name="codePoint">A character of the script.</param>
    /// <returns><see langword="true"/> for Telugu and Kannada.</returns>
    internal static bool BaseIsFirstConsonant(int codePoint) => (codePoint & BlockMask) is Telugu or Kannada;

    /// <summary>Determines whether a consonant takes a below-base or post-base form after a virama, so it is not the base of the syllable.</summary>
    /// <param name="codePoint">The consonant.</param>
    /// <returns><see langword="true"/> for ra, and in Bengali also ya, which become subjoined signs.</returns>
    internal static bool IsSubjoiningConsonant(int codePoint)
    {
        var offset = codePoint & OffsetMask;
        return offset == RaOffset || ((codePoint & BlockMask) == Bengali && offset == YaOffset);
    }

    /// <summary>Splits a two-part vowel sign into its parts, as the font needs them when it has no glyph for the whole sign.</summary>
    /// <param name="codePoint">The vowel sign.</param>
    /// <param name="first">Receives the part written first.</param>
    /// <param name="second">Receives the part written second.</param>
    /// <returns><see langword="true"/> when the character splits.</returns>
    internal static bool TrySplitVowelSign(int codePoint, out int first, out int second)
    {
        first = 0;
        second = 0;
        for (var i = 0; i < Splits.Length; i++)
        {
            if (Splits[i].Sign != codePoint)
            {
                continue;
            }

            first = Splits[i].First;
            second = Splits[i].Second;
            return true;
        }

        return false;
    }

    /// <summary>Builds the category of each place in a block: the layout the Indic blocks share.</summary>
    /// <returns>The categories, by place.</returns>
    private static IndicCategory[] BuildOffsetCategories()
    {
        var table = new IndicCategory[BlockLength];
        Fill(table, 0x00, 0x03, IndicCategory.Modifier);
        Fill(table, 0x04, 0x14, IndicCategory.Vowel);
        Fill(table, 0x15, 0x39, IndicCategory.Consonant);
        Fill(table, 0x3A, 0x3B, IndicCategory.Matra);
        Fill(table, 0x3C, 0x3C, IndicCategory.Nukta);
        Fill(table, 0x3E, 0x4C, IndicCategory.Matra);
        Fill(table, 0x4D, 0x4D, IndicCategory.Halant);
        Fill(table, 0x51, 0x54, IndicCategory.Modifier);
        Fill(table, 0x55, 0x57, IndicCategory.Matra);
        Fill(table, 0x58, 0x5F, IndicCategory.Consonant);
        Fill(table, 0x60, 0x61, IndicCategory.Vowel);
        Fill(table, 0x62, 0x63, IndicCategory.Matra);
        return table;
    }

    /// <summary>Gives a run of places a category.</summary>
    /// <param name="table">The table.</param>
    /// <param name="first">The first place.</param>
    /// <param name="last">The last place.</param>
    /// <param name="category">The category.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Fill(IndicCategory[] table, int first, int last, IndicCategory category) =>
        Array.Fill(table, category, first, last - first + 1);

    /// <summary>Gets the category of a place in the Devanagari block that the shared layout leaves open.</summary>
    /// <param name="offset">The place in the block.</param>
    /// <returns>The category.</returns>
    private static IndicCategory DevanagariOther(int offset) => offset switch
    {
        PrishthamatraOffset => IndicCategory.MatraBefore,
        >= FirstExtendedVowel and <= LastExtendedVowel => IndicCategory.Vowel,
        >= FirstExtendedConsonant => IndicCategory.Consonant,
        _ => IndicCategory.Other,
    };

    /// <summary>Determines whether a vowel sign is written before its consonant.</summary>
    /// <param name="block">The start of the block.</param>
    /// <param name="offset">The sign's place in the block.</param>
    /// <returns><see langword="true"/> for the signs that precede the consonant.</returns>
    private static bool IsMatraBefore(int block, int offset) => block switch
    {
        Devanagari or Gurmukhi => offset == SignIOffset,
        Bengali => offset == SignIOffset || offset is BengaliSignEOffset or LastPreSignOffset,
        Tamil or Malayalam => offset is >= SignEOffset and <= LastPreSignOffset,
        _ => false,
    };

    /// <summary>A two-part vowel sign and its parts.</summary>
    /// <param name="Sign">The vowel sign.</param>
    /// <param name="First">The part written first.</param>
    /// <param name="Second">The part written second.</param>
    private readonly record struct SplitSign(int Sign, int First, int Second);
}
