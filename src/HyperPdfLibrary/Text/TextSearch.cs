// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Finds a query in folded page text. Any run of white space in the query matches any run of white space in the text, line
/// breaks included, and white space at the ends of the query is ignored. Matches are found from the start of the text;
/// each begins after the previous one ends, or after the previous one's first character when matches may overlap.
/// </summary>
internal static class TextSearch
{
    /// <summary>The longest query folded on the stack.</summary>
    private const int StackQueryChars = 128;

    /// <summary>Finds every match.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="folded">The folded page text.</param>
    /// <param name="query">The query, not yet folded.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives the page text index of each match's first and last character.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FindAll<TState>(FoldedText folded, ReadOnlySpan<char> query, PdfTextSearchOptions options, TState state, Action<TState, int, int> visit) =>
        FindAll(new FoldedView(folded.Text, folded.Map, folded.SourceLength), query, options, state, visit);

    /// <summary>Finds every match in folded text held in caller-owned buffers.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="folded">The folded page text.</param>
    /// <param name="query">The query, not yet folded.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives the page text index of each match's first and last character.</param>
    internal static void FindAll<TState>(FoldedView folded, ReadOnlySpan<char> query, PdfTextSearchOptions options, TState state, Action<TState, int, int> visit)
    {
        char[]? rented = null;
        var capacity = TextNormalization.DecomposedLength(query, (options & PdfTextSearchOptions.FoldCompatibility) != 0);
        var buffer = capacity <= StackQueryChars ? stackalloc char[StackQueryChars] : (rented = ArrayPool<char>.Shared.Rent(capacity));
        try
        {
            var length = TextFolding.Fold(query, buffer, options);
            var pattern = buffer[..CollapseWhiteSpace(buffer[..length])];
            if (!pattern.IsEmpty)
            {
                Scan(folded, pattern, options, state, visit);
            }
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Replaces each run of white space with one space and removes white space at both ends.</summary>
    /// <param name="pattern">The pattern, changed in place.</param>
    /// <returns>The new length.</returns>
    private static int CollapseWhiteSpace(Span<char> pattern)
    {
        var count = 0;
        foreach (var value in pattern)
        {
            var keep = !char.IsWhiteSpace(value);
            if (!keep && (count == 0 || pattern[count - 1] == ' '))
            {
                continue;
            }

            pattern[count] = keep ? value : ' ';
            count++;
        }

        return count > 0 && pattern[count - 1] == ' ' ? count - 1 : count;
    }

    /// <summary>Walks the text, reporting each match.</summary>
    /// <typeparam name="TState">The type of the state handed to each visit.</typeparam>
    /// <param name="folded">The folded text.</param>
    /// <param name="pattern">The folded pattern, which starts with a character that is not white space.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The state handed to each visit.</param>
    /// <param name="visit">Receives the first and last text index of each match.</param>
    private static void Scan<TState>(FoldedView folded, ReadOnlySpan<char> pattern, PdfTextSearchOptions options, TState state, Action<TState, int, int> visit)
    {
        var text = folded.Text;
        var overlap = (options & PdfTextSearchOptions.Consecutive) != 0;
        var wholeWord = (options & PdfTextSearchOptions.WholeWord) != 0;
        var from = 0;
        while (from < text.Length)
        {
            var found = text[from..].IndexOf(pattern[0]);
            if (found < 0)
            {
                return;
            }

            var start = from + found;
            if (!TryMatch(text, start, pattern, out var end) || !IsAcceptable(text, start, end, pattern, wholeWord))
            {
                from = start + 1;
                continue;
            }

            visit(state, folded.StartOf(start), folded.EndOf(end - 1));
            from = overlap ? start + 1 : end;
        }
    }

    /// <summary>Matches the pattern at a position.</summary>
    /// <param name="text">The folded text.</param>
    /// <param name="start">The position.</param>
    /// <param name="pattern">The folded pattern.</param>
    /// <param name="end">Receives the position after the match.</param>
    /// <returns><see langword="true"/> when the text matches.</returns>
    private static bool TryMatch(ReadOnlySpan<char> text, int start, ReadOnlySpan<char> pattern, out int end)
    {
        end = start;
        foreach (var value in pattern)
        {
            if (end >= text.Length)
            {
                return false;
            }

            if (value != ' ')
            {
                if (text[end] != value)
                {
                    return false;
                }

                end++;
                continue;
            }

            if (!char.IsWhiteSpace(text[end]))
            {
                return false;
            }

            while (end < text.Length && char.IsWhiteSpace(text[end]))
            {
                end++;
            }
        }

        return true;
    }

    /// <summary>
    /// Determines whether a match counts: it must not split a letter from its mark, must not start inside a conjunct, and
    /// with whole-word search it must not be part of a longer word.
    /// </summary>
    /// <param name="text">The folded text.</param>
    /// <param name="start">The match start.</param>
    /// <param name="end">The position after the match.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="wholeWord">Whether only whole words match.</param>
    /// <returns><see langword="true"/> when the match is reported.</returns>
    private static bool IsAcceptable(ReadOnlySpan<char> text, int start, int end, ReadOnlySpan<char> pattern, bool wholeWord) =>
        !SplitsLetter(text, end) && !TextClusters.IsInsideConjunct(text, start) && (!wholeWord || IsWholeWord(text, start, end, pattern));

    /// <summary>
    /// Determines whether a match ends between a letter and a mark that belongs to it, as "e" would in "é", which text and
    /// query hold as "e" and an acute accent. Such a match is a different letter, so it does not count.
    /// </summary>
    /// <param name="text">The folded text.</param>
    /// <param name="end">The position after the match.</param>
    /// <returns><see langword="true"/> when a mark follows the match.</returns>
    private static bool SplitsLetter(ReadOnlySpan<char> text, int end) => end < text.Length && TextUnicode.IsMark(text[end]);

    /// <summary>Determines whether a match is not part of a longer word.</summary>
    /// <param name="text">The folded text.</param>
    /// <param name="start">The match start.</param>
    /// <param name="end">The position after the match.</param>
    /// <param name="pattern">The pattern.</param>
    /// <returns><see langword="true"/> when the characters around the match are not word characters.</returns>
    private static bool IsWholeWord(ReadOnlySpan<char> text, int start, int end, ReadOnlySpan<char> pattern)
    {
        if (TextUnicode.IsWordChar(pattern[0]) && start > 0 && TextUnicode.IsWordChar(text[start - 1]))
        {
            return false;
        }

        return !(TextUnicode.IsWordChar(pattern[^1]) && end < text.Length && TextUnicode.IsWordChar(text[end]));
    }
}
