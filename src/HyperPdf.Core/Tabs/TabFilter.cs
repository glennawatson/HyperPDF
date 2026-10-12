// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

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
        foreach (var range in query.SplitAny(" \t"))
        {
            var word = query[range];
            if (word.IsEmpty)
            {
                continue;
            }

            if (Compare.IndexOf(fileName, word, Options) < 0 && Compare.IndexOf(title, word, Options) < 0 && Compare.IndexOf(folder, word, Options) < 0)
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
}
