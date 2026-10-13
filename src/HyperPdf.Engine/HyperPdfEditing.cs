// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdf.Engine;

/// <summary>Implements Editing over the document's owned state.</summary>
internal static class HyperPdfEditing
{
    /// <summary>Records an edit that changed a form field's value and appearance.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    internal static bool FieldChanged(HyperPdfDocument self, bool changed)
    {
        if (changed)
        {
            // A field value and its appearance are widget and field objects: no page content or structure tree changes.
            Edited(self, PdfCacheScopes.None);
        }

        return changed;
    }

    /// <summary>Records an edit that added an annotation or reply.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page edited.</param>
    /// <param name="index">The new index, or -1 when nothing was added.</param>
    /// <returns><paramref name="index"/>.</returns>
    internal static int Added(HyperPdfDocument self, int pageIndex, int index)
    {
        _ = Changed(self, pageIndex, index >= 0);
        return index;
    }

    /// <summary>Records an edit to a page's annotations.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page edited.</param>
    /// <param name="changed">Whether the edit succeeded.</param>
    /// <returns><paramref name="changed"/>.</returns>
    internal static bool Changed(HyperPdfDocument self, int pageIndex, bool changed)
    {
        if (changed && (uint)pageIndex < (uint)self.PageCount)
        {
            // An annotation is an object in the page's /Annots array: the page's content stream and the structure tree stay.
            Edited(self, PdfCacheScopes.None);
        }

        return changed;
    }

    /// <summary>Records an edit of the bookmarks or attached files: drops what was read from them and marks the document unsaved.</summary>
    /// <param name="self">The owning document.</param>
    internal static void PartsEdited(HyperPdfDocument self)
    {
        // Bookmarks, attached files, metadata and layers are not page content and not part of the structure tree.
        Edited(self, PdfCacheScopes.None);
        Volatile.Write(ref self.Outline, null);
        Volatile.Write(ref self.Attachments, null);
    }

    /// <summary>
    /// Drops everything read or drawn from the objects before an edit: the library's pages and links, this document's
    /// links and page pictures. Moving the edit version on marks the document unsaved.
    /// </summary>
    /// <param name="self">The owning document.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Edited(HyperPdfDocument self) =>Edited(self, PdfCacheScopes.All);

    /// <summary>
    /// Drops everything read or drawn from the objects before an edit. The library keeps the text pages, marked content and
    /// structure tree that the edit's parts cannot have changed.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="scopes">The parts of the document the edit can have changed.</param>
    internal static void Edited(HyperPdfDocument self, PdfCacheScopes scopes)
    {
        PdfDocumentEditing.InvalidateCaches(self.Document, scopes);
        Volatile.Write(ref self.Links, null);
        HyperPdfRendering.ResetRenderer(self);
        _ = Interlocked.Increment(ref self.EditVersion);
    }
}
