// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>Finds the signatures a full rewrite would invalidate: filled signature fields and catalog permissions (DocMDP, UR3).</summary>
internal static class SignaturePresence
{
    /// <summary>Determines whether a document holds a signature over its bytes.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns><see langword="true"/> when the catalog has /Perms or a signature field has a value.</returns>
    internal static bool HasSignature(PdfObjectStore store)
    {
        var catalog = store.Catalog;
        if (catalog.ContainsKey(KnownName.Perms))
        {
            return true;
        }

        var fields = catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        return fields is not null && HasSignedField(fields, 0);
    }

    /// <summary>Stops a full rewrite of a signed document, whose new bytes no signature would cover.</summary>
    /// <param name="store">The document's objects.</param>
    /// <exception cref="PdfException">The document is signed.</exception>
    internal static void ThrowIfSigned(PdfObjectStore store)
    {
        if (HasSignature(store))
        {
            throw new PdfException(
                "The document is signed, and rewriting the whole file would leave signatures that no longer match its bytes. "
                + "Save incrementally to keep the signatures, or remove them with PdfDocumentSignatureEditing.RemoveSignatures first to write an unsigned copy.");
        }
    }

    /// <summary>Determines whether a field tree holds a signature field with a value.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when one is found.</returns>
    internal static bool HasSignedField(PdfArray fields, int depth)
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

            if (IsSignedField(field) || (field.GetArray(KnownName.Kids) is { } kids && HasSignedField(kids, depth + 1)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a field is a signature field with a value.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool IsSignedField(PdfDictionary field) => field.IsName(KnownName.FT, KnownName.Sig) && !field.GetRaw(KnownName.V).IsNull;
}
