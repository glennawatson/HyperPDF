// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// A set of changes to indirect dictionaries. Each dictionary is copied once, changed on the copy and written back
/// together, so readers never see a dictionary that is being changed. The copies are kept in pooled arrays, which
/// <see cref="Dispose"/> gives back.
/// </summary>
[DebuggerDisplay("FormEdit: {_count} objects")]
internal ref struct FormEdit
{
    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The object ids of the copies, in the order they were made; a pooled array.</summary>
    private PdfObjectId[]? _ids;

    /// <summary>The copies, parallel to <see cref="_ids"/>; a pooled array.</summary>
    private PdfDictionary[]? _copies;

    /// <summary>The number of copies.</summary>
    private int _count;

    /// <summary>Initializes a new instance of the <see cref="FormEdit"/> struct.</summary>
    /// <param name="store">The document's objects.</param>
    internal FormEdit(PdfObjectStore store) => _store = store;

    /// <summary>Gets the document's objects.</summary>
    internal readonly PdfObjectStore Store => _store;

    /// <summary>Gets a value indicating whether anything was copied.</summary>
    internal readonly bool HasChanges => _count > 0;

    /// <summary>Gets the editable copy of an indirect dictionary.</summary>
    /// <param name="id">The object id.</param>
    /// <returns>The copy; <see langword="null"/> when the id is not valid or the object is not a dictionary.</returns>
    internal PdfDictionary? Edit(PdfObjectId id)
    {
        if (!id.IsValid)
        {
            return null;
        }

        for (var i = 0; i < _count; i++)
        {
            if (_ids![i].Number == id.Number)
            {
                return _copies![i];
            }
        }

        if (StoreReading.GetDictionary(_store, id) is not { } original)
        {
            return null;
        }

        var copy = original.Clone();
        PooledArrays.EnsureRoom(ref _ids, _count, clear: false)[_count] = id;
        PooledArrays.EnsureRoom(ref _copies, _count, clear: true)[_count] = copy;
        _count++;
        return copy;
    }

    /// <summary>Writes every copy back to the document and gives the arrays back.</summary>
    internal void Commit()
    {
        for (var i = 0; i < _count; i++)
        {
            StoreEditing.Replace(_store, _ids![i], PdfValue.FromDictionary(_copies![i]), PdfCacheScopes.None);
        }

        Dispose();
    }

    /// <summary>Gives the arrays back without writing pending changes.</summary>
    internal void Dispose()
    {
        PooledArrays.Return(ref _ids, _count, clear: false);
        PooledArrays.Return(ref _copies, _count, clear: true);
        _count = 0;
    }
}
