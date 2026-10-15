// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>Joins characters into lines, splitting wide gaps so side-by-side columns stay apart.</summary>
internal static class ReadingLines
{
    /// <summary>The Arabic letter lam.</summary>
    private const char ArabicLam = 'ل';

    /// <summary>The soft hyphen some documents use at line breaks.</summary>
    private const char SoftHyphen = '­';

    /// <summary>The most characters of text a page character adds: itself and an inferred space before it.</summary>
    private const int MostCharsPerCharacter = 2;

    /// <summary>The share of the smaller height two boxes must overlap by to be on one line.</summary>
    private const float SameLineOverlap = 0.3F;

    /// <summary>A gap wider than this many font sizes on one line separates columns rather than words.</summary>
    private const float ColumnGapSizes = 2.5F;

    /// <summary>A gap wider than this share of the font size between characters is a space.</summary>
    private const float SpaceGapShare = 0.2F;

    /// <summary>Joins characters into lines and adds them to the scratch.</summary>
    /// <param name="scratch">The working space; the lines are added after any it holds.</param>
    /// <param name="characters">The characters.</param>
    /// <param name="pageIndices">Each character's page index, when the characters are a subset of the page; empty when their positions are used.</param>
    internal static void Build(ReadingScratch scratch, ReadOnlySpan<PageCharacter> characters, ReadOnlySpan<int> pageIndices)
    {
        scratch.EnsureChars(characters.Length * MostCharsPerCharacter);
        var current = -1;
        var pendingSpace = false;
        var rightToLeftPage = IsMostlyRightToLeft(characters);
        for (var i = 0; i < characters.Length; i++)
        {
            ref readonly var character = ref characters[i];
            if (character.Generated || char.IsWhiteSpace(character.Value) || character.Bounds.Width <= 0)
            {
                pendingSpace |= current >= 0;
                continue;
            }

            var index = pageIndices.IsEmpty ? i : pageIndices[i];
            if (current < 0 || StartsNewLine(scratch, current, in character, RunRight(characters, i, scratch.Lines[current].IsRightToLeft)))
            {
                current = Start(scratch, index, in character, rightToLeftPage);
                pendingSpace = false;
                continue;
            }

            Append(scratch, current, index, in character, pendingSpace || Gap(scratch.Lines[current].Last, character.Bounds) > character.FontSize * SpaceGapShare);
            pendingSpace = false;
        }
    }

    /// <summary>Determines whether most of a page's letters are from right-to-left scripts.</summary>
    /// <param name="characters">The characters.</param>
    /// <returns><see langword="true"/> for a mostly right-to-left page.</returns>
    private static bool IsMostlyRightToLeft(ReadOnlySpan<PageCharacter> characters)
    {
        var rightToLeft = 0;
        var letters = 0;
        foreach (var character in characters)
        {
            var c = character.Value;
            if (!char.IsLetter(c))
            {
                continue;
            }

            letters++;
            rightToLeft += ReadingText.IsRightToLeft(c) ? 1 : 0;
        }

        return rightToLeft > letters - rightToLeft;
    }

    /// <summary>Starts a line with its first character.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="index">The character's page index.</param>
    /// <param name="character">The character.</param>
    /// <param name="rightToLeftPage">Whether the page is mostly right-to-left text.</param>
    /// <returns>The line's position.</returns>
    private static int Start(ReadingScratch scratch, int index, in PageCharacter character, bool rightToLeftPage)
    {
        var line = scratch.AddLine();
        ref var record = ref scratch.Lines[line];
        record.Bounds = character.Bounds;
        record.IsRightToLeft = rightToLeftPage || ReadingText.IsRightToLeft(character.Value);
        Append(scratch, line, index, in character, false);
        return line;
    }

    /// <summary>Adds a character to the line being built.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="line">The line, which is the last one.</param>
    /// <param name="index">The character's page index.</param>
    /// <param name="character">The character.</param>
    /// <param name="spaceBefore">Whether a space goes before it.</param>
    private static void Append(ReadingScratch scratch, int line, int index, in PageCharacter character, bool spaceBefore)
    {
        ref var record = ref scratch.Lines[line];
        if (spaceBefore && record.Count > 0 && scratch.Chars[scratch.CharCount - 1] != ' ')
        {
            Push(scratch, ref record, ' ', -1);
        }

        if (IsReversedLigature(scratch, line, in character))
        {
            // A lam-alef ligature mapped to Unicode as alef then lam: put the lam first, as it is read.
            var last = scratch.CharCount - 1;
            scratch.Chars[last + 1] = scratch.Chars[last];
            scratch.Indices[last + 1] = scratch.Indices[last];
            scratch.Chars[last] = ArabicLam;
            scratch.Indices[last] = index;
            scratch.CharCount++;
            record.Count++;
        }
        else
        {
            Push(scratch, ref record, character.Value == SoftHyphen ? '-' : character.Value, index);
        }

        record.Bounds = record.Bounds.Union(character.Bounds);
        record.Last = character.Bounds;
        record.SizeTotal += character.FontSize;
        record.Bold += character.Bold ? 1 : 0;
        record.Visible++;
    }

    /// <summary>Adds one character of text to the end of the line being built.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="record">The line.</param>
    /// <param name="value">The character.</param>
    /// <param name="index">Its page index, or -1 for an inferred space.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Push(ReadingScratch scratch, ref LineRecord record, char value, int index)
    {
        scratch.Chars[scratch.CharCount] = value;
        scratch.Indices[scratch.CharCount] = index;
        scratch.CharCount++;
        record.Count++;
    }

    /// <summary>Determines whether a lam completes a lam-alef ligature that was mapped as alef then lam.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="line">The line, which is the last one.</param>
    /// <param name="character">The new character.</param>
    /// <returns><see langword="true"/> when the lam belongs before the alef just added.</returns>
    private static bool IsReversedLigature(ReadingScratch scratch, int line, in PageCharacter character)
    {
        ref readonly var record = ref scratch.Lines[line];
        return record.IsRightToLeft && character.Value == ArabicLam && record.Count > 0 && IsAlef(scratch.Chars[scratch.CharCount - 1]) && character.Bounds == record.Last;
    }

    /// <summary>Determines whether a character is one of the Arabic alef forms a lam-alef ligature joins.</summary>
    /// <param name="c">The character.</param>
    /// <returns><see langword="true"/> for alef, alef with madda, or alef with hamza above or below.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAlef(char c) => c is 'آ' or 'أ' or 'إ' or 'ا';

    /// <summary>Determines whether a character starts a new line: below the line, back to its left, or past a column gap.</summary>
    /// <param name="scratch">The working space.</param>
    /// <param name="at">The current line.</param>
    /// <param name="character">The character.</param>
    /// <param name="runRight">The right edge of the word the character starts, for a left-to-right word in right-to-left text.</param>
    /// <returns><see langword="true"/> for a new line.</returns>
    private static bool StartsNewLine(ReadingScratch scratch, int at, in PageCharacter character, float runRight)
    {
        ref readonly var line = ref scratch.Lines[at];
        var bounds = character.Bounds;
        var overlap = Math.Min(bounds.Bottom, line.Bounds.Bottom) - Math.Max(bounds.Top, line.Bounds.Top);
        var sameLine = overlap > Math.Min(bounds.Height, line.Bounds.Height) * SameLineOverlap;
        var size = Math.Max(character.FontSize, 1);
        if (line.IsRightToLeft)
        {
            // Right-to-left text runs leftwards; embedded left-to-right words stay inside the line's extent.
            var forwards = bounds.Left > line.Bounds.Right + size;
            var gapLeft = line.Bounds.Left - runRight > size * ColumnGapSizes;
            return !sameLine || forwards || gapLeft;
        }

        var backwards = bounds.Left < line.Bounds.Right - size;
        var columnGap = bounds.Left - line.Bounds.Right > size * ColumnGapSizes;
        return !sameLine || backwards || columnGap;
    }

    /// <summary>
    /// Finds the right edge of the left-to-right word starting at a character inside a right-to-left line: such a word
    /// is laid out leftwards as a whole, so its first letter sits a word's width from the text before it.
    /// </summary>
    /// <param name="characters">The characters.</param>
    /// <param name="start">The character.</param>
    /// <param name="lineIsRightToLeft">Whether the current line is right-to-left.</param>
    /// <returns>The word's right edge, or the character's own when it is not such a word.</returns>
    private static float RunRight(ReadOnlySpan<PageCharacter> characters, int start, bool lineIsRightToLeft)
    {
        var right = characters[start].Bounds.Right;
        if (!lineIsRightToLeft)
        {
            return right;
        }

        for (var i = start; i < characters.Length; i++)
        {
            var c = characters[i];
            if (c.Generated || char.IsWhiteSpace(c.Value) || ReadingText.IsRightToLeft(c.Value))
            {
                break;
            }

            right = Math.Max(right, c.Bounds.Right);
        }

        return right;
    }

    /// <summary>Measures the horizontal gap between two neighbouring characters, whichever way the text runs.</summary>
    /// <param name="previous">The previous character's box.</param>
    /// <param name="next">The next character's box.</param>
    /// <returns>The gap, or a negative number when they overlap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Gap(in PageRect previous, in PageRect next) => Math.Max(next.Left - previous.Right, previous.Left - next.Right);
}
