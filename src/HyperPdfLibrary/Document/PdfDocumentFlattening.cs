// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Draws annotations and form fields into the content of pages and removes them, in place on an open document as one
/// edit that can be undone. A flattened annotation is drawn from its normal appearance (ISO 32000-2 12.5.5), fitted to its
/// rectangle, clipped to the appearance's box, with the annotation's opacity, blend mode and optional content. Form fields
/// without an appearance get one drawn from their value first (12.7.3.3). When every field is flattened the document loses
/// its <c>/AcroForm</c>. In a tagged document the drawing of an annotation that belonged to a structure element becomes
/// marked content of that element, and any other drawing becomes an artifact (14.6, 14.8). A signed document is refused.
/// </summary>
public static class PdfDocumentFlattening
{
    /// <summary>The kinds of change a flatten makes.</summary>
    private const PdfChangeKinds Kinds = PdfChangeKinds.PageChanges | PdfChangeKinds.Annotations | PdfChangeKinds.FormFill | PdfChangeKinds.Other;

    /// <summary>Finds out whether a document can be flattened.</summary>
    /// <param name="document">The document.</param>
    /// <returns>Why it cannot, or <see cref="PdfFlattenRefusal.None"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfFlattenRefusal GetRefusal(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        // Flattening rewrites page content, which no signature or certification lets through.
        return SignaturePresence.HasSignature(document.Objects) ? PdfFlattenRefusal.Signed : PdfFlattenRefusal.None;
    }

    /// <summary>Flattens pages for a mode, with every kind of annotation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="mode">Whether the page is drawn as shown on screen or as printed.</param>
    /// <returns>What was drawn, removed and left alone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist, or <paramref name="mode"/> is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFlattenReport Flatten(PdfDocument document, ReadOnlySpan<int> pages, PdfFlattenMode mode) =>
        Flatten(document, pages, new PdfFlattenOptions { Mode = mode }, CancellationToken.None);

    /// <summary>Flattens pages.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="options">What to flatten.</param>
    /// <returns>What was drawn, removed and left alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist, or an option is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfFlattenReport Flatten(PdfDocument document, ReadOnlySpan<int> pages, PdfFlattenOptions options) =>
        Flatten(document, pages, options, CancellationToken.None);

    /// <summary>Flattens pages, passing the token to the edit.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>What was drawn, removed and left alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist, or an option is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfFlattenReport Flatten(PdfDocument document, ReadOnlySpan<int> pages, PdfFlattenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        Validate(document, pages, options);
        var run = new Run(document, Distinct(pages), options, cancellationToken);
        PdfDocumentEditing.RunEdit(document, "Flatten annotations", Kinds, run, static (transaction, run) => run.Execute(transaction));
        return run.Report!;
    }

    /// <summary>Flattens every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <returns>What was drawn, removed and left alone.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfFlattenReport FlattenAll(PdfDocument document, PdfFlattenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Flatten(document, AllPages(document), options, cancellationToken);
    }

    /// <summary>Flattens pages after loading them with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The zero based indexes of the pages; repeats are ignored.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Cancels loading and the edit; the document is left unchanged.</param>
    /// <returns>What was drawn, removed and left alone.</returns>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    public static async ValueTask<PdfFlattenReport> FlattenAsync(PdfDocument document, ReadOnlyMemory<int> pages, PdfFlattenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        for (var i = 0; i < pages.Length; i++)
        {
            _ = await PdfDocumentPages.GetPageAsync(document, pages.Span[i], cancellationToken).ConfigureAwait(false);
        }

        return Flatten(document, pages.Span, options, cancellationToken);
    }

    /// <summary>Flattens every page after loading the pages with real asynchronous I/O.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Cancels loading and the edit; the document is left unchanged.</param>
    /// <returns>What was drawn, removed and left alone.</returns>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    public static ValueTask<PdfFlattenReport> FlattenAllAsync(PdfDocument document, PdfFlattenOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        return FlattenAsync(document, AllPages(document), options, cancellationToken);
    }

    /// <summary>Lists every page index.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The indexes.</returns>
    private static int[] AllPages(PdfDocument document)
    {
        var pages = new int[document.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i] = i;
        }

        return pages;
    }

    /// <summary>Sorts page indexes and drops repeats.</summary>
    /// <param name="pages">The indexes.</param>
    /// <returns>The distinct indexes in order.</returns>
    private static int[] Distinct(ReadOnlySpan<int> pages)
    {
        var sorted = pages.ToArray();
        Array.Sort(sorted);
        var count = 0;
        foreach (var index in sorted)
        {
            if (count > 0 && sorted[count - 1] == index)
            {
                continue;
            }

            sorted[count] = index;
            count++;
        }

        return count == sorted.Length ? sorted : sorted[..count];
    }

    /// <summary>Checks the arguments.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentOutOfRangeException">A page does not exist or an option is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document is signed.</exception>
    private static void Validate(PdfDocument document, ReadOnlySpan<int> pages, PdfFlattenOptions options)
    {
        foreach (var index in pages)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)document.PageCount, nameof(pages));
        }

        if (!Enum.IsDefined(options.Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "The flatten mode is not defined.");
        }

        if (!Enum.IsDefined(options.Targets))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Targets, "The flatten targets are not defined.");
        }

        if (GetRefusal(document) == PdfFlattenRefusal.Signed)
        {
            throw new InvalidOperationException("The document is signed. Flattening changes the signed pages and would make the signature invalid, so it was not done.");
        }
    }

    /// <summary>A flatten to run inside an edit, and the report it produces.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The distinct page indexes, in order.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Cancels the flatten.</param>
    private sealed class Run(PdfDocument document, int[] pages, PdfFlattenOptions options, CancellationToken cancellationToken)
    {
        /// <summary>Gets the report, once the flatten has run.</summary>
        internal PdfFlattenReport? Report { get; private set; }

        /// <summary>Runs the flatten.</summary>
        /// <param name="transaction">The transaction the edit belongs to.</param>
        internal void Execute(PdfEditTransaction transaction)
        {
            var session = new PdfFlattenSession(document, transaction, options, cancellationToken);
            var copies = new Dictionary<int, PdfDictionary>();
            foreach (var index in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PdfFlattenPages.Apply(session, index, copies);
            }

            var removal = PdfFieldRemoval.Remove(transaction, document.Objects, session.RemovedWidgets, session.Candidates);
            if (session.Structure is { } structure)
            {
                structure.Finish();
                session.Candidates.AddRange(structure.Dropped);
            }

            PdfPageStamping.StoreAll(document, transaction, copies);
            DeleteUnreachable(session);
            Report = CreateReport(session, removal);
        }

        /// <summary>Makes the report of a finished flatten.</summary>
        /// <param name="session">The flatten.</param>
        /// <param name="removal">What was taken out of the form.</param>
        /// <returns>The report.</returns>
        private static PdfFlattenReport CreateReport(PdfFlattenSession session, PdfFieldRemovalResult removal) => new(
            session.PagesChanged,
            session.AnnotationsDrawn,
            session.WidgetsDrawn,
            session.AppearancesDrawn,
            session.WidgetsRemoved,
            removal.FieldsRemoved,
            removal.FormRemoved,
            removal.XfaRemoved,
            session.Structure?.TaggedContent ?? 0,
            session.Structure?.Artifacts ?? 0,
            session.RotationsBaked,
            session.Skipped.ToArray());

        /// <summary>Deletes the objects that only the removed annotations and fields used.</summary>
        /// <param name="session">The flatten.</param>
        private void DeleteUnreachable(PdfFlattenSession session)
        {
            var reachable = PdfObjectReachability.FromTrailer(document.Objects, cancellationToken);
            var deleted = new HashSet<int>();
            foreach (var id in session.Candidates)
            {
                if (id.IsValid && !reachable.Contains(id.Number) && deleted.Add(id.Number))
                {
                    session.Transaction.Delete(id);
                }
            }
        }
    }
}
