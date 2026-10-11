// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Text;

/// <content>Searching the page text.</content>
public sealed partial class PdfTextPage
{
    /// <summary>The longest query lowered on the stack.</summary>
    private const int StackQueryChars = 128;

    /// <summary>The words more than the query's length a split query can have: a leading and a trailing empty word.</summary>
    private const int ExtraWords = 2;

    /// <summary>The page text in lower case, made on the first case-insensitive search.</summary>
    private string? _lowerText;

    /// <summary>Appends every match of a query, as PDFium's FPDFText_FindStart and FPDFText_FindNext find them from the start of the page.</summary>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="output">The list receiving the matches.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
    public void Find(ReadOnlySpan<char> query, PdfTextSearchOptions options, List<PdfTextMatch> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Find(query, options, output, static (matches, start, count) => matches.Add(new(start, count)));
    }

    /// <summary>Visits every match of a query without an intermediate list.</summary>
    /// <typeparam name="TState">The caller's state passed to each visit.</typeparam>
    /// <param name="query">The text to find.</param>
    /// <param name="options">The search options.</param>
    /// <param name="state">The caller's state.</param>
    /// <param name="visit">Receives each match's first character and length.</param>
    public void Find<TState>(ReadOnlySpan<char> query, PdfTextSearchOptions options, TState state, Action<TState, int, int> visit)
    {
        ArgumentNullException.ThrowIfNull(visit);
        if (query.IsEmpty || _text.Length == 0)
        {
            return;
        }

        var matchCase = (options & PdfTextSearchOptions.MatchCase) != 0;
        var text = matchCase ? _text : LowerText();
        char[]? rentedQuery = null;
        var lowered = query.Length <= StackQueryChars ? stackalloc char[StackQueryChars] : (rentedQuery = ArrayPool<char>.Shared.Rent(query.Length));
        var words = ArrayPool<TextSegment>.Shared.Rent(query.Length + ExtraWords);
        try
        {
            lowered = lowered[..query.Length];
            if (matchCase)
            {
                query.CopyTo(lowered);
            }
            else
            {
                _ = query.ToLowerInvariant(lowered);
            }

            var count = TextFinder.SplitWords(lowered, words);
            var finder = new TextFinder(text, lowered, words.AsSpan(0, count), options);
            while (finder.FindNext())
            {
                var start = CharIndexFromTextIndex(finder.MatchStart);
                visit(state, start, CharIndexFromTextIndex(finder.MatchEnd) - start + 1);
            }
        }
        finally
        {
            ArrayPool<TextSegment>.Shared.Return(words);
            if (rentedQuery is not null)
            {
                ArrayPool<char>.Shared.Return(rentedQuery);
            }
        }
    }

    /// <summary>Gets the page text in lower case, making it once.</summary>
    /// <returns>The lowered text.</returns>
    private string LowerText()
    {
        if (Volatile.Read(ref _lowerText) is { } existing)
        {
            return existing;
        }

        var lowered = string.Create(_text.Length, _text, static (destination, source) => source.AsSpan().ToLowerInvariant(destination));
        _ = Interlocked.CompareExchange(ref _lowerText, lowered, null);
        return Volatile.Read(ref _lowerText)!;
    }
}
