// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// A private copy of a document that the build attempts of one signature use in turn. The attempts differ only in the room
/// reserved for the signature value, so each attempt's edits are rolled back before the next, and the copy's cross-reference
/// data is read once. The copy keeps the document the caller shows from being edited, keeps a signature in progress on
/// another thread out of this one's transaction, and keeps a concurrent edit of the document from being rolled back with it.
/// </summary>
internal sealed class PdfSigningWorkspace : IDisposable
{
    /// <summary>1 once the copy is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfSigningWorkspace"/> class.</summary>
    /// <param name="working">The private copy.</param>
    private PdfSigningWorkspace(PdfDocument working) => Document = working;

    /// <summary>Gets the private copy.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Disposes the private copy, once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Document.Dispose();
        }
    }

    /// <summary>Makes a private copy of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The workspace, which the caller disposes.</returns>
    internal static PdfSigningWorkspace Open(PdfDocument document) => new(PdfDocumentOptimizing.OpenWorkingCopy(document));

    /// <summary>Starts one build attempt. Disposing the attempt rolls back everything it changed.</summary>
    /// <param name="label">The transaction's description.</param>
    /// <returns>The attempt.</returns>
    internal PdfSigningAttempt BeginAttempt(string label)
    {
        var store = Document.Objects;
        var next = StoreTransactions.GetNextNumber(store);
        return new(PdfDocumentEditing.BeginEdit(Document, label), store, next);
    }
}
