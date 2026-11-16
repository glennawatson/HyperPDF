// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Works out the ranges of page labels that result from numbering some pages in a new way while every other page keeps its label.</summary>
internal static class PageLabelMerger
{
    /// <summary>The most ranges a change adds: the numbered pages' own, one to carry on after them, and one to keep the pages before them as they were.</summary>
    private const int MostAdded = 3;

    /// <summary>Numbers the pages from one to another in a new way.</summary>
    /// <param name="existing">The ranges the document has, in page order; empty when pages are numbered 1, 2, 3.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="from">The zero based first page to number.</param>
    /// <param name="to">The zero based last page to number.</param>
    /// <param name="style">How the number is written.</param>
    /// <param name="prefix">Text written before the number.</param>
    /// <param name="firstNumber">The number of the first page.</param>
    /// <returns>The ranges to set, in page order, starting on the first page.</returns>
    internal static List<PageLabelRun> Apply(IReadOnlyList<PageLabelRun> existing, int pageCount, int from, int to, PageLabelStyle style, string prefix, int firstNumber)
    {
        var result = new List<PageLabelRun>(existing.Count + MostAdded);
        foreach (var run in existing)
        {
            if (run.StartPage < from)
            {
                result.Add(run);
            }
        }

        if (from > 0 && result.Count == 0)
        {
            result.Add(new(0, PageLabelStyle.Decimal, string.Empty, 1));
        }

        result.Add(new(from, style, prefix, firstNumber));
        var next = to + 1;
        if (next < pageCount && !StartsAt(existing, next))
        {
            result.Add(Continue(existing, next));
        }

        foreach (var run in existing)
        {
            if (run.StartPage > to)
            {
                result.Add(run);
            }
        }

        return result;
    }

    /// <summary>Tells whether a range starts on a page.</summary>
    /// <param name="ranges">The ranges.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns><see langword="true"/> when one starts there.</returns>
    private static bool StartsAt(IReadOnlyList<PageLabelRun> ranges, int page)
    {
        foreach (var run in ranges)
        {
            if (run.StartPage == page)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Makes the range that gives a page the label it has now and carries on from there.</summary>
    /// <param name="ranges">The ranges the document has.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns>The range.</returns>
    private static PageLabelRun Continue(IReadOnlyList<PageLabelRun> ranges, int page)
    {
        PageLabelRun? found = null;
        foreach (var run in ranges)
        {
            if (run.StartPage <= page)
            {
                found = run;
            }
        }

        return found is { } current
            ? new(page, current.Style, current.Prefix, current.FirstNumber + (page - current.StartPage))
            : new(page, PageLabelStyle.Decimal, string.Empty, page + 1);
    }
}
