// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
            // A field value and its appearance are widget and field objects: no page content, structure tree or Link annotation changes.
            EditedKeepingLinks(self);
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
            // It can be a Link or file attachment annotation, so only this page's links are read again.
            EditedKeepingLinks(self);
            DropPageLinks(self, pageIndex);
        }

        return changed;
    }

    /// <summary>Records an edit of the bookmarks or attached files: drops what was read from them and marks the document unsaved.</summary>
    /// <param name="self">The owning document.</param>
    internal static void PartsEdited(HyperPdfDocument self)
    {
        // Bookmarks, attached files, metadata and layers are not page content and not part of the structure tree.
        Edited(self);
        Volatile.Write(ref self.Outline, null);
        Volatile.Write(ref self.Attachments, null);
    }

    /// <summary>
    /// Drops this document's links and page pictures after an edit, and moves the edit version on, which marks the document
    /// unsaved. The library dropped its own caches when the edit changed its objects.
    /// </summary>
    /// <param name="self">The owning document.</param>
    internal static void Edited(HyperPdfDocument self)
    {
        EditedKeepingLinks(self);

        // The version moved on first, so a link read that started before the edit sees it and removes what it published.
        Volatile.Write(ref self.Links, null);
    }

    /// <summary>
    /// Drops everything read or drawn from the objects before an edit except this document's links, for edits that cannot
    /// change a page's links or that name the page they change.
    /// </summary>
    /// <param name="self">The owning document.</param>
    internal static void EditedKeepingLinks(HyperPdfDocument self)
    {
        // The library dropped its own caches when the edit, undo or redo changed its objects, taking the pages a page edit left.
        // Dropping them again here would throw those pages away and read the whole page tree on the next page read.
        HyperPdfRendering.ResetRenderer(self);
        _ = Interlocked.Increment(ref self.EditVersion);
    }

    /// <summary>Forgets one page's links; the other pages keep theirs.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The page edited.</param>
    private static void DropPageLinks(HyperPdfDocument self, int pageIndex)
    {
        if (Volatile.Read(ref self.Links) is { } cache && (uint)pageIndex < (uint)cache.Length)
        {
            Volatile.Write(ref cache[pageIndex], null);
        }
    }
}
