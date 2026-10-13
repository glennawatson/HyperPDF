// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Keeps the interactive form's default resources (<c>/DR</c>) holding the fonts that generated appearances use, so a
/// reader that draws the field again finds the font its default appearance names (ISO 32000-2 12.7.3.3, Table 224).
/// </summary>
internal static class FormDefaultResources
{
    /// <summary>Adds the fonts an appearance stream uses to the form's <c>/DR /Font</c> dictionary when they are not there.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="appearance">The appearance stream.</param>
    internal static void AddFonts(PdfObjectStore store, PdfStream appearance)
    {
        var used = appearance.Dictionary.GetDictionary(KnownName.Resources)?.GetDictionary(KnownName.Font);
        if (used is null || used.Count == 0 || store.Catalog.GetDictionary(KnownName.AcroForm) is not { } form)
        {
            return;
        }

        var current = form.GetDictionary(KnownName.DR);
        if (!HasMissing(used, current?.GetDictionary(KnownName.Font)))
        {
            return;
        }

        var resources = current?.Clone() ?? new PdfDictionary(store);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(Merge(store, used, current?.GetDictionary(KnownName.Font))));
        Store(store, form, resources);
    }

    /// <summary>Copies the fonts already in the default resources and adds the used fonts that are not there.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="used">The fonts an appearance uses.</param>
    /// <param name="current">The default resources' fonts, or <see langword="null"/>.</param>
    /// <returns>The new font dictionary.</returns>
    private static PdfDictionary Merge(PdfObjectStore store, PdfDictionary used, PdfDictionary? current)
    {
        var fonts = current?.Clone() ?? new PdfDictionary(store);
        for (var i = 0; i < used.Count; i++)
        {
            if (!fonts.ContainsKey(used.GetKeyAt(i)))
            {
                fonts.Set(used.GetKeyAt(i), used.GetValueAt(i));
            }
        }

        return fonts;
    }

    /// <summary>Determines whether some used font is not in the default resources.</summary>
    /// <param name="used">The fonts an appearance uses.</param>
    /// <param name="current">The default resources' fonts, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a font is missing.</returns>
    private static bool HasMissing(PdfDictionary used, PdfDictionary? current)
    {
        for (var i = 0; i < used.Count; i++)
        {
            if (current is null || !current.ContainsKey(used.GetKeyAt(i)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes the form with new default resources back, in the object that holds it.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="form">The form dictionary as it is now.</param>
    /// <param name="resources">The new default resources.</param>
    private static void Store(PdfObjectStore store, PdfDictionary form, PdfDictionary resources)
    {
        var copy = form.Clone();
        copy.Set(KnownName.DR, PdfValue.FromDictionary(resources));
        if (store.Catalog.GetRaw(KnownName.AcroForm).AsReference() is { IsValid: true } formId)
        {
            StoreEditing.Replace(store, formId, PdfValue.FromDictionary(copy));
            return;
        }

        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(copy));
        StoreEditing.Replace(store, store.Trailer.GetRaw(KnownName.Root).AsReference(), PdfValue.FromDictionary(catalog));

        // The store keeps the catalog it opened with, so it is pointed at the edited copy.
        store.Catalog = catalog;
    }
}
