// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>Character properties the shaper needs: which characters have no visible form of their own, and which brackets mirror in right-to-left text.</summary>
internal static class ShapingCharacters
{
    /// <summary>The characters that mirror in right-to-left text, as pairs of the character and its mirror image (Unicode Standard Annex #9, rule L4, BidiMirroring.txt).</summary>
    private static readonly MirrorPair[] Mirrors =
    [
        new('(', ')'),
        new(')', '('),
        new('<', '>'),
        new('>', '<'),
        new('[', ']'),
        new(']', '['),
        new('{', '}'),
        new('}', '{'),
        new('«', '»'),
        new('»', '«'),
        new('‹', '›'),
        new('›', '‹'),
        new('⁅', '⁆'),
        new('⁆', '⁅'),
        new('≤', '≥'),
        new('≥', '≤'),
    ];

    /// <summary>The ranges of default ignorable characters (Unicode Standard, "Default Ignorable Code Points"): soft hyphen, joiners, directional marks, variation selectors and tags.</summary>
    private static readonly CharacterRange[] Ignorable =
    [
        new(0x00AD, 0x00AD),
        new(0x034F, 0x034F),
        new(0x061C, 0x061C),
        new(0x115F, 0x1160),
        new(0x17B4, 0x17B5),
        new(0x180B, 0x180F),
        new(0x200B, 0x200F),
        new(0x202A, 0x202E),
        new(0x2060, 0x206F),
        new(0x3164, 0x3164),
        new(0xFE00, 0xFE0F),
        new(0xFEFF, 0xFEFF),
        new(0xFFA0, 0xFFA0),
        new(0xE0000, 0xE0FFF),
    ];

    /// <summary>Determines whether a character has no visible form: a joiner, directional mark, variation selector or other format character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> when the character is default ignorable.</returns>
    internal static bool IsDefaultIgnorable(int codePoint)
    {
        foreach (var range in Ignorable)
        {
            if (codePoint >= range.First && codePoint <= range.Last)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the character that is the mirror image of a bracket or comparison sign.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns>The mirrored character; the character itself when it does not mirror.</returns>
    internal static int Mirror(int codePoint)
    {
        foreach (var pair in Mirrors)
        {
            if (pair.Character == codePoint)
            {
                return pair.Mirrored;
            }
        }

        return codePoint;
    }

    /// <summary>A character and its mirror image.</summary>
    /// <param name="Character">The character.</param>
    /// <param name="Mirrored">The mirror image.</param>
    private readonly record struct MirrorPair(int Character, int Mirrored);

    /// <summary>A range of characters.</summary>
    /// <param name="First">The first character.</param>
    /// <param name="Last">The last character.</param>
    private readonly record struct CharacterRange(int First, int Last);
}
