// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Platform.Linux.Spelling;

/// <summary>
/// Where each letter appears in a word of up to 64 letters, one bit per position, so the edit distance to other words is
/// counted a whole column at a time in one 64-bit number (Hyyrö 2003, with swapped neighbours counted as one change).
/// </summary>
internal readonly ref struct LetterMasks
{
    /// <summary>The letters kept in the direct table; others are found by a scan.</summary>
    internal const int AsciiLetters = 128;

    /// <summary>The longest word, one bit per letter.</summary>
    internal const int MaxLength = 64;

    /// <summary>The positions of each ASCII letter.</summary>
    private readonly Span<ulong> _ascii;

    /// <summary>The word's other letters, each once.</summary>
    private readonly ReadOnlySpan<char> _others;

    /// <summary>The positions of each of <see cref="_others"/>.</summary>
    private readonly ReadOnlySpan<ulong> _otherMasks;

    /// <summary>The bit of the word's last letter.</summary>
    private readonly ulong _last;

    /// <summary>The word.</summary>
    private readonly ReadOnlySpan<char> _word;

    /// <summary>Initializes a new instance of the <see cref="LetterMasks"/> struct.</summary>
    /// <param name="word">The word, no longer than <see cref="MaxLength"/>.</param>
    /// <param name="ascii">The ASCII table, all zero; <see cref="AsciiLetters"/> long. <see cref="Release"/> zeroes it again.</param>
    /// <param name="others">Space for the other letters; <see cref="MaxLength"/> long.</param>
    /// <param name="otherMasks">Space for the other letters' positions; <see cref="MaxLength"/> long.</param>
    internal LetterMasks(ReadOnlySpan<char> word, Span<ulong> ascii, Span<char> others, Span<ulong> otherMasks)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(word.Length, MaxLength);
        var count = 0;
        for (var i = 0; i < word.Length; i++)
        {
            var letter = word[i];
            var bit = 1UL << i;
            if (letter < AsciiLetters)
            {
                ascii[letter] |= bit;
                continue;
            }

            var at = others[..count].IndexOf(letter);
            if (at < 0)
            {
                at = count;
                others[at] = letter;
                otherMasks[at] = 0;
                count++;
            }

            otherMasks[at] |= bit;
        }

        _ascii = ascii;
        _others = others[..count];
        _otherMasks = otherMasks[..count];
        _word = word;
        _last = word.IsEmpty ? 0 : 1UL << (word.Length - 1);
    }

    /// <summary>Gets the word's length.</summary>
    internal int Length => _word.Length;

    /// <summary>Zeroes the ASCII table entries the word set, so the table can be reused without clearing all of it.</summary>
    internal void Release()
    {
        // A word sets a handful of entries; clearing just those measured faster than clearing the whole 1 KB table.
        foreach (var letter in _word)
        {
            if (letter < AsciiLetters)
            {
                _ascii[letter] = 0;
            }
        }
    }

    /// <summary>Counts the changes from the word to another: letters added, removed, replaced, or two neighbours swapped.</summary>
    /// <param name="text">The other word.</param>
    /// <param name="limit">The most changes that matter.</param>
    /// <returns>The changes, or more than <paramref name="limit"/> once past it.</returns>
    internal int Distance(ReadOnlySpan<char> text, int limit)
    {
        // Letters the words share at either end change nothing; when one word is all shared, the other's remaining
        // letters are the changes, which settles most near misses without counting.
        var common = _word.CommonPrefixLength(text);
        var word = _word[common..];
        var other = text[common..];
        while (!word.IsEmpty && !other.IsEmpty && word[^1] == other[^1])
        {
            word = word[..^1];
            other = other[..^1];
        }

        return word.IsEmpty || other.IsEmpty ? Math.Min(Math.Max(word.Length, other.Length), limit + 1) : CountChanges(text, limit);
    }

    /// <summary>Counts the changes column by column, a whole column in one 64-bit number.</summary>
    /// <param name="text">The other word.</param>
    /// <param name="limit">The most changes that matter.</param>
    /// <returns>The changes, or more than <paramref name="limit"/> once past it.</returns>
    private int CountChanges(ReadOnlySpan<char> text, int limit)
    {
        // The vertical deltas of the current column: positive, negative, and the diagonal zero mask before them.
        var positive = ulong.MaxValue;
        var negative = 0UL;
        var diagonal = 0UL;
        var previousMatch = 0UL;
        var distance = _word.Length;
        for (var j = 0; j < text.Length; j++)
        {
            var match = MaskOf(text[j]);
            var swapped = ((~diagonal & match) << 1) & previousMatch;
            diagonal = (((match & positive) + positive) ^ positive) | match | negative | swapped;
            var horizontalPositive = negative | ~(diagonal | positive);
            var horizontalNegative = diagonal & positive;
            if ((horizontalPositive & _last) != 0)
            {
                distance++;
            }
            else if ((horizontalNegative & _last) != 0)
            {
                distance--;
            }

            // Each letter still to come can lower the count by at most one.
            if (distance - (text.Length - 1 - j) > limit)
            {
                return limit + 1;
            }

            horizontalPositive = (horizontalPositive << 1) | 1;
            horizontalNegative <<= 1;
            positive = horizontalNegative | ~(diagonal | horizontalPositive);
            negative = horizontalPositive & diagonal;
            previousMatch = match;
        }

        return distance;
    }

    /// <summary>Gets the positions of a letter in the word.</summary>
    /// <param name="letter">The letter.</param>
    /// <returns>One bit per position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ulong MaskOf(char letter)
    {
        if (letter < AsciiLetters)
        {
            return _ascii[letter];
        }

        var at = _others.IndexOf(letter);
        return at < 0 ? 0 : _otherMasks[at];
    }
}
