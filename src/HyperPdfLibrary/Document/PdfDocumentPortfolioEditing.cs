// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Portfolio;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Makes a document a portfolio (ISO 32000-2 12.3.5) by writing its <c>/Collection</c> dictionary, or turns it back into an
/// ordinary document. The attached files themselves are added with <see cref="PdfDocumentAttachmentEditing"/>. Each edit joins
/// the open transaction or makes its own, so it can be undone; a document certified with a signature refuses the edit, and
/// a document whose PDF/A level does not allow attached files refuses it.
/// </summary>
public static class PdfDocumentPortfolioEditing
{
    /// <summary>What an edit of the portfolio is called when the document's certification refuses it.</summary>
    private const string EditDescription = "change the portfolio";

    /// <summary>Makes the document a portfolio, replacing any collection it has.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The portfolio.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The view, a field or a sort key is not valid, or the first file is not attached.</exception>
    /// <exception cref="InvalidOperationException">The document is certified, or its PDF/A level does not allow attached files.</exception>
    public static void SetPortfolio(PdfDocument document, PdfPortfolioSpec spec)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(spec);
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Make portfolio",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            new Step(document, spec),
            static (edits, run) => PdfPortfolioEditor.Set(edits, run.Document, run.Spec));
    }

    /// <summary>Makes the document a portfolio.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The portfolio.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the portfolio is set.</returns>
    public static async ValueTask SetPortfolioAsync(PdfDocument document, PdfPortfolioSpec spec, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.Names, cancellationToken).ConfigureAwait(false);
        SetPortfolio(document, spec);
    }

    /// <summary>Turns a portfolio back into an ordinary document. The attached files stay.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when the document was a portfolio.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static bool RemovePortfolio(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = new RemoveState();
        PdfDocumentEditing.RunObjectEdit(
            document,
            "Remove portfolio",
            PdfChangeKinds.Other,
            PdfCacheScopes.None,
            EditDescription,
            state,
            static (edits, run) => run.Removed = PdfPortfolioEditor.Remove(edits));
        return state.Removed;
    }

    /// <summary>Turns a portfolio back into an ordinary document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the document was a portfolio.</returns>
    public static async ValueTask<bool> RemovePortfolioAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        await PdfEditPrefetch.CatalogEntryAsync(document, KnownName.Collection, cancellationToken).ConfigureAwait(false);
        return RemovePortfolio(document);
    }

    /// <summary>The arguments of a set.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Spec">The portfolio.</param>
    [DebuggerDisplay("Step: {Spec}")]
    private readonly record struct Step(PdfDocument Document, PdfPortfolioSpec Spec);

    /// <summary>The result of a removal.</summary>
    [DebuggerDisplay("RemoveState: {Removed}")]
    private sealed class RemoveState
    {
        /// <summary>Gets or sets a value indicating whether the document was a portfolio.</summary>
        internal bool Removed { get; set; }
    }
}
