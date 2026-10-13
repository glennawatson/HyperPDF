// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// A working set of dictionary objects changed inside one transaction. Each object is copied the first time it is asked
/// for, every change goes to the copy, and <see cref="Flush"/> replaces each changed object once. This keeps an edit that
/// touches the same object many times (a long run of siblings, for example) to one copy and one replacement.
/// </summary>
[DebuggerDisplay("PdfObjectEdits: {Count} changed")]
internal sealed class PdfObjectEdits
{
    /// <summary>The transaction the edit belongs to.</summary>
    private readonly PdfEditTransaction _transaction;

    /// <summary>The store being edited.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The copies, by object number.</summary>
    private readonly Dictionary<int, PdfDictionary> _copies = [];

    /// <summary>The ids of the copies, by object number, in the order they were first asked for.</summary>
    private readonly List<PdfObjectId> _order = [];

    /// <summary>The catalog copy, once asked for.</summary>
    private PdfDictionary? _catalog;

    /// <summary>Initializes a new instance of the <see cref="PdfObjectEdits"/> class.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="store">The store.</param>
    internal PdfObjectEdits(PdfEditTransaction transaction, PdfObjectStore store)
    {
        _transaction = transaction;
        _store = store;
    }

    /// <summary>Gets the store being edited.</summary>
    internal PdfObjectStore Store => _store;

    /// <summary>Gets the transaction the edits belong to.</summary>
    internal PdfEditTransaction Transaction => _transaction;

    /// <summary>Gets the catalog as the edit currently has it: the working copy once there is one, else the stored catalog.</summary>
    internal PdfDictionary CatalogForRead => _catalog ?? _store.Catalog;

    /// <summary>Gets the number of objects changed so far.</summary>
    internal int Count => _copies.Count;

    /// <summary>Gets the editable copy of the catalog, making it the first time; <see cref="Flush"/> replaces the catalog.</summary>
    /// <returns>The copy.</returns>
    internal PdfDictionary Catalog() => _catalog ??= _store.Catalog.Clone();

    /// <summary>Gets the catalog's /Names dictionary for reading, as the edit currently has it.</summary>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    internal PdfDictionary? NamesForRead()
    {
        var raw = CatalogForRead.GetRaw(KnownName.Names);
        return raw.IsReference ? Peek(raw.AsReference()) : raw.AsDictionary();
    }

    /// <summary>Gets the editable copy of the catalog's /Names dictionary, making it, and linking it from the catalog, when needed.</summary>
    /// <returns>The copy.</returns>
    internal PdfDictionary Names()
    {
        var catalog = Catalog();
        var raw = catalog.GetRaw(KnownName.Names);
        if (raw.IsReference)
        {
            return Edit(raw.AsReference()) ?? ReplaceNames(catalog);
        }

        if (raw.AsDictionary() is { } direct)
        {
            var copy = direct.Clone();
            catalog.Set(KnownName.Names, PdfValue.FromDictionary(copy));
            return copy;
        }

        return ReplaceNames(catalog);
    }

    /// <summary>Gets the editable copy of a dictionary object, making it the first time.</summary>
    /// <param name="id">The object.</param>
    /// <returns>The copy, or <see langword="null"/> when the object is not a dictionary.</returns>
    internal PdfDictionary? Edit(PdfObjectId id)
    {
        if (!id.IsValid)
        {
            return null;
        }

        if (_copies.TryGetValue(id.Number, out var copy))
        {
            return copy;
        }

        copy = _transaction.CloneDictionary(id);
        if (copy is null)
        {
            return null;
        }

        _copies.Add(id.Number, copy);
        _order.Add(id);
        return copy;
    }

    /// <summary>Gets a dictionary object for reading: its working copy when it has one, else the stored object.</summary>
    /// <param name="id">The object.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    internal PdfDictionary? Peek(PdfObjectId id)
    {
        if (!id.IsValid)
        {
            return null;
        }

        return _copies.TryGetValue(id.Number, out var copy) ? copy : StoreReading.GetDictionary(_store, id);
    }

    /// <summary>
    /// Adds a new dictionary object. The store keeps the dictionary itself, so later changes to it need no second write
    /// and <see cref="Flush"/> skips it.
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The new object's id.</returns>
    internal PdfObjectId Add(PdfDictionary dictionary)
    {
        var id = _transaction.Add(PdfValue.FromDictionary(dictionary));
        _copies.Add(id.Number, dictionary);
        return id;
    }

    /// <summary>Makes a dictionary the new content of an existing object; <see cref="Flush"/> writes it.</summary>
    /// <param name="id">The object.</param>
    /// <param name="dictionary">The new content.</param>
    internal void Put(PdfObjectId id, PdfDictionary dictionary)
    {
        if (!_copies.ContainsKey(id.Number))
        {
            _order.Add(id);
        }

        _copies[id.Number] = dictionary;
    }

    /// <summary>Deletes an object and forgets any copy of it.</summary>
    /// <param name="id">The object.</param>
    internal void Delete(PdfObjectId id)
    {
        _ = _copies.Remove(id.Number);
        _transaction.Delete(id);
    }

    /// <summary>Writes every changed object back to the store.</summary>
    internal void Flush()
    {
        if (_catalog is not null)
        {
            PdfPageTreeWriter.ReplaceCatalog(_transaction, _store, _catalog);
            _catalog = null;
        }

        foreach (var id in _order)
        {
            if (_copies.TryGetValue(id.Number, out var copy))
            {
                _transaction.Replace(id, PdfValue.FromDictionary(copy));
            }
        }

        _copies.Clear();
        _order.Clear();
    }

    /// <summary>Links a new, empty /Names dictionary from the catalog.</summary>
    /// <param name="catalog">The catalog copy.</param>
    /// <returns>The new dictionary.</returns>
    private PdfDictionary ReplaceNames(PdfDictionary catalog)
    {
        var created = new PdfDictionary(_store);
        catalog.Set(KnownName.Names, PdfValue.FromDictionary(created));
        return created;
    }
}
