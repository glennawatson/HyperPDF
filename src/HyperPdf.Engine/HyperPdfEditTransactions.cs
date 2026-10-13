// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdf.Engine;

/// <summary>
/// Starts edit transactions for callers that hold the edit gate. The library allows one open transaction per document, and
/// a caller that used the library directly may hold it, so these helpers turn that refusal into a plain message.
/// </summary>
internal static class HyperPdfEditTransactions
{
    /// <summary>The message shown when another edit holds the document's only transaction.</summary>
    internal const string EditInProgress = "Another edit of this document is still running. Try again when it has finished.";

    /// <summary>Starts a transaction.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="label">The undo label.</param>
    /// <returns>The transaction.</returns>
    /// <exception cref="InvalidOperationException">Another edit holds the document's transaction.</exception>
    internal static PdfEditTransaction Begin(PdfDocument document, string label) =>
        TryBegin(document, label, out var transaction) ? transaction : throw new InvalidOperationException(EditInProgress);

    /// <summary>Starts a transaction unless another edit holds the document's only one.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="transaction">The transaction when one was started.</param>
    /// <returns><see langword="true"/> when a transaction was started.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryBegin(PdfDocument document, string label, [NotNullWhen(true)] out PdfEditTransaction? transaction) =>
        TryBegin(document, label, PdfCacheScopes.All, out transaction);

    /// <summary>Starts a transaction whose changes affect only some parts of the document, unless another edit holds the document's only one.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="label">The undo label.</param>
    /// <param name="scopes">The parts of the document the transaction's changes can affect; the caches built from the other parts stay.</param>
    /// <param name="transaction">The transaction when one was started.</param>
    /// <returns><see langword="true"/> when a transaction was started.</returns>
    internal static bool TryBegin(PdfDocument document, string label, PdfCacheScopes scopes, [NotNullWhen(true)] out PdfEditTransaction? transaction)
    {
        try
        {
            transaction = PdfDocumentEditing.BeginEdit(document, label, scopes);
            return true;
        }
        catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
        {
            transaction = null;
            return false;
        }
    }

    /// <summary>Reverts or reapplies the latest history step.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="undo">Whether to revert rather than reapply.</param>
    /// <returns><see langword="true"/> when a step was applied.</returns>
    /// <exception cref="InvalidOperationException">Another edit holds the document's transaction.</exception>
    internal static bool Step(PdfDocument document, bool undo)
    {
        try
        {
            return undo ? PdfDocumentEditing.Undo(document) : PdfDocumentEditing.Redo(document);
        }
        catch (InvalidOperationException ex) when (ex is not ObjectDisposedException)
        {
            throw new InvalidOperationException(EditInProgress, ex);
        }
    }
}
