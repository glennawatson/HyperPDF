// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Document;

/// <summary>Edits the signatures of a document.</summary>
public static class PdfDocumentSignatureEditing
{
    /// <summary>
    /// Removes every signature in a transaction (the open one, or its own), so the document can be rewritten as an unsigned
    /// copy: each filled signature field loses its value and the catalog loses /Perms (DocMDP and usage rights). The
    /// fields and their appearances stay. Compact and linearized saves refuse a signed document, as a rewrite leaves
    /// signatures that no longer match the file.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The number of signature values and permission dictionaries removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static int RemoveSignatures(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = new RemovalState(document);
        PdfDocumentEditing.RunEdit(
            document,
            "Remove signatures",
            PdfChangeKinds.Signing,
            PdfCacheScopes.None,
            state,
            static (transaction, state) => state.Removed = SignatureRemoval.Remove(state.Document.Objects, transaction));
        return state.Removed;
    }

    /// <summary>The document a removal edits and the number it removed.</summary>
    /// <param name="document">The document.</param>
    [DebuggerDisplay("RemovalState: {Removed}")]
    private sealed class RemovalState(PdfDocument document)
    {
        /// <summary>Gets the document.</summary>
        internal PdfDocument Document { get; } = document;

        /// <summary>Gets or sets the number of signature values and permission dictionaries removed.</summary>
        internal int Removed { get; set; }
    }
}
