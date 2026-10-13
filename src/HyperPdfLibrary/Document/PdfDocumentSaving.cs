// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Document;

/// <summary>Saves PDF documents.</summary>
public static class PdfDocumentSaving
{
    /// <summary>
    /// Saves the document with its edits appended to the original file, writing with async I/O. The original is copied
    /// from the source in chunks, then the update follows.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream to write.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask SaveAsync(PdfDocument document, Stream destination, CancellationToken cancellationToken) =>
        SaveAsync(document, destination, null, cancellationToken);

    /// <summary>
    /// Saves the document with its edits appended to the original file, writing with async I/O, and reports what the
    /// user should know: a PDF/A claim that was repaired or removed, and trap networks removed because the edits made
    /// them out of date. The save keeps the file's own encryption and never adds, changes or removes it: encryption covers
    /// the whole file, so <see cref="PdfDocumentProtection"/> writes a new copy for that.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="destination">The stream to write.</param>
    /// <param name="report">Receives what the save found, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the bytes are written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">An edited value cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static async ValueTask SaveAsync(PdfDocument document, Stream destination, PdfSaveReport? report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ObjectDisposedException.ThrowIf(document.IsDisposed, document);
        await PdfIncrementalWriter.SaveAsync(document.Objects, destination, report, cancellationToken).ConfigureAwait(false);
    }
}
