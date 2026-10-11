// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
namespace HyperPdfLibrary.Document;

/// <summary>Optimizes document objects and content.</summary>
public static class PdfDocumentOptimizing
{
    /// <summary>
    /// Opens a private copy of the document over the same file, with the unsaved edits copied in, so the optimiser can
    /// change pages without touching the document the caller shows. The copy shares this document's security handler and
    /// file, so it must not be disposed; it is collected when no longer used.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="PdfException">The file cannot be read again.</exception>
    internal static PdfDocument OpenWorkingCopy(PdfDocument document)
    {
        var store = StoreRevisions.OpenRevision(document.Objects, document.Objects.Source.Length);
        new StoreValueTranslator(document.Objects, store).ReplayEdits();
        lock (store.Gate)
        {
            StoreTransactions.RefreshCatalogLocked(store);
        }

        var copy = new PdfDocument(store, document.FontFactory);
        StoreTransactions.SetChangeCallback(store, copy.OnObjectsChanged);
        return copy;
    }

    /// <summary>Drops every cache read from the objects, including marked content and the structure tree, after the optimiser changed pages.</summary>
    /// <param name="document">The document.</param>
    internal static void RefreshAfterOptimizerEdit(PdfDocument document)
    {
        PdfDocumentEditing.InvalidateCaches(document);
        Volatile.Write(ref document.State.MarkedContent, null);
        Volatile.Write(ref document.State.StructureTree, null);
        lock (document.Objects.Gate)
        {
            StoreTransactions.RefreshCatalogLocked(document.Objects);
        }
    }
}
