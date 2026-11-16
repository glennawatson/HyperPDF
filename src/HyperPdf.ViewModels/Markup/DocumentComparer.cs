// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Compares the words of two documents page by page.</summary>
internal static class DocumentComparer
{
    /// <summary>Finds the pages whose words differ.</summary>
    /// <param name="current">The open document.</param>
    /// <param name="other">The document to compare it with.</param>
    /// <param name="cancellationToken">Stops the comparison.</param>
    /// <returns>The pages that differ, in page order; a page one document lacks counts all its words.</returns>
    internal static List<ComparedPage> Compare(IDocument current, IDocument other, CancellationToken cancellationToken)
    {
        var result = new List<ComparedPage>();
        var pages = Math.Max(current.PageCount, other.PageCount);
        for (var page = 0; page < pages; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = page < current.PageCount ? Count(current, page) : [];
            var after = page < other.PageCount ? Count(other, page) : [];
            var removed = Surplus(before, after);
            var added = Surplus(after, before);
            if (added + removed > 0)
            {
                result.Add(new(page, added, removed));
            }
        }

        return result;
    }

    /// <summary>Counts how often each word appears on a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns>The count of each word.</returns>
    private static Dictionary<string, int> Count(IDocument document, int page)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var text = document.GetText(page, 0, document.GetCharacterCount(page));
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            counts[word] = counts.GetValueOrDefault(word) + 1;
        }

        return counts;
    }

    /// <summary>Counts the words the first page has more of than the second.</summary>
    /// <param name="first">The words of the first page.</param>
    /// <param name="second">The words of the second page.</param>
    /// <returns>The number of surplus words.</returns>
    private static int Surplus(Dictionary<string, int> first, Dictionary<string, int> second)
    {
        var surplus = 0;
        foreach (var (word, count) in first)
        {
            surplus += Math.Max(0, count - second.GetValueOrDefault(word));
        }

        return surplus;
    }
}
