// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Portfolio;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>Writes the catalog's <c>/Collection</c> dictionary that makes the attached files a portfolio (ISO 32000-2 12.3.5, Tables 153 to 156).</summary>
internal static class PdfPortfolioEditor
{
    /// <summary>The key of the collection dictionary's initial view.</summary>
    private const string ViewKey = "View";

    /// <summary>The first version with portfolios (12.3.5).</summary>
    private static readonly PdfVersion CollectionVersion = new(1, 7);

    /// <summary>The field types that hold data a file carries as a collection item (Table 155).</summary>
    private static readonly string[] ItemTypes = ["S", "D", "N"];

    /// <summary>The field types that read a file's own properties (Table 155).</summary>
    private static readonly string[] FileTypes = ["F", "Desc", "ModDate", "CreationDate", "Size"];

    /// <summary>The initial views a writer may choose without a navigator (Table 153).</summary>
    private static readonly string[] Views = ["D", "T", "H"];

    /// <summary>Makes the document a portfolio, replacing any collection dictionary it has.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="spec">The portfolio.</param>
    /// <exception cref="ArgumentException">The view, a field or a sort key is not valid, or the initial document is not attached.</exception>
    /// <exception cref="InvalidOperationException">The claimed PDF/A level does not allow attached files.</exception>
    internal static void Set(PdfObjectEdits edits, PdfDocument document, PdfPortfolioSpec spec)
    {
        PdfEditConformance.RequireCollectionAllowed(document);
        Validate(document, spec);
        var store = edits.Store;
        var collection = new PdfDictionary(store);
        collection.Set(KnownName.Type, PdfValue.FromName(KnownName.Collection));
        collection.Set(KnownName.Schema, PdfValue.FromDictionary(BuildSchema(store, spec.Schema)));
        if (!string.IsNullOrEmpty(spec.InitialDocument))
        {
            collection.Set(KnownName.D, PdfValue.FromString(PdfText.Encode(spec.InitialDocument, store.Version)));
        }

        if (!string.Equals(spec.View, Views[0], StringComparison.Ordinal))
        {
            collection.Set(store.Names.Intern(ViewKey), PdfValue.FromName(store.Names.Intern(spec.View)));
        }

        if (spec.Sort.Count > 0)
        {
            collection.Set(KnownName.Sort, PdfValue.FromDictionary(BuildSort(store, spec.Sort)));
        }

        edits.Catalog().Set(KnownName.Collection, PdfValue.FromDictionary(collection));
        PdfCatalogEditor.RaiseVersion(edits, CollectionVersion);
    }

    /// <summary>Takes away the collection dictionary, so the file is an ordinary document with attachments.</summary>
    /// <param name="edits">The edits.</param>
    /// <returns><see langword="true"/> when the document was a portfolio.</returns>
    internal static bool Remove(PdfObjectEdits edits)
    {
        if (edits.CatalogForRead.GetRaw(KnownName.Collection).IsNull)
        {
            return false;
        }

        _ = edits.Catalog().Remove(KnownName.Collection);
        return true;
    }

    /// <summary>Checks a portfolio before it is written.</summary>
    /// <param name="document">The document.</param>
    /// <param name="spec">The portfolio.</param>
    /// <exception cref="ArgumentException">Something in it is not valid.</exception>
    private static void Validate(PdfDocument document, PdfPortfolioSpec spec)
    {
        if (Array.IndexOf(Views, spec.View) < 0)
        {
            throw new ArgumentException("The view must be D (details), T (tiles) or H (hidden).", nameof(spec));
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in spec.Schema)
        {
            ValidateField(field, nameof(spec));
            if (!keys.Add(field.Key))
            {
                throw new ArgumentException($"The field key '{field.Key}' is used twice.", nameof(spec));
            }
        }

        foreach (var sort in spec.Sort)
        {
            if (!keys.Contains(sort.Key))
            {
                throw new ArgumentException($"The sort key '{sort.Key}' is not a field of the portfolio.", nameof(spec));
            }
        }

        if (!string.IsNullOrEmpty(spec.InitialDocument) && !HasAttachment(document, spec.InitialDocument))
        {
            throw new ArgumentException($"There is no attached file named '{spec.InitialDocument}' to show first.", nameof(spec));
        }
    }

    /// <summary>Checks one field.</summary>
    /// <param name="field">The field.</param>
    /// <param name="argument">The argument name for the exception.</param>
    /// <exception cref="ArgumentException">The key, type or name is not valid.</exception>
    private static void ValidateField(PdfPortfolioField field, string argument)
    {
        if (string.IsNullOrEmpty(field.Key) || string.IsNullOrWhiteSpace(field.Name))
        {
            throw new ArgumentException("A portfolio field needs a key and a name to show.", argument);
        }

        if (Array.IndexOf(ItemTypes, field.Subtype) < 0 && Array.IndexOf(FileTypes, field.Subtype) < 0)
        {
            throw new ArgumentException($"'{field.Subtype}' is not a field type. Use S, D, N, F, Desc, ModDate, CreationDate or Size.", argument);
        }
    }

    /// <summary>Determines whether the document has an attached file with a name.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The name in the embedded files name tree.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasAttachment(PdfDocument document, string name)
    {
        foreach (var attachment in PdfDocumentAttachments.GetAttachments(document))
        {
            if (string.Equals(attachment.Key, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the schema dictionary.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary BuildSchema(PdfObjectStore store, IReadOnlyList<PdfPortfolioField> fields)
    {
        var schema = new PdfDictionary(store, fields.Count);
        foreach (var field in fields)
        {
            var dictionary = new PdfDictionary(store);
            dictionary.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("CollectionField"u8)));
            dictionary.Set(KnownName.Subtype, PdfValue.FromName(store.Names.Intern(field.Subtype)));
            dictionary.Set(KnownName.N, PdfValue.FromString(PdfText.Encode(field.Name, store.Version)));
            dictionary.Set(store.Names.Intern("O"u8), PdfValue.FromInteger(field.Order));
            dictionary.Set(store.Names.Intern("V"u8), PdfValue.FromBoolean(field.Visible));
            dictionary.Set(store.Names.Intern("E"u8), PdfValue.FromBoolean(field.Editable));
            schema.Set(store.Names.Intern(field.Key), PdfValue.FromDictionary(dictionary));
        }

        return schema;
    }

    /// <summary>Builds the sort dictionary (Table 156).</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="sorts">The keys, most significant first.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary BuildSort(PdfObjectStore store, IReadOnlyList<PdfPortfolioSort> sorts)
    {
        var keys = new PdfArray(store, sorts.Count);
        var directions = new PdfArray(store, sorts.Count);
        foreach (var sort in sorts)
        {
            keys.Add(PdfValue.FromName(store.Names.Intern(sort.Key)));
            directions.Add(PdfValue.FromBoolean(sort.Ascending));
        }

        var result = new PdfDictionary(store);
        result.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("CollectionSort"u8)));
        result.Set(KnownName.S, PdfValue.FromArray(keys));
        result.Set(KnownName.A, PdfValue.FromArray(directions));
        return result;
    }
}
