// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Layers;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and updates optional content layers.</summary>
public static class PdfDocumentLayers
{
    /// <summary>Gets the document's layers and their visibility.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The document's optional content state.</returns>
    public static PdfOptionalContent GetOptionalContent(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.OptionalContent) is { } existing)
        {
            return existing;
        }

        var revision = PdfDocumentCaches.Revision(document);
        var created = new PdfOptionalContent(document.Objects);
        created.StartAt(Volatile.Read(ref document.State.LayerVersionFloor));
        var published = Interlocked.CompareExchange(ref document.State.OptionalContent, created, null) ?? created;
        if (Volatile.Read(ref document.State.ObjectsRevision) != revision)
        {
            // An edit ran while the layers were read; keep them only if none of the layer objects changed.
            PdfDocumentLayers.RefreshOptionalContent(document);
        }

        return published;
    }

    /// <summary>
    /// Drops the layer set after an edit unless the edit left every layer object alone, so a reader's choice of shown layers
    /// survives edits to pages, metadata and the like.
    /// </summary>
    /// <param name="document">The document.</param>
    internal static void RefreshOptionalContent(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.OptionalContent) is not { } current)
        {
            return;
        }

        if (current.IsCurrent())
        {
            // The layers are the same, but renderers redraw after any edit, as they do when the set is read again.
            current.InvalidatePages();
            return;
        }

        PdfDocumentLayers.DropOptionalContent(document, current);
    }

    /// <summary>Drops a layer set so it is read again, raising the version floor past every version it handed out.</summary>
    /// <param name="document">The document.</param>
    /// <param name="expected">The set to drop; a newer one a reader already published is left.</param>
    private static void DropOptionalContent(PdfDocument document, PdfOptionalContent expected)
    {
        if (Interlocked.CompareExchange(ref document.State.OptionalContent, null, expected) != expected)
        {
            return;
        }

        var next = expected.Version + 1;
        var floor = Volatile.Read(ref document.State.LayerVersionFloor);
        while (next > floor)
        {
            var seen = Interlocked.CompareExchange(ref document.State.LayerVersionFloor, next, floor);
            if (seen == floor)
            {
                return;
            }

            floor = seen;
        }
    }
}
