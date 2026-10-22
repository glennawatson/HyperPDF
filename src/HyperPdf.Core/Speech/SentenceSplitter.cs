// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdf.Core.Speech;

/// <summary>
/// Splits a page's text into sentences for reading aloud, keeping their positions for the follow-along highlight. A
/// sentence ends at <c>.</c>, <c>!</c> or <c>?</c> before white space (not after a common abbreviation), at a blank
/// line, or after a heading (a short line of its own without end punctuation, followed by a capital); very long runs
/// are split at a comma or space so the voice never waits long. Allocates nothing beyond the list.
/// </summary>
public static class SentenceSplitter
{
    /// <summary>The longest sentence spoken in one piece, in characters.</summary>
    private const int MaxLength = 300;

    /// <summary>The longest sentence prepared on the stack, in characters; a sentence is normally under <see cref="MaxLength"/>.</summary>
    private const int StackSentence = 512;

    /// <summary>The longest line taken for a heading, in characters.</summary>
    private const int MaxHeadingLength = 40;

    /// <summary>Characters with nothing to say: spaces and punctuation.</summary>
    private static readonly SearchValues<char> Silent = SearchValues.Create(" \r\n\t.,;:!?-—…\"'()[]");

    /// <summary>Characters that end a sentence.</summary>
    private static readonly SearchValues<char> Terminators = SearchValues.Create(".!?");

    /// <summary>Characters a sentence can end at whatever its length: terminators and the line feed that ends a line.</summary>
    private static readonly SearchValues<char> EndCandidates = SearchValues.Create(".!?\n");

    /// <summary>Characters a run too long to speak in one piece is split after.</summary>
    private static readonly SearchValues<char> LongRunBreaks = SearchValues.Create(",; ");

    /// <summary>Words that end with a full stop without ending the sentence.</summary>
    private static readonly string[] Abbreviations =
    [
        "mr", "mrs", "ms", "dr", "prof", "st", "vs", "etc", "e.g", "i.e", "fig", "no", "vol", "p", "pp",
        "inc", "ltd", "jr", "sr", "a.m", "p.m", "approx", "dept", "est",
    ];

    /// <summary>Splits text into sentences.</summary>
    /// <param name="text">The text.</param>
    /// <param name="sentences">The list receiving the sentences; cleared first.</param>
    public static void Split(ReadOnlySpan<char> text, List<SpeechSentence> sentences)
    {
        ArgumentNullException.ThrowIfNull(sentences);
        sentences.Clear();
        var start = SkipSpace(text, 0);
        var i = NextCandidate(text, start, start);
        while (i < text.Length)
        {
            if (EndsSentence(text, i, start))
            {
                Add(text, start, i + 1, sentences);
                start = SkipSpace(text, i + 1);
                i = NextCandidate(text, start, start);
            }
            else
            {
                i = NextCandidate(text, i + 1, start);
            }
        }

        Add(text, start, text.Length, sentences);
    }

    /// <summary>Prepares a sentence for the voice: joins words broken over lines and makes line breaks spaces.</summary>
    /// <param name="sentence">The sentence as it appears on the page.</param>
    /// <returns>The text to speak.</returns>
    public static string ToSpeech(ReadOnlySpan<char> sentence)
    {
        // The prepared text is never longer than the sentence, so one buffer holds it; only a very long one is rented.
        char[]? rented = null;
        Span<char> buffer = sentence.Length <= StackSentence ? stackalloc char[StackSentence] : (rented = ArrayPool<char>.Shared.Rent(sentence.Length));
        try
        {
            return new(buffer[..Prepare(sentence, buffer)].Trim());
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Writes a sentence with words broken over lines joined and every other run of white space as one space.</summary>
    /// <param name="sentence">The sentence as it appears on the page.</param>
    /// <param name="buffer">Receives the text; at least as long as the sentence.</param>
    /// <returns>The characters written.</returns>
    private static int Prepare(ReadOnlySpan<char> sentence, Span<char> buffer)
    {
        var length = 0;
        for (var i = 0; i < sentence.Length; i++)
        {
            var c = sentence[i];
            if (c == '-' && IsLineBreak(sentence, i + 1))
            {
                // A word hyphenated at the end of a line: drop the hyphen and the break.
                i = SkipLineBreaks(sentence, i + 1) - 1;
                continue;
            }

            if (!char.IsWhiteSpace(c) && !char.IsControl(c))
            {
                buffer[length] = c;
                length++;
            }
            else if (length > 0 && buffer[length - 1] != ' ')
            {
                buffer[length] = ' ';
                length++;
            }
        }

        return length;
    }

    /// <summary>Determines whether a line break is at an index.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> for a carriage return or line feed.</returns>
    private static bool IsLineBreak(ReadOnlySpan<char> text, int index) => index < text.Length && text[index] is '\r' or '\n';

    /// <summary>Skips line breaks.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The first character after them.</returns>
    private static int SkipLineBreaks(ReadOnlySpan<char> text, int index)
    {
        while (IsLineBreak(text, index))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Finds the next character a sentence can end at: a terminator or line feed, or, once the sentence is long enough to be
    /// split, a comma, semicolon or space. Every other character cannot end one, so <see cref="EndsSentence"/> is skipped for it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="from">Where to look from.</param>
    /// <param name="start">Where the sentence started.</param>
    /// <returns>The index of the candidate, or the text's length when there is none.</returns>
    private static int NextCandidate(ReadOnlySpan<char> text, int from, int start)
    {
        if (from >= text.Length)
        {
            return text.Length;
        }

        var found = text[from..].IndexOfAny(EndCandidates);
        var end = found < 0 ? text.Length : from + found;
        var splittableFrom = Math.Max(from, start + MaxLength - 1);
        if (splittableFrom >= end)
        {
            return end;
        }

        var breakable = text[splittableFrom..end].IndexOfAny(LongRunBreaks);
        return breakable < 0 ? end : splittableFrom + breakable;
    }

    /// <summary>Determines whether a sentence ends at a character.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The character.</param>
    /// <param name="start">Where the sentence started.</param>
    /// <returns><see langword="true"/> when it ends there.</returns>
    private static bool EndsSentence(ReadOnlySpan<char> text, int index, int start)
    {
        var next = index + 1 < text.Length ? text[index + 1] : ' ';
        var terminated = Terminators.Contains(text[index]) && char.IsWhiteSpace(next) && !IsAbbreviation(text, index);

        // A very long run is split where it can be.
        var tooLong = index - start >= MaxLength - 1 && IsBreakable(text[index]);
        return terminated || tooLong || EndsLine(text, index, start);
    }

    /// <summary>
    /// Determines whether a line break ends a sentence: a blank line ends a paragraph, and a heading such as "Page 1" or
    /// "Introduction" is read on its own.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The character, a line feed for a sentence to end.</param>
    /// <param name="start">Where the sentence started; a heading is a whole line from there.</param>
    /// <returns><see langword="true"/> before a blank line, or after a short line without end punctuation followed by a capital or digit.</returns>
    private static bool EndsLine(ReadOnlySpan<char> text, int index, int start)
    {
        if (text[index] != '\n' || index <= start)
        {
            return false;
        }

        if (IsLineBreak(text, index + 1))
        {
            return true;
        }

        var line = text[start..index].TrimEnd();
        return line.Length is > 0 and <= MaxHeadingLength && !line.ContainsAny('\n', '\r') && char.IsLetterOrDigit(line[^1]) && StartsWithCapital(text, index + 1);
    }

    /// <summary>Determines whether the next words start with a capital letter or a digit.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to look from.</param>
    /// <returns><see langword="true"/> for a capital or digit after any white space.</returns>
    private static bool StartsWithCapital(ReadOnlySpan<char> text, int index)
    {
        var next = SkipSpace(text, index);
        return next < text.Length && (char.IsUpper(text[next]) || char.IsDigit(text[next]));
    }

    /// <summary>Determines whether a long run can be split after a character.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for commas, semicolons and spaces.</returns>
    private static bool IsBreakable(char value) => value is ',' or ';' or ' ';

    /// <summary>Determines whether a full stop ends an abbreviation such as "Dr.".</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">The full stop.</param>
    /// <returns><see langword="true"/> for an abbreviation.</returns>
    private static bool IsAbbreviation(ReadOnlySpan<char> text, int index)
    {
        if (text[index] != '.')
        {
            return false;
        }

        var wordStart = index;
        while (wordStart > 0 && !char.IsWhiteSpace(text[wordStart - 1]))
        {
            wordStart--;
        }

        var word = text[wordStart..index].TrimStart("([\"'“");
        foreach (var abbreviation in Abbreviations)
        {
            if (word.Equals(abbreviation, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // A single capital letter, as in an initial: "J. Smith".
        return word.Length == 1 && char.IsUpper(word[0]);
    }

    /// <summary>Skips white space.</summary>
    /// <param name="text">The text.</param>
    /// <param name="index">Where to start.</param>
    /// <returns>The next non-space character.</returns>
    private static int SkipSpace(ReadOnlySpan<char> text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Adds a sentence when it has something to say.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The first character.</param>
    /// <param name="end">The character after the last.</param>
    /// <param name="sentences">The list.</param>
    private static void Add(ReadOnlySpan<char> text, int start, int end, List<SpeechSentence> sentences)
    {
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        if (end > start && text[start..end].ContainsAnyExcept(Silent))
        {
            sentences.Add(new(start, end - start));
        }
    }
}
