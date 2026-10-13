// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdf.Engine;

/// <summary>
/// Runs an edit of the bookmarks, attached files or form fields on the library document, then tells the adapter what it changed.
/// An edit reads what it needs with asynchronous I/O and no lock, then applies the change under the edit gate and the page write
/// lock, so two edits never overlap and readers never see one half done.
/// </summary>
internal static class HyperPdfDocumentEdits
{
    /// <summary>Runs an edit. The library makes the edit one undoable step and refuses it with an exception that says why.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="self">The owning document.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="prefetch">Loads what the edit reads, without a lock.</param>
    /// <param name="apply">Makes the change while the edit gate is held; it must not capture, so it allocates nothing.</param>
    /// <param name="cancellationToken">Stops the loading and the edit.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async ValueTask<DocumentEditResult> RunAsync<TState>(
        HyperPdfDocument self,
        TState state,
        Func<PdfDocument, CancellationToken, ValueTask> prefetch,
        Action<PdfDocument, TState> apply,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        try
        {
            await prefetch(self.Document, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Apply(self, state, apply);
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            return DocumentEditResult.Refused(ex.Message);
        }
    }

    /// <summary>
    /// Runs an edit that reports a refusal itself, such as a form field edit. The page caches are dropped only when the
    /// edit was made, because a form field edit changes pages, their annotations and their pictures.
    /// </summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="self">The owning document.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="prefetch">Loads what the edit reads, without a lock.</param>
    /// <param name="apply">Makes the change while the edit gate is held; it must not capture, so it allocates nothing. It returns the reason it was refused, or <see langword="null"/>.</param>
    /// <param name="scopes">The parts of the document the edit can change; the caches built from the other parts stay.</param>
    /// <param name="cancellationToken">Stops the loading and the edit.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ObjectDisposedException">The document is disposed.</exception>
    internal static async ValueTask<DocumentEditResult> RunCheckedAsync<TState>(
        HyperPdfDocument self,
        TState state,
        Func<PdfDocument, CancellationToken, ValueTask> prefetch,
        Func<PdfDocument, TState, string?> apply,
        PdfCacheScopes scopes,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(self.IsDisposed, self);
        try
        {
            await prefetch(self.Document, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return ApplyChecked(self, state, apply, scopes);
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            return DocumentEditResult.Refused(ex.Message);
        }
    }

    /// <summary>Tells whether an exception is the library refusing the edit. A document closed during the edit is not a refusal.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> when the edit was refused.</returns>
    internal static bool IsRefusal(Exception exception) => exception is (InvalidOperationException and not ObjectDisposedException) or ArgumentException;

    /// <summary>Makes an edit while no other edit, save or page read runs.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="self">The owning document.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="apply">The edit.</param>
    /// <returns>The outcome.</returns>
    private static DocumentEditResult Apply<TState>(HyperPdfDocument self, TState state, Action<PdfDocument, TState> apply)
    {
        lock (self.EditGate)
        {
            // Page readers wait while the edit runs and the cached outline, attachments and page pictures are dropped.
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            ObjectDisposedException.ThrowIf(self.IsDisposed, self);
            apply(self.Document, state);
            HyperPdfEditing.PartsEdited(self);
        }

        return DocumentEditResult.Done;
    }

    /// <summary>Makes an edit that reports its own refusal while no other edit, save or page read runs.</summary>
    /// <typeparam name="TState">The type of the arguments the edit reads.</typeparam>
    /// <param name="self">The owning document.</param>
    /// <param name="state">The arguments.</param>
    /// <param name="apply">The edit.</param>
    /// <param name="scopes">The parts of the document the edit can change.</param>
    /// <returns>The outcome.</returns>
    private static DocumentEditResult ApplyChecked<TState>(HyperPdfDocument self, TState state, Func<PdfDocument, TState, string?> apply, PdfCacheScopes scopes)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            ObjectDisposedException.ThrowIf(self.IsDisposed, self);
            if (apply(self.Document, state) is { } reason)
            {
                return DocumentEditResult.Refused(reason);
            }

            HyperPdfNavigation.PagesEdited(self, scopes);
        }

        return DocumentEditResult.Done;
    }
}
