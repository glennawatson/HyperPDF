// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.Core.Tabs;

/// <summary>
/// Matches open tabs against what the user types in the tab finder. Every word of the query must appear, in any
/// order, in the file name, title or folder, ignoring case and accents. Matching allocates nothing.
/// </summary>
public static class TabFilter
{
    /// <summary>The comparison: case and accent insensitive, culture aware.</summary>
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Gets the comparer used for matching.</summary>
    private static CompareInfo Compare => CultureInfo.CurrentCulture.CompareInfo;

    /// <summary>Determines whether a tab matches a query.</summary>
    /// <param name="query">The query; empty matches everything.</param>
    /// <param name="fileName">The file name.</param>
    /// <param name="title">The document title.</param>
    /// <param name="folder">The folder path.</param>
    /// <returns><see langword="true"/> when every word of the query appears in one of the fields.</returns>
    public static bool Matches(ReadOnlySpan<char> query, ReadOnlySpan<char> fileName, ReadOnlySpan<char> title, ReadOnlySpan<char> folder)
    {
        var compare = Compare;
        var ordinalSafe = IsOrdinalSafe(compare);
        foreach (var range in query.SplitAny(" \t"))
        {
            var word = query[range];
            if (word.IsEmpty)
            {
                continue;
            }

            var asciiWord = ordinalSafe && Ascii.IsValid(word);
            if (IndexOf(compare, asciiWord, fileName, word) < 0 && IndexOf(compare, asciiWord, title, word) < 0 && IndexOf(compare, asciiWord, folder, word) < 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Writes the indexes of the matching tabs into <paramref name="output"/>.</summary>
    /// <param name="query">The query.</param>
    /// <param name="tabs">The tabs.</param>
    /// <param name="output">Receives the matching indexes; must be at least as long as <paramref name="tabs"/>.</param>
    /// <returns>The number of matches.</returns>
    /// <exception cref="ArgumentException"><paramref name="output"/> is shorter than <paramref name="tabs"/>.</exception>
    public static int Filter(ReadOnlySpan<char> query, ReadOnlySpan<TabSummary> tabs, Span<int> output)
    {
        if (output.Length < tabs.Length)
        {
            throw new ArgumentException("The output must be as long as the tab list.", nameof(output));
        }

        var count = 0;
        for (var i = 0; i < tabs.Length; i++)
        {
            ref readonly var tab = ref tabs[i];
            if (!Matches(query, tab.FileName, tab.Title, tab.Folder))
            {
                continue;
            }

            output[count] = i;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Determines whether the culture's rules for plain ASCII text are the same as an ordinal comparison that ignores case.
    /// English and the invariant culture have no contractions and no special casing; Turkish and Czech, for example, do.
    /// </summary>
    /// <param name="compare">The culture's comparer.</param>
    /// <returns><see langword="true"/> when ASCII text may be compared ordinally.</returns>
    private static bool IsOrdinalSafe(CompareInfo compare) => compare.Name.Length == 0 || compare.Name.StartsWith("en", StringComparison.Ordinal);

    /// <summary>Finds a word in a field, skipping the culture compare when both are plain ASCII.</summary>
    /// <param name="compare">The culture's comparer.</param>
    /// <param name="asciiWord">Whether the word is ASCII and ASCII text may be compared ordinally.</param>
    /// <param name="field">The field to search.</param>
    /// <param name="word">The word.</param>
    /// <returns>The index of the word, or -1.</returns>
    private static int IndexOf(CompareInfo compare, bool asciiWord, ReadOnlySpan<char> field, ReadOnlySpan<char> word) =>
        asciiWord && Ascii.IsValid(field) ? field.IndexOf(word, StringComparison.OrdinalIgnoreCase) : compare.IndexOf(field, word, Options);
}
