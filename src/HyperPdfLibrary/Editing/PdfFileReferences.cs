// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Finds what still points at a file specification, so a file is deleted only when nothing uses it.</summary>
internal static class PdfFileReferences
{
    /// <summary>Determines whether anything other than one name tree entry points at a file specification.</summary>
    /// <param name="document">The document.</param>
    /// <param name="id">The file specification's object.</param>
    /// <param name="excludedKey">The name tree entry that is being removed and does not count.</param>
    /// <returns><see langword="true"/> when another entry, an associated files list or a file attachment annotation uses it.</returns>
    internal static bool IsUsed(PdfDocument document, PdfObjectId id, string excludedKey)
    {
        foreach (var attachment in PdfDocumentAttachments.GetAttachments(document))
        {
            if (attachment.Id.Number == id.Number && !string.Equals(attachment.Key, excludedKey, StringComparison.Ordinal))
            {
                return true;
            }
        }

        var associated = document.Objects.Names.Intern("AF"u8);
        if (Lists(document.Catalog.GetArray(associated), id))
        {
            return true;
        }

        foreach (var page in PdfDocumentPages.GetPageSet(document).Pages)
        {
            // The page set is cached, so read each page as the store has it now.
            var current = PdfPageAnnotations.GetPageDictionary(document.Objects, page);
            if (Lists(current.GetArray(associated), id) || UsedByAnnotation(current, associated, id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a page's annotations point at a file specification.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="associated">The <c>/AF</c> key.</param>
    /// <param name="id">The file specification's object.</param>
    /// <returns><see langword="true"/> when an annotation's <c>/FS</c> or <c>/AF</c> does.</returns>
    private static bool UsedByAnnotation(PdfDictionary page, PdfName associated, PdfObjectId id)
    {
        var annotations = page.GetArray(KnownName.Annots);
        var fileSpec = page.Owner!.Names.Intern("FS"u8);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            if (annotation.GetRaw(fileSpec).AsReference().Number == id.Number || Lists(annotation.GetArray(associated), id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an array lists an object.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="id">The object.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool Lists(PdfArray? array, PdfObjectId id)
    {
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.GetRaw(i).AsReference().Number == id.Number)
            {
                return true;
            }
        }

        return false;
    }
}
