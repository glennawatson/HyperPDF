// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Reading;

namespace HyperPdf.Core.Spelling;

/// <summary>Finds the misspelled words in typed text, skipping what is not a word to check: numbers, codes and short capitals.</summary>
public static class SpellingWords
{
    /// <summary>Capitalised words up to this length are taken as abbreviations, such as "PDF".</summary>
    private const int AbbreviationLength = 5;

    /// <summary>Finds the misspelled words.</summary>
    /// <param name="text">The text.</param>
    /// <param name="checker">The spell checker.</param>
    /// <param name="ignored">Words the reader chose to keep; its comparer decides whether case matters.</param>
    /// <param name="output">Receives each misspelled word's place in the text.</param>
    public static void FindMisspelled(string text, ISpellChecker checker, HashSet<string> ignored, List<TextRange> output)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(ignored);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        if (text.Length == 0 || !checker.IsAvailable)
        {
            return;
        }

        // Words are checked where they sit in the text, so nothing is copied out.
        var kept = ignored.GetAlternateLookup<ReadOnlySpan<char>>();
        for (var start = 0; NextWord(text, ref start, out var length); start += length)
        {
            var word = text.AsSpan(start, length);
            if (ShouldCheck(word) && !kept.Contains(word) && !checker.IsCorrect(word))
            {
                output.Add(new(start, length));
            }
        }
    }

    /// <summary>Finds the word at a place in the text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">A character index, such as the caret's.</param>
    /// <returns>The word's place, or an empty range when the index is not in a word.</returns>
    public static TextRange WordAt(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (index < 0 || index > text.Length)
        {
            return default;
        }

        var start = index;
        while (start > 0 && IsWordChar(text, start - 1))
        {
            start--;
        }

        var end = index;
        while (end < text.Length && IsWordChar(text, end))
        {
            end++;
        }

        return new(start, end - start);
    }

    /// <summary>Moves to the next word.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">Where to look from; on return, where the word starts.</param>
    /// <param name="length">The word's length.</param>
    /// <returns><see langword="true"/> when a word was found.</returns>
    private static bool NextWord(string text, ref int start, out int length)
    {
        while (start < text.Length && !IsWordChar(text, start))
        {
            start++;
        }

        var end = start;
        while (end < text.Length && IsWordChar(text, end))
        {
            end++;
        }

        length = end - start;
        return length > 0;
    }

    /// <summary>Determines whether a character is part of a word: a letter or digit, or an apostrophe between letters.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The character.</param>
    /// <returns><see langword="true"/> for a word character.</returns>
    private static bool IsWordChar(string text, int index)
    {
        var c = text[index];
        if (char.IsLetterOrDigit(c))
        {
            return true;
        }

        return c is '\'' or '’' && index > 0 && index < text.Length - 1 && char.IsLetter(text[index - 1]) && char.IsLetter(text[index + 1]);
    }

    /// <summary>Determines whether a word is worth checking: not a single letter, not holding digits, not a short abbreviation.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> to check it.</returns>
    private static bool ShouldCheck(ReadOnlySpan<char> word)
    {
        if (word.Length < 2)
        {
            return false;
        }

        // One pass: a digit rules the word out, and a lower-case letter means it is not an abbreviation.
        var hasLower = false;
        foreach (var c in word)
        {
            if (char.IsAsciiDigit(c))
            {
                return false;
            }

            hasLower |= char.IsLower(c);
        }

        return hasLower || word.Length > AbbreviationLength;
    }
}
