// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Plans the order of pages taken from several documents; <see cref="PdfDocumentAssembly"/> builds the document.</summary>
public static class PdfDocumentCollation
{
    /// <summary>Lists every page of each document in turn: all of the first, then all of the second, and so on.</summary>
    /// <param name="pageCounts">The number of pages in each document.</param>
    /// <returns>The pages in order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    public static PdfPageReference[] Concatenate(ReadOnlySpan<int> pageCounts)
    {
        var references = new List<PdfPageReference>(Total(pageCounts));
        for (var source = 0; source < pageCounts.Length; source++)
        {
            for (var page = 0; page < pageCounts[source]; page++)
            {
                references.Add(new(source, page));
            }
        }

        return [.. references];
    }

    /// <summary>
    /// Lists pages by taking a group from each document in turn, then going round again, as in a collated print job. A
    /// document that runs out is skipped; the rest carry on until every page is listed.
    /// </summary>
    /// <param name="pageCounts">The number of pages in each document.</param>
    /// <param name="groupSize">The pages taken from a document on each turn.</param>
    /// <returns>The pages in order.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative or the group size is not positive.</exception>
    public static PdfPageReference[] Interleave(ReadOnlySpan<int> pageCounts, int groupSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(groupSize);
        var total = Total(pageCounts);
        var references = new List<PdfPageReference>(total);
        var next = new int[pageCounts.Length];
        var listed = 0;
        while (listed < total)
        {
            for (var source = 0; source < pageCounts.Length; source++)
            {
                var end = Math.Min(next[source] + groupSize, pageCounts[source]);
                for (; next[source] < end; next[source]++)
                {
                    references.Add(new(source, next[source]));
                    listed++;
                }
            }
        }

        return [.. references];
    }

    /// <summary>Adds up page counts.</summary>
    /// <param name="pageCounts">The counts.</param>
    /// <returns>The total.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A count is negative.</exception>
    private static int Total(ReadOnlySpan<int> pageCounts)
    {
        var total = 0;
        foreach (var count in pageCounts)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            total = checked(total + count);
        }

        return total;
    }
}
