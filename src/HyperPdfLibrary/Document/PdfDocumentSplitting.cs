// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Splits a document into several documents, each holding a run of pages. A part is made like
/// <see cref="PdfDocumentPageOperations.ExtractPages(PdfDocument, ReadOnlySpan{int})"/>: it keeps the pages' annotations,
/// form fields, outline entries, destinations, labels, layers and embedded file names that belong to them.
/// </summary>
public static class PdfDocumentSplitting
{
    /// <summary>Plans parts of a fixed number of pages; the last part may be shorter.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pagesPerPart">The pages in each part.</param>
    /// <returns>The parts, in page order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pagesPerPart"/> is not positive.</exception>
    public static PdfSplitPart[] PlanByPageCount(PdfDocument document, int pagesPerPart)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pagesPerPart);
        var parts = new PdfSplitPart[(document.PageCount + pagesPerPart - 1) / pagesPerPart];
        for (var i = 0; i < parts.Length; i++)
        {
            var first = i * pagesPerPart;
            parts[i] = new(null, first, Math.Min(pagesPerPart, document.PageCount - first));
        }

        return parts;
    }

    /// <summary>
    /// Plans one part for each top-level outline entry that goes to a page: the part runs from that page to the page before
    /// the next entry's. Pages before the first entry form a part without a title. A document with no such entry is one part.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The parts, in page order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfSplitPart[] PlanByOutline(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var starts = ReadStarts(document);
        var parts = new List<PdfSplitPart>(starts.Count + 1);
        if (starts.Count == 0 || starts[0].Page > 0)
        {
            parts.Add(new(null, 0, starts.Count == 0 ? document.PageCount : starts[0].Page));
        }

        for (var i = 0; i < starts.Count; i++)
        {
            var end = i + 1 < starts.Count ? starts[i + 1].Page : document.PageCount;
            parts.Add(new(starts[i].Title, starts[i].Page, end - starts[i].Page));
        }

        return [.. parts];
    }

    /// <summary>Makes a document from one part.</summary>
    /// <param name="document">The document.</param>
    /// <param name="part">The part.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The PDF bytes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The part does not lie inside the document.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] ExtractPart(PdfDocument document, PdfSplitPart part, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return PdfDocumentPageOperations.ExtractPages(document, ListPages(document, part), cancellationToken);
    }

    /// <summary>Makes a document from each part, one at a time as the result is read.</summary>
    /// <param name="document">The document; it must stay open while the result is read.</param>
    /// <param name="parts">The parts.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The documents, in the order of the parts.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A part does not lie inside the document.</exception>
    public static IEnumerable<PdfSplitOutput> Split(PdfDocument document, IReadOnlyList<PdfSplitPart> parts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(parts);
        foreach (var part in parts)
        {
            _ = ListPages(document, part);
        }

        return SplitCore(document, parts, cancellationToken);
    }

    /// <summary>Makes a document from each part, loading the pages with real asynchronous I/O, one at a time as the result is read.</summary>
    /// <param name="document">The document; it must stay open while the result is read.</param>
    /// <param name="parts">The parts.</param>
    /// <param name="cancellationToken">Cancels loading and the copy.</param>
    /// <returns>The documents, in the order of the parts.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A part does not lie inside the document.</exception>
    public static IAsyncEnumerable<PdfSplitOutput> SplitAsync(PdfDocument document, IReadOnlyList<PdfSplitPart> parts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(parts);
        foreach (var part in parts)
        {
            _ = ListPages(document, part);
        }

        return SplitAsyncCore(document, parts, cancellationToken);
    }

    /// <summary>Lists the page indexes of a part after checking that it lies inside the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="part">The part.</param>
    /// <returns>The indexes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="part"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The part does not lie inside the document.</exception>
    private static int[] ListPages(PdfDocument document, PdfSplitPart part)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentOutOfRangeException.ThrowIfNegative(part.FirstPage);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(part.PageCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(part.FirstPage + part.PageCount, document.PageCount, nameof(part));
        var pages = new int[part.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = part.FirstPage + i;
        }

        return pages;
    }

    /// <summary>Reads the page each top-level outline entry goes to, in page order with the first entry kept for a page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The starts.</returns>
    private static List<OutlineStart> ReadStarts(PdfDocument document)
    {
        var outline = PdfDocumentNavigation.GetOutline(document);
        var starts = new List<OutlineStart>(outline.Count);
        for (var i = 0; i < outline.Count; i++)
        {
            if (outline[i].Action.Value is GoToAction { Destination.PageIndex: var page } && (uint)page < (uint)document.PageCount)
            {
                starts.Add(new(page, i, outline[i].Title));
            }
        }

        starts.Sort(static (left, right) => left.Page != right.Page ? left.Page.CompareTo(right.Page) : left.Order.CompareTo(right.Order));
        for (var i = starts.Count - 1; i > 0; i--)
        {
            if (starts[i].Page == starts[i - 1].Page)
            {
                starts.RemoveAt(i);
            }
        }

        return starts;
    }

    /// <summary>Makes each part's document as the caller reads them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parts">The parts, already checked.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The documents.</returns>
    private static IEnumerable<PdfSplitOutput> SplitCore(PdfDocument document, IReadOnlyList<PdfSplitPart> parts, CancellationToken cancellationToken)
    {
        foreach (var part in parts)
        {
            yield return new(part, ExtractPart(document, part, cancellationToken));
        }
    }

    /// <summary>Makes each part's document as the caller reads them, loading pages asynchronously.</summary>
    /// <param name="document">The document.</param>
    /// <param name="parts">The parts, already checked.</param>
    /// <param name="cancellationToken">Cancels loading and the copy.</param>
    /// <returns>The documents.</returns>
    private static async IAsyncEnumerable<PdfSplitOutput> SplitAsyncCore(PdfDocument document, IReadOnlyList<PdfSplitPart> parts, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var part in parts)
        {
            var bytes = await PdfDocumentPageEditing.ExtractPagesAsync(document, ListPages(document, part), cancellationToken).ConfigureAwait(false);
            yield return new(part, bytes);
        }
    }

    /// <summary>The page an outline entry goes to.</summary>
    /// <param name="Page">The zero based page index.</param>
    /// <param name="Order">The entry's position among its siblings.</param>
    /// <param name="Title">The entry's title.</param>
    private readonly record struct OutlineStart(int Page, int Order, string Title);
}
