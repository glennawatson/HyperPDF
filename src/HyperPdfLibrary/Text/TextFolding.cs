// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Text;

/// <summary>
/// Folds characters so a search can treat variants as equal. Each folded character comes from one source character, or
/// is dropped (combining marks), so a match in folded text maps back to the page text through an index table.
/// Order: diacritics, then compatibility variants, then case.
/// </summary>
internal static class TextFolding
{
    /// <summary>The options that change how text is folded.</summary>
    internal const PdfTextSearchOptions FoldOptions = PdfTextSearchOptions.MatchCase | PdfTextSearchOptions.IgnoreDiacritics | PdfTextSearchOptions.FoldCompatibility;

    /// <summary>The offset between a fullwidth ASCII variant (U+FF01 to U+FF5E) and its ASCII character.</summary>
    private const int FullwidthOffset = 0xFEE0;

    /// <summary>The first fullwidth ASCII variant.</summary>
    private const char FullwidthFirst = (char)0xFF01;

    /// <summary>The last fullwidth ASCII variant.</summary>
    private const char FullwidthLast = (char)0xFF5E;

    /// <summary>Gets the case-folded and fold-option-folded text, with a map from folded index to source index.</summary>
    /// <param name="text">The source text.</param>
    /// <param name="options">The search options; only the folding options matter.</param>
    /// <returns>The folded text.</returns>
    internal static FoldedText Fold(string text, PdfTextSearchOptions options)
    {
        var buffer = ArrayPool<char>.Shared.Rent(text.Length);
        int[]? map = null;
        try
        {
            var count = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (!TryFold(text[i], options, out var folded))
                {
                    map ??= IdentityMap(text.Length, count);
                    continue;
                }

                buffer[count] = folded;
                if (map is not null)
                {
                    map[count] = i;
                }

                count++;
            }

            // Text that folds to itself keeps its own string instead of a copy.
            var result = buffer.AsSpan(0, count);
            var foldedText = result.SequenceEqual(text) ? text : new string(result);
            return new(foldedText, map is null ? null : map.AsSpan(0, count).ToArray());
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
            if (map is not null)
            {
                ArrayPool<int>.Shared.Return(map);
            }
        }
    }

    /// <summary>Folds a query into a buffer.</summary>
    /// <param name="source">The query.</param>
    /// <param name="destination">The buffer, at least as long as the query.</param>
    /// <param name="options">The search options.</param>
    /// <returns>The folded length.</returns>
    internal static int Fold(ReadOnlySpan<char> source, Span<char> destination, PdfTextSearchOptions options)
    {
        var count = 0;
        foreach (var value in source)
        {
            if (!TryFold(value, options, out var folded))
            {
                continue;
            }

            destination[count] = folded;
            count++;
        }

        return count;
    }

    /// <summary>Folds one character.</summary>
    /// <param name="value">The character.</param>
    /// <param name="options">The search options.</param>
    /// <param name="folded">The folded character.</param>
    /// <returns><see langword="false"/> when the character is dropped.</returns>
    internal static bool TryFold(char value, PdfTextSearchOptions options, out char folded)
    {
        folded = value;
        if ((options & PdfTextSearchOptions.IgnoreDiacritics) != 0)
        {
            if (TextUnicode.IsCombiningMark(value))
            {
                return false;
            }

            folded = TextUnicode.GetBaseLetter(folded);
        }

        if ((options & PdfTextSearchOptions.FoldCompatibility) != 0)
        {
            folded = FoldCompatibilityVariant(folded);
        }

        if ((options & PdfTextSearchOptions.MatchCase) == 0)
        {
            folded = char.ToLowerInvariant(folded);
        }

        return true;
    }

    /// <summary>Maps typographic and fullwidth variants to the plain character.</summary>
    /// <param name="value">The character.</param>
    /// <returns>The plain character, or the character itself.</returns>
    private static char FoldCompatibilityVariant(char value) => value switch
    {
        >= FullwidthFirst and <= FullwidthLast => (char)(value - FullwidthOffset),
        _ when IsSpaceVariant(value) => ' ',
        _ when IsSingleQuoteVariant(value) => '\'',
        >= '“' and <= '‟' => '"',
        _ when IsDashVariant(value) => '-',
        _ => value,
    };

    /// <summary>Determines whether a character is a no-break, typographic or ideographic space.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those spaces.</returns>
    private static bool IsSpaceVariant(char value) => value is ' ' or (>= ' ' and <= ' ') or ' ' or ' ' or '　';

    /// <summary>Determines whether a character is a curly or low single quote.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those quotes.</returns>
    private static bool IsSingleQuoteVariant(char value) => value is >= '‘' and <= '‛';

    /// <summary>Determines whether a character is a hyphen, en dash, em dash, bar or minus.</summary>
    /// <param name="value">The character.</param>
    /// <returns><see langword="true"/> for those dashes.</returns>
    private static bool IsDashVariant(char value) => value is (>= '‐' and <= '―') or '−' or '﹘' or '﹣' or '－';

    /// <summary>Makes a map where the first entries point at themselves.</summary>
    /// <param name="length">The map length.</param>
    /// <param name="filled">The number of leading entries to fill.</param>
    /// <returns>The map.</returns>
    private static int[] IdentityMap(int length, int filled)
    {
        var map = ArrayPool<int>.Shared.Rent(length);
        for (var i = 0; i < filled; i++)
        {
            map[i] = i;
        }

        return map;
    }
}
