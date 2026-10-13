// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// Tells whether the feature a requirement type names is still in a document. A requirement says its feature is used or
/// needed by the document (ISO 32000-2 §12.11.3), so a requirement whose feature an edit removed would claim a need the
/// file no longer has. Only the types whose feature can be found in the file are checked; any other type is kept.
/// </summary>
internal static class PdfRequirementFeatures
{
    /// <summary>Determines whether the file has nothing left that the requirement type names.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="type">The requirement type.</param>
    /// <returns><see langword="true"/> when the feature is gone; <see langword="false"/> when it is there or cannot be told.</returns>
    internal static bool IsAbsent(PdfObjectStore store, string type) => type switch
    {
        "Collection" or "CollectionEditing" => !store.Catalog.ContainsKey(KnownName.Collection),
        "Attachment" or "AttachmentEditing" => !HasAttachments(store),
        "DigSigValidation" or "DigSigMDP" => !SignaturePresence.HasSignature(store),
        "DigSig" => !SignaturePresence.HasSignature(store) && !HasSignatureField(store),
        _ => false,
    };

    /// <summary>Determines whether the file embeds files: a portfolio, an embedded files name tree entry or a file attachment annotation.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasAttachments(PdfObjectStore store)
    {
        if (store.Catalog.ContainsKey(KnownName.Collection))
        {
            return true;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.Enumerate(store.Catalog.GetDictionary(KnownName.Names)?.GetDictionary(KnownName.EmbeddedFiles), entries);
        return entries.Count > 0 || HasAttachmentAnnotation(store);
    }

    /// <summary>Determines whether any page has a file attachment annotation.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when one is found.</returns>
    private static bool HasAttachmentAnnotation(PdfObjectStore store)
    {
        foreach (var page in PageTreeReader.Read(store).Pages)
        {
            var annotations = page.Dictionary.GetArray(KnownName.Annots);
            for (var i = 0; annotations is not null && i < annotations.Count; i++)
            {
                if (annotations.GetDictionary(i)?.IsName(KnownName.Subtype, KnownName.FileAttachment) == true)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Determines whether the form has any signature field, signed or not.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when one is found.</returns>
    private static bool HasSignatureField(PdfObjectStore store) =>
        store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields) is { } fields && HasSignatureField(fields, 0);

    /// <summary>Determines whether a field tree holds a signature field.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when one is found.</returns>
    private static bool HasSignatureField(PdfArray fields, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return false;
        }

        for (var i = 0; i < fields.Count; i++)
        {
            if (fields.GetDictionary(i) is not { } field)
            {
                continue;
            }

            if (field.IsName(KnownName.FT, KnownName.Sig) || (field.GetArray(KnownName.Kids) is { } kids && HasSignatureField(kids, depth + 1)))
            {
                return true;
            }
        }

        return false;
    }
}
