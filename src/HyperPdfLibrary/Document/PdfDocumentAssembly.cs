// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Builds a new document from pages of several documents, in any order. Pages are copied as
/// <see cref="PdfDocumentPageOperations.InsertPages(PdfDocument, int, PdfDocument, ReadOnlySpan{int})"/> copies them, so
/// each keeps its annotations, form fields, outline entries, destinations, labels, layers and embedded file names.
/// <see cref="PdfDocumentCollation"/> plans the order.
/// </summary>
public static class PdfDocumentAssembly
{
    /// <summary>Builds a document from pages of several documents.</summary>
    /// <param name="sources">The documents; they stay open until the result is returned.</param>
    /// <param name="pages">The pages in the order they take; at least one.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The PDF bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no pages.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A reference names a document or page that does not exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte[] Assemble(IReadOnlyList<PdfDocument> sources, ReadOnlySpan<PdfPageReference> pages, CancellationToken cancellationToken) =>
        Assemble(sources, pages, PdfAssemblyOptions.Default, cancellationToken);

    /// <summary>Builds a document from pages of several documents, taking more from the sources than their pages when asked.</summary>
    /// <param name="sources">The documents; they stay open until the result is returned.</param>
    /// <param name="pages">The pages in the order they take; at least one.</param>
    /// <param name="options">What the result takes besides the pages.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>The PDF bytes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no pages.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A reference or an option names a document or page that does not exist.</exception>
    public static byte[] Assemble(IReadOnlyList<PdfDocument> sources, ReadOnlySpan<PdfPageReference> pages, PdfAssemblyOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var runs = ToRuns(sources, pages);
        CheckPreferencesSource(sources, options);
        var first = runs[0];
        using var result = PdfDocumentReader.Open(PdfDocumentPageOperations.ExtractPages(sources[first.Source], first.Pages, cancellationToken), null);
        for (var i = 1; i < runs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PdfDocumentPageOperations.InsertPages(result, result.PageCount, sources[runs[i].Source], runs[i].Pages, cancellationToken);
        }

        TakePreferences(result, sources, options, cancellationToken);
        return PdfCompactWriter.Save(result.Objects, PdfCompactOptions.Default);
    }

    /// <summary>Builds a document from pages of several documents, loading the pages with real asynchronous I/O.</summary>
    /// <param name="sources">The documents; they stay open until the result is returned.</param>
    /// <param name="pages">The pages in the order they take; at least one.</param>
    /// <param name="cancellationToken">Cancels loading and the copy.</param>
    /// <returns>The PDF bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no pages.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A reference names a document or page that does not exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<byte[]> AssembleAsync(IReadOnlyList<PdfDocument> sources, ReadOnlyMemory<PdfPageReference> pages, CancellationToken cancellationToken) =>
        AssembleAsync(sources, pages, PdfAssemblyOptions.Default, cancellationToken);

    /// <summary>Builds a document from pages of several documents, loading the pages with real asynchronous I/O.</summary>
    /// <param name="sources">The documents; they stay open until the result is returned.</param>
    /// <param name="pages">The pages in the order they take; at least one.</param>
    /// <param name="options">What the result takes besides the pages.</param>
    /// <param name="cancellationToken">Cancels loading and the copy.</param>
    /// <returns>The PDF bytes.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no pages.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A reference or an option names a document or page that does not exist.</exception>
    public static async ValueTask<byte[]> AssembleAsync(IReadOnlyList<PdfDocument> sources, ReadOnlyMemory<PdfPageReference> pages, PdfAssemblyOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var runs = ToRuns(sources, pages.Span);
        CheckPreferencesSource(sources, options);
        var first = runs[0];
        using var result = await PdfDocumentReader.OpenAsync(
            await PdfDocumentPageEditing.ExtractPagesAsync(sources[first.Source], first.Pages, cancellationToken).ConfigureAwait(false),
            null,
            cancellationToken).ConfigureAwait(false);
        for (var i = 1; i < runs.Count; i++)
        {
            await PdfDocumentPageEditing.InsertPagesAsync(result, result.PageCount, sources[runs[i].Source], runs[i].Pages, cancellationToken).ConfigureAwait(false);
        }

        TakePreferences(result, sources, options, cancellationToken);
        await using var output = new MemoryStream();
        await PdfDocumentSaving.SaveAsync(result, output, cancellationToken).ConfigureAwait(false);
        return output.ToArray();
    }

    /// <summary>Checks that the document whose preferences are taken exists.</summary>
    /// <param name="sources">The documents.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentOutOfRangeException">The option names a document that does not exist.</exception>
    private static void CheckPreferencesSource(IReadOnlyList<PdfDocument> sources, PdfAssemblyOptions options)
    {
        if (options.ViewerPreferencesSource != -1)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)options.ViewerPreferencesSource, (uint)sources.Count, nameof(options));
        }
    }

    /// <summary>Copies the viewer preferences the options ask for into the result.</summary>
    /// <param name="result">The assembled document.</param>
    /// <param name="sources">The documents.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    private static void TakePreferences(PdfDocument result, IReadOnlyList<PdfDocument> sources, PdfAssemblyOptions options, CancellationToken cancellationToken)
    {
        if (options.ViewerPreferencesSource != -1)
        {
            _ = PdfDocumentViewerPreferences.CopyViewerPreferences(result, sources[options.ViewerPreferencesSource], cancellationToken);
        }
    }

    /// <summary>Checks the references and groups them into runs that come from one document, with no page repeated in a run.</summary>
    /// <param name="sources">The documents.</param>
    /// <param name="pages">The references.</param>
    /// <returns>The runs, in order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There are no pages.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A reference names a document or page that does not exist.</exception>
    private static List<PageRun> ToRuns(IReadOnlyList<PdfDocument> sources, ReadOnlySpan<PdfPageReference> pages)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (pages.IsEmpty)
        {
            throw new ArgumentException("A document needs at least one page.", nameof(pages));
        }

        var runs = new List<PageRun>();
        var current = new List<int>();
        var source = pages[0].Source;
        foreach (var reference in pages)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)reference.Source, (uint)sources.Count, nameof(pages));
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)reference.Page, (uint)sources[reference.Source].PageCount, nameof(pages));
            if (reference.Source != source || current.Contains(reference.Page))
            {
                runs.Add(new(source, [.. current]));
                current.Clear();
                source = reference.Source;
            }

            current.Add(reference.Page);
        }

        runs.Add(new(source, [.. current]));
        return runs;
    }

    /// <summary>Pages that come from one document, in order.</summary>
    /// <param name="Source">The document's position in the list.</param>
    /// <param name="Pages">The page indexes.</param>
    private sealed record PageRun(int Source, int[] Pages);
}
