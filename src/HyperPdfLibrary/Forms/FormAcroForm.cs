// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Creates and rewrites the interactive form dictionary (ISO 32000-2 12.7.3, Table 224) inside a transaction.</summary>
internal static class FormAcroForm
{
    /// <summary>Gets the default appearance a new form gets: Helvetica fitted to the field, in black.</summary>
    private static ReadOnlySpan<byte> DefaultAppearanceText => "/Helv 0 Tf 0 g"u8;

    /// <summary>Gets the form's current dictionary, making an empty form when the document has none.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <returns>The form dictionary as the document holds it now.</returns>
    internal static PdfDictionary Ensure(PdfEditTransaction transaction, PdfObjectStore store)
    {
        if (store.Catalog.GetDictionary(KnownName.AcroForm) is { } existing)
        {
            return existing;
        }

        var form = new PdfDictionary(store);
        form.Set(KnownName.Fields, PdfValue.FromArray(new(store)));
        form.Set(KnownName.DA, PdfValue.FromString(DefaultAppearanceText.ToArray()));
        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(form));
        PdfPageTreeWriter.ReplaceCatalog(transaction, store, catalog);
        return store.Catalog.GetDictionary(KnownName.AcroForm)!;
    }

    /// <summary>Makes sure the form's default resources hold the Helvetica font the default appearance names.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    internal static void EnsureHelvetica(PdfEditTransaction transaction, PdfObjectStore store)
    {
        var form = Ensure(transaction, store);
        var name = store.Names.Intern(FormFont.FallbackName);
        if (form.GetDictionary(KnownName.DR)?.GetDictionary(KnownName.Font)?.ContainsKey(name) == true)
        {
            return;
        }

        var resources = form.GetDictionary(KnownName.DR)?.Clone() ?? new PdfDictionary(store);
        var fonts = resources.GetDictionary(KnownName.Font)?.Clone() ?? new PdfDictionary(store);
        fonts.Set(name, FormFont.CreateFallback(store).Resource);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        var copy = form.Clone();
        copy.Set(KnownName.DR, PdfValue.FromDictionary(resources));
        Store(transaction, store, copy);
    }

    /// <summary>Writes the form dictionary back where the document holds it: its own object, or the catalog.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="form">The new form dictionary.</param>
    internal static void Store(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary form)
    {
        if (store.Catalog.GetRaw(KnownName.AcroForm).AsReference() is { IsValid: true } id)
        {
            transaction.Replace(id, PdfValue.FromDictionary(form));
            return;
        }

        var catalog = store.Catalog.Clone();
        catalog.Set(KnownName.AcroForm, PdfValue.FromDictionary(form));
        PdfPageTreeWriter.ReplaceCatalog(transaction, store, catalog);
    }

    /// <summary>Replaces the form's <c>/Fields</c> array, keeping it an indirect object when it was one.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="form">The form dictionary being rewritten; changed when the array is direct.</param>
    /// <param name="fields">The new array.</param>
    internal static void SetFields(PdfEditTransaction transaction, PdfDictionary form, PdfArray fields)
    {
        if (form.GetRaw(KnownName.Fields).AsReference() is { IsValid: true } id)
        {
            transaction.Replace(id, PdfValue.FromArray(fields));
            return;
        }

        form.Set(KnownName.Fields, PdfValue.FromArray(fields));
    }

    /// <summary>Replaces an array that sits in a dictionary: in its own object when it is indirect, otherwise in the dictionary.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="owner">The dictionary being rewritten; changed when the array is direct.</param>
    /// <param name="key">The array's key.</param>
    /// <param name="array">The new array.</param>
    internal static void SetArray(PdfEditTransaction transaction, PdfDictionary owner, PdfName key, PdfArray array)
    {
        if (owner.GetRaw(key).AsReference() is { IsValid: true } id)
        {
            transaction.Replace(id, PdfValue.FromArray(array));
            return;
        }

        owner.Set(key, PdfValue.FromArray(array));
    }
}
