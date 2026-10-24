// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdf.Core.Accessibility;

/// <summary>
/// Finds where characters, words, sentences, lines and paragraphs start and end, so a screen reader can move through the
/// page text by each. A stretch always starts at the beginning of its unit and ends at the beginning of the next one, so
/// the spaces after a word belong to the word, as screen readers expect.
/// </summary>
public static class TextBoundaries
{
    /// <summary>The characters that end a sentence.</summary>
    private static readonly SearchValues<char> SentenceEnds = SearchValues.Create(".!?…。！？");

    /// <summary>The characters that may follow a sentence end and still belong to that sentence.</summary>
    private static readonly SearchValues<char> SentenceClosers = SearchValues.Create("\"')]}’”»");

    /// <summary>Finds the stretch of a given size that holds an offset.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset; a value at or past the end counts as the end.</param>
    /// <param name="granularity">The size of stretch.</param>
    /// <returns>The stretch, which is empty when the text is empty or the offset is at the end.</returns>
    public static TextSpan At(ReadOnlySpan<char> text, int offset, TextGranularity granularity) =>
        text.IsEmpty || offset < 0 || offset >= text.Length
            ? new(text.Length, text.Length)
            : granularity switch
            {
                TextGranularity.Character => new(offset, offset + 1),
                TextGranularity.Word => Word(text, offset),
                TextGranularity.Sentence => Sentence(text, offset),
                _ => Line(text, offset),
            };

    /// <summary>Finds the stretch just before the one that holds an offset.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="granularity">The size of stretch.</param>
    /// <returns>The stretch, which is empty when the offset is in the first one.</returns>
    public static TextSpan Before(ReadOnlySpan<char> text, int offset, TextGranularity granularity)
    {
        var current = At(text, Math.Clamp(offset, 0, Math.Max(0, text.Length - 1)), granularity);
        return current.Start <= 0 ? new(0, 0) : At(text, current.Start - 1, granularity);
    }

    /// <summary>Finds the stretch just after the one that holds an offset.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="granularity">The size of stretch.</param>
    /// <returns>The stretch, which is empty when the offset is in the last one.</returns>
    public static TextSpan After(ReadOnlySpan<char> text, int offset, TextGranularity granularity)
    {
        var current = At(text, Math.Clamp(offset, 0, Math.Max(0, text.Length - 1)), granularity);
        return current.End >= text.Length ? new(text.Length, text.Length) : At(text, current.End, granularity);
    }

    /// <summary>Finds a word with the spaces and marks after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset inside the text.</param>
    /// <returns>The word.</returns>
    private static TextSpan Word(ReadOnlySpan<char> text, int offset)
    {
        // A line feed ends the word before it, so it is a stretch of its own; looking back from it would find that word again and
        // "next word" would never get past the end of a line.
        if (text[offset] == '\n')
        {
            return new(offset, offset + 1);
        }

        var start = WordStart(text, offset);
        var end = WordEnd(text, offset);

        // A line feed ends the word it follows and is a stretch of its own when nothing else is on the line.
        return new(start, end == start ? start + 1 : end);
    }

    /// <summary>Finds where the word holding an offset starts; spaces and marks belong to the word before them.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset inside the text.</param>
    /// <returns>The offset of the word's first character.</returns>
    private static int WordStart(ReadOnlySpan<char> text, int offset)
    {
        var start = offset;
        while (start > 0 && !IsWordCharacter(text, offset) && !IsWordCharacter(text, start - 1) && text[start - 1] != '\n')
        {
            start--;
        }

        while (start > 0 && IsWordCharacter(text, start - 1))
        {
            start--;
        }

        return start;
    }

    /// <summary>Finds where the word holding an offset ends, after the spaces and marks that follow it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset inside the text.</param>
    /// <returns>The offset just after the stretch.</returns>
    private static int WordEnd(ReadOnlySpan<char> text, int offset)
    {
        var end = offset;
        while (end < text.Length && IsWordCharacter(text, end))
        {
            end++;
        }

        while (end < text.Length && !IsWordCharacter(text, end) && text[end] != '\n')
        {
            end++;
        }

        return end;
    }

    /// <summary>Finds a sentence with the spaces after it.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset inside the text.</param>
    /// <returns>The sentence.</returns>
    private static TextSpan Sentence(ReadOnlySpan<char> text, int offset)
    {
        var start = offset;
        while (start > 0 && !EndsSentence(text, start - 1))
        {
            start--;
        }

        // The spaces after a sentence mark belong to the sentence it ends, so this sentence starts at the next character.
        while (start < offset && char.IsWhiteSpace(text[start]) && text[start] != '\n')
        {
            start++;
        }

        var end = offset;
        while (end < text.Length && !EndsSentence(text, end))
        {
            end++;
        }

        if (end < text.Length)
        {
            end = SkipAfterSentenceEnd(text, end);
        }

        return new(start, end);
    }

    /// <summary>Finds a line, up to and including its line feed.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">An offset inside the text.</param>
    /// <returns>The line.</returns>
    private static TextSpan Line(ReadOnlySpan<char> text, int offset)
    {
        var before = text[..offset].LastIndexOf('\n');
        var start = before < 0 ? 0 : before + 1;
        var feed = text[offset..].IndexOf('\n');
        return new(start, feed < 0 ? text.Length : offset + feed + 1);
    }

    /// <summary>Checks whether the character at an index is part of a word.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns>Whether it is a letter, a digit or an apostrophe between two of those.</returns>
    private static bool IsWordCharacter(ReadOnlySpan<char> text, int index)
    {
        var c = text[index];
        if (char.IsLetterOrDigit(c) || c == '_')
        {
            return true;
        }

        var apostrophe = c is '\'' or '’';
        return apostrophe && index > 0 && index + 1 < text.Length && char.IsLetterOrDigit(text[index - 1]) && char.IsLetterOrDigit(text[index + 1]);
    }

    /// <summary>Checks whether the character at an index is the end of a sentence.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns>Whether a sentence ends here: a sentence mark followed by a space, or a line feed.</returns>
    private static bool EndsSentence(ReadOnlySpan<char> text, int index)
    {
        var c = text[index];
        if (c == '\n')
        {
            return true;
        }

        if (!SentenceEnds.Contains(c))
        {
            return false;
        }

        var next = SkipClosers(text, index + 1);
        return next >= text.Length || char.IsWhiteSpace(text[next]);
    }

    /// <summary>Moves past a sentence end, the closing marks after it and the spaces that follow.</summary>
    /// <param name="text">The text.</param>
    /// <param name="end">The index of the sentence end.</param>
    /// <returns>The index of the start of the next sentence.</returns>
    private static int SkipAfterSentenceEnd(ReadOnlySpan<char> text, int end)
    {
        var index = text[end] == '\n' ? end + 1 : SkipClosers(text, end + 1);
        while (index < text.Length && char.IsWhiteSpace(text[index]) && text[index] != '\n')
        {
            index++;
        }

        // The line feed that ends a sentence belongs to it, so the next sentence starts on the following line.
        return index < text.Length && text[index] == '\n' ? index + 1 : index;
    }

    /// <summary>Moves past closing quotes and brackets.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index to start from.</param>
    /// <returns>The index after them.</returns>
    private static int SkipClosers(ReadOnlySpan<char> text, int index)
    {
        while (index < text.Length && SentenceClosers.Contains(text[index]))
        {
            index++;
        }

        return index;
    }
}
