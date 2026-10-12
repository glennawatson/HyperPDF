// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Removes signatures inside an edit: the value of each filled signature field and the catalog's /Perms. The fields and
/// their appearances stay, so the page looks the same and PDF/A appearance rules still hold.
/// </summary>
internal static class SignatureRemoval
{
    /// <summary>Removes every signature.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The open transaction.</param>
    /// <returns>The number of signature values and permission dictionaries removed.</returns>
    internal static int Remove(PdfObjectStore store, PdfEditTransaction transaction)
    {
        var fields = store.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.Fields);
        var removed = fields is null ? 0 : ClearFields(store, transaction, fields, 0);
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (!root.IsReference || !store.Catalog.ContainsKey(KnownName.Perms))
        {
            return removed;
        }

        var catalog = store.Catalog.Clone();
        _ = catalog.Remove(KnownName.Perms);
        transaction.Replace(root.AsReference(), PdfValue.FromDictionary(catalog));
        return removed + 1;
    }

    /// <summary>Clears the values of the signed fields in a field tree. Fields are indirect objects (ISO 32000-2 §12.7.3.1).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="fields">The fields.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns>The number of values removed.</returns>
    private static int ClearFields(PdfObjectStore store, PdfEditTransaction transaction, PdfArray fields, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return 0;
        }

        var removed = 0;
        for (var i = 0; i < fields.Count; i++)
        {
            var raw = fields.GetRaw(i);
            if (!raw.IsReference || StoreReading.Resolve(store, raw).AsDictionary() is not { } field)
            {
                continue;
            }

            if (field.GetArray(KnownName.Kids) is { } kids)
            {
                removed += ClearFields(store, transaction, kids, depth + 1);
            }

            if (!SignaturePresence.IsSignedField(field))
            {
                continue;
            }

            var copy = field.Clone();
            _ = copy.Remove(KnownName.V);
            transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(copy));
            removed++;
        }

        return removed;
    }
}
