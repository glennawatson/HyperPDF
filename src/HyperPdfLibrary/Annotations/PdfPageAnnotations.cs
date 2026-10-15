// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Reads and edits a page's <c>/Annots</c> array. An annotation's index is its position in the array.
/// Edits are copy-on-write: the array, or the page holding it, is copied, changed and put back into the document's
/// objects, so readers never see a half-changed array. Callers serialise edits to one document.
/// </summary>
public static class PdfPageAnnotations
{
    /// <summary>Gets the page dictionary as it is now, including edits made since opening.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The page dictionary.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfDictionary GetPageDictionary(PdfObjectStore store, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(page);
        return page.Id.IsValid && StoreReading.GetDictionary(store, page.Id) is { } current ? current : page.Dictionary;
    }

    /// <summary>Gets the page's annotation array.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The array, or <see langword="null"/> when the page has none.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfArray? GetArray(PdfObjectStore store, PdfPage page) => GetPageDictionary(store, page).GetArray(KnownName.Annots);

    /// <summary>Gets the annotation at an index.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The index in <c>/Annots</c>.</param>
    /// <returns>The annotation, or <see langword="null"/> when the index is out of range or not a dictionary.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfDictionary? Get(PdfObjectStore store, PdfPage page, int index) => GetArray(store, page)?.GetDictionary(index);

    /// <summary>Adds an annotation as a new indirect object at the end of the page's array.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The new annotation.</param>
    /// <returns>The new annotation's index, or -1 when the page cannot be changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static int Append(PdfObjectStore store, PdfPage page, PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var current = GetArray(store, page);
        var array = new PdfArray(store, (current?.Count ?? 0) + 1);
        if (current is not null)
        {
            foreach (var item in current.Items)
            {
                array.Add(item);
            }
        }

        if (!CanPublish(store, page))
        {
            return -1;
        }

        array.Add(PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromDictionary(annotation), PdfCacheScopes.None)));
        return Publish(store, page, array) ? array.Count - 1 : -1;
    }

    /// <summary>Replaces the annotation at an index with an edited copy.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The index.</param>
    /// <param name="annotation">The edited annotation.</param>
    /// <returns><see langword="true"/> when replaced.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool Replace(PdfObjectStore store, PdfPage page, int index, PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (GetArray(store, page) is not { } current || (uint)index >= (uint)current.Count)
        {
            return false;
        }

        var raw = current.GetRaw(index);
        if (raw.IsReference && raw.AsReference().IsValid)
        {
            StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromDictionary(annotation), PdfCacheScopes.None);
            return true;
        }

        var array = current.Clone();
        array.SetAt(index, PdfValue.FromDictionary(annotation));
        return Publish(store, page, array);
    }

    /// <summary>Removes the annotation at an index; later annotations move down one place.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool RemoveAt(PdfObjectStore store, PdfPage page, int index)
    {
        if (GetArray(store, page) is not { } current || (uint)index >= (uint)current.Count)
        {
            return false;
        }

        var array = current.Clone();
        array.RemoveAt(index);
        return Publish(store, page, array);
    }

    /// <summary>Gets the object id of the annotation at an index, first making an inline annotation an indirect object.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The index.</param>
    /// <returns>The id, or an invalid id when the index is not an annotation or the page cannot be changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfObjectId MakeIndirect(PdfObjectStore store, PdfPage page, int index)
    {
        if (GetArray(store, page) is not { } current || current.GetDictionary(index) is not { } annotation)
        {
            return default;
        }

        var raw = current.GetRaw(index);
        if (raw.IsReference)
        {
            return raw.AsReference();
        }

        if (!CanPublish(store, page))
        {
            return default;
        }

        var id = StoreEditing.Add(store, PdfValue.FromDictionary(annotation), PdfCacheScopes.None);
        var array = current.Clone();
        array.SetAt(index, PdfValue.FromReference(id));
        return Publish(store, page, array) ? id : default;
    }

    /// <summary>Gets the object id the page's array holds at an index.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="index">The index.</param>
    /// <returns>The id, or an invalid id for an inline annotation or an index out of range.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfObjectId GetId(PdfObjectStore store, PdfPage page, int index) => GetArray(store, page)?.GetRaw(index).AsReference() ?? default;

    /// <summary>Puts a whole annotation array on the page, for example one leaving some annotations out while saving.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotations">The array.</param>
    /// <returns><see langword="true"/> when set.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool SetArray(PdfObjectStore store, PdfPage page, PdfArray annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        return Publish(store, page, annotations);
    }

    /// <summary>Determines whether the page's array can be replaced: it is an object of its own, or the page is.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when it can.</returns>
    private static bool CanPublish(PdfObjectStore store, PdfPage page) => page.Id.IsValid || GetPageDictionary(store, page).GetRaw(KnownName.Annots).AsReference().IsValid;

    /// <summary>Puts an annotation array in place: into its own object when it has one, otherwise into a copy of the page.</summary>
    /// <param name="store">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="array">The new array.</param>
    /// <returns><see langword="true"/> when put in place.</returns>
    private static bool Publish(PdfObjectStore store, PdfPage page, PdfArray array)
    {
        var dictionary = GetPageDictionary(store, page);
        var raw = dictionary.GetRaw(KnownName.Annots);
        if (raw.IsReference && raw.AsReference().IsValid)
        {
            StoreEditing.Replace(store, raw.AsReference(), PdfValue.FromArray(array), PdfCacheScopes.None);
            return true;
        }

        if (!page.Id.IsValid)
        {
            return false;
        }

        var copy = dictionary.Clone();
        copy.Set(KnownName.Annots, PdfValue.FromArray(array));
        StoreEditing.Replace(store, page.Id, PdfValue.FromDictionary(copy), PdfCacheScopes.None);
        return true;
    }
}
