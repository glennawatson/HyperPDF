// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Text.Layout;

/// <summary>Finds a paragraph's direction from its first strongly directional character.</summary>
public static class TextDirection
{
    /// <summary>The first Hebrew code point.</summary>
    private const int HebrewStart = 0x0590;

    /// <summary>The last code point of the Hebrew, Arabic, Syriac, Thaana and NKo blocks.</summary>
    private const int RightToLeftBlocksEnd = 0x08FF;

    /// <summary>The first code point of the Hebrew and Arabic presentation forms.</summary>
    private const int PresentationStart = 0xFB1D;

    /// <summary>The last code point of the Arabic presentation forms A.</summary>
    private const int PresentationEnd = 0xFDFF;

    /// <summary>The first code point of the Arabic presentation forms B.</summary>
    private const int PresentationBStart = 0xFE70;

    /// <summary>The last code point of the Arabic presentation forms B.</summary>
    private const int PresentationBEnd = 0xFEFF;

    /// <summary>Determines whether a paragraph reads right to left.</summary>
    /// <param name="text">The paragraph.</param>
    /// <returns><see langword="true"/> when its first letter is Hebrew, Arabic or another right-to-left script.</returns>
    public static bool IsRightToLeft(ReadOnlySpan<char> text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsRightToLeft(rune.Value))
            {
                return true;
            }

            if (Rune.IsLetter(rune))
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Determines whether a character belongs to a right-to-left script.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns><see langword="true"/> for right-to-left letters.</returns>
    public static bool IsRightToLeft(int codePoint) =>
        codePoint is (>= HebrewStart and <= RightToLeftBlocksEnd) or (>= PresentationStart and <= PresentationEnd) or (>= PresentationBStart and <= PresentationBEnd);
}
