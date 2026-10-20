// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Redaction;

/// <summary>
/// Applies the redact annotations of a document (ISO 32000-2, 12.5.6.23) so the covered content is really removed: text
/// glyphs, image pixels, line art, links, widgets and other annotations under the areas go from the page, not just out of
/// sight. Applying cannot be undone once the file is saved, and the document can only be saved compactly afterwards:
/// <see cref = "StoreRedaction.RequiresCompactSave"/> stops an incremental update, which would keep the removed bytes.
/// Applying also removes every signature, as no signature can cover the rewritten file.
/// </summary>
public static class PdfRedactor
{
    /// <summary>Applies every redact annotation in the document in memory. Use <see cref="ApplyAndSave(PdfDocument, Stream, PdfRedactionOptions)"/> to save.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to treat what lies under the areas, and what to clean up.</param>
    /// <returns>What was removed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfRedactionReport Apply(PdfDocument document, PdfRedactionOptions options) => Apply(document, options, CancellationToken.None);

    /// <summary>Applies every redact annotation in the document in memory.</summary>
    /// <param name="document">The document.</param>
    /// <param name="options">How to treat what lies under the areas, and what to clean up.</param>
    /// <param name="cancellationToken">Checked between pages and inside long loops; the edit is rolled back when it fires.</param>
    /// <returns>What was removed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static PdfRedactionReport Apply(PdfDocument document, PdfRedactionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(options);
        var marks = new List<PdfRedaction>();
        PdfRedactions.GetAll(document, marks);
        if (marks.Count == 0)
        {
            return PdfRedactionReport.Empty;
        }

        var tally = new RedactionTally();
        PdfDocumentPageContent.RunInTransaction(
        document,
        "Apply redactions",
        PdfChangeKinds.Annotations | PdfChangeKinds.Metadata | PdfChangeKinds.Other,
        new RunState(
        document,
        marks,
        options,
        tally,
        cancellationToken),
        Run);
        StoreRedaction.RequireCompactSave(document.Objects);
        PdfDocumentPageContent.InvalidatePageContent(document);
        return tally.ToReport();
    }

    /// <summary>Applies every redact annotation in the document and writes a new, compact file without the removed content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream receiving the new file.</param>
    /// <param name="options">How to treat what lies under the areas, and what to clean up.</param>
    /// <returns>What was removed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfRedactionReport ApplyAndSave(PdfDocument document, Stream destination, PdfRedactionOptions options)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var report = Apply(document, options);
        PdfCompactWriter.Save(document.Objects, options.Layout, destination);
        return report;
    }

    /// <summary>Applies every redact annotation in the document and writes a new, compact file; the original bytes are never kept.</summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream receiving the new file.</param>
    /// <param name="options">How to treat what lies under the areas, and what to clean up.</param>
    /// <param name="cancellationToken">Cancels the work; the document is left unchanged when it stops before the pages are applied.</param>
    /// <returns>What was removed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async Task<PdfRedactionReport> ApplyAndSaveAsync(PdfDocument document, Stream destination, PdfRedactionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var report = Apply(document, options, cancellationToken);
        await PdfCompactWriter.SaveAsync(document.Objects, options.Layout, destination, cancellationToken).ConfigureAwait(false);
        return report;
    }

    /// <summary>Applies the marks inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="state">The work to do.</param>
    private static void Run(PdfEditTransaction transaction, RunState state)
    {
        var byPage = new SortedDictionary<int, List<PdfRedaction>>();
        foreach (var mark in state.Marks)
        {
            if (!byPage.TryGetValue(mark.PageIndex, out var list))
            {
                list = [];
                byPage[mark.PageIndex] = list;
            }

            list.Add(mark);
        }

        foreach (var (pageIndex, marks) in byPage)
        {
            state.Token.ThrowIfCancellationRequested();
            RedactPage(state, pageIndex, marks);
        }

        if (state.Options.PruneToUnicode)
        {
            ToUnicodePruner.Run(state.Document, state.Tally, state.Token);
        }

        if (state.Options.RemoveUnusedResources)
        {
            FontProgramTrimmer.Trim(state.Document, state.Tally, state.Token);
        }

        if (state.Options.ScrubMetadata)
        {
            MetadataScrubber.Scrub(state.Document.Objects, transaction);
        }

        // The compact save rewrites every byte, so no signature can still cover the redacted file.
        _ = SignatureRemoval.Remove(state.Document.Objects, transaction);
    }

    /// <summary>Applies the marks of one page.</summary>
    /// <param name="state">The work to do.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="marks">The page's redact annotations.</param>
    private static void RedactPage(RunState state, int pageIndex, List<PdfRedaction> marks)
    {
        var document = state.Document;
        var regions = new List<PdfRectangle>();
        var sources = new List<RedactMark>();
        foreach (var mark in marks)
        {
            regions.AddRange(mark.Regions);
            sources.Add(new(mark, PdfPageAnnotations.Get(document.Objects, PdfDocumentPages.GetPage(document, pageIndex), mark.AnnotationIndex)!));
        }

        var areas = regions.ToArray();
        var content = PdfPageContentReader.Read(document, pageIndex, state.Token);
        new ContentRedactor(areas, state.Options, state.Tally, state.Token).Redact(content);
        if (state.Options.DrawOverlay)
        {
            OverlayWriter.Write(content, sources);
        }

        if (PdfPageContentEditing.IsModified(content))
        {
            PdfPageContentApplication.Apply(content);
        }

        AnnotationRedactor.Run(document, pageIndex, areas, state.Options.Annotations, state.Tally);
        RemoveThumbnail(document, pageIndex);
        if (state.Options.RemoveUnusedResources)
        {
            ResourcePruner.Prune(document, pageIndex, state.Tally);
        }

        state.Tally.Pages++;
        state.Tally.Regions += areas.Length;
    }

    /// <summary>Takes the page's thumbnail away, since it still shows the content that was removed.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private static void RemoveThumbnail(PdfDocument document, int pageIndex)
    {
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var dictionary = PdfPageAnnotations.GetPageDictionary(document.Objects, page);
        if (!dictionary.ContainsKey(document.Objects.Names.Intern("Thumb"u8)))
        {
            return;
        }

        var copy = dictionary.Clone();
        _ = copy.Remove(document.Objects.Names.Intern("Thumb"u8));
        StoreEditing.Replace(document.Objects, page.Id, PdfValue.FromDictionary(copy));
    }

    /// <summary>The work an apply run does inside its transaction.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Marks">The redact annotations.</param>
    /// <param name="Options">The options.</param>
    /// <param name="Tally">Receives the counts.</param>
    /// <param name="Token">The cancellation token.</param>
    private sealed record RunState(PdfDocument Document, List<PdfRedaction> Marks, PdfRedactionOptions Options, RedactionTally Tally, CancellationToken Token);
}
