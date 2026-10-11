// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Adds annotations to a document. New annotations are stored as objects first and put on each page in one step, so a
/// page's annotation array is copied once however many annotations it gets. An annotation with the name of one already on
/// its page replaces it, so importing the same file twice leaves one copy.
/// </summary>
internal static class InterchangeAnnotationImporter
{
    /// <summary>Adds the annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annotations">The annotations.</param>
    /// <returns>How many were added, replaced and skipped.</returns>
    internal static AnnotationCounts Import(PdfDocument document, List<PdfInterchangeAnnotation> annotations)
    {
        if (annotations.Count == 0)
        {
            return default;
        }

        var store = document.Objects;
        var existing = IndexNames(document, annotations);
        var batches = new Dictionary<int, PageBatch>();
        var placed = new List<PlacedAnnotation>(annotations.Count);
        var replaced = 0;
        var skipped = 0;
        foreach (var source in annotations)
        {
            if ((uint)source.Page >= (uint)document.PageCount || !InterchangeAnnotationReader.IsSupported(source.Subtype))
            {
                skipped++;
                continue;
            }

            var page = PdfDocumentPages.GetPage(document, source.Page);
            var dictionary = Build(source, store, page);
            var id = Place(source, page, dictionary, existing, batches, out var wasReplaced);
            replaced += wasReplaced ? 1 : 0;
            placed.Add(new(source, page, id));
        }

        LinkAll(store, placed, batches);
        Commit(store, batches);
        return new(placed.Count - replaced, replaced, skipped);
    }

    /// <summary>Builds an annotation dictionary with its page.</summary>
    /// <param name="source">The annotation.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="page">The page.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Build(PdfInterchangeAnnotation source, PdfObjectStore store, PdfPage page)
    {
        var dictionary = InterchangeAnnotationBuilder.Build(source, store, store.Names, new StoreStreams(store));
        if (page.Id.IsValid)
        {
            dictionary.Set(KnownName.P, PdfValue.FromReference(page.Id));
        }

        return dictionary;
    }

    /// <summary>Stores an annotation, replacing the one of the same name on the page or queueing it for the page.</summary>
    /// <param name="source">The annotation.</param>
    /// <param name="page">The page.</param>
    /// <param name="dictionary">The built dictionary.</param>
    /// <param name="existing">The annotations already in the document, by page and name.</param>
    /// <param name="batches">The queued annotations by page.</param>
    /// <param name="replaced">Whether an existing annotation was replaced.</param>
    /// <returns>The object id of the annotation.</returns>
    private static PdfObjectId Place(PdfInterchangeAnnotation source, PdfPage page, PdfDictionary dictionary, Dictionary<NameKey, int> existing, Dictionary<int, PageBatch> batches, out bool replaced)
    {
        var store = dictionary.Owner!;
        replaced = false;
        if (source.Name is { } name && existing.TryGetValue(new(source.Page, name), out var index))
        {
            var id = PdfPageAnnotations.MakeIndirect(store, page, index);
            if (id.IsValid)
            {
                StoreEditing.Replace(store, id, PdfValue.FromDictionary(dictionary));
                replaced = true;
                return id;
            }
        }

        var added = StoreEditing.Add(store, PdfValue.FromDictionary(dictionary));
        GetBatch(page, source.Page, batches).Added.Add(PdfValue.FromReference(added));
        return added;
    }

    /// <summary>Links replies to the annotations they answer and gives annotations their pop-ups.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="placed">The annotations placed.</param>
    /// <param name="batches">The queued annotations by page.</param>
    private static void LinkAll(PdfObjectStore store, List<PlacedAnnotation> placed, Dictionary<int, PageBatch> batches)
    {
        var ids = new Dictionary<string, PdfObjectId>(StringComparer.Ordinal);
        foreach (var item in placed)
        {
            if (!string.IsNullOrEmpty(item.Source.Name))
            {
                ids[item.Source.Name] = item.Id;
            }
        }

        foreach (var item in placed)
        {
            var parent = item.Source.InReplyTo is { } name && ids.TryGetValue(name, out var found) ? found : default;
            if (!parent.IsValid && item.Source.Popup is null)
            {
                continue;
            }

            var dictionary = StoreReading.GetDictionary(store, item.Id)!.Clone();
            if (parent.IsValid)
            {
                InterchangeAnnotationBuilder.LinkReply(dictionary, parent, item.Source.ReplyType, store.Names);
            }

            if (item.Source.Popup is { } popup)
            {
                AddPopup(store, item, dictionary, popup, batches);
            }

            StoreEditing.Replace(store, item.Id, PdfValue.FromDictionary(dictionary));
        }
    }

    /// <summary>Adds a pop-up window and points the annotation at it.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="item">The annotation.</param>
    /// <param name="dictionary">The annotation's dictionary, which receives <c>/Popup</c>.</param>
    /// <param name="popup">The pop-up.</param>
    /// <param name="batches">The queued annotations by page.</param>
    private static void AddPopup(PdfObjectStore store, PlacedAnnotation item, PdfDictionary dictionary, PdfInterchangePopup popup, Dictionary<int, PageBatch> batches)
    {
        var window = PdfAnnotations.Create(store, KnownName.Popup, popup.Rect);
        window.Set(KnownName.Open, PdfValue.FromBoolean(popup.IsOpen));
        window.Set(KnownName.Parent, PdfValue.FromReference(item.Id));
        if (item.Page.Id.IsValid)
        {
            window.Set(KnownName.P, PdfValue.FromReference(item.Page.Id));
        }

        var id = StoreEditing.Add(store, PdfValue.FromDictionary(window));
        dictionary.Set(KnownName.Popup, PdfValue.FromReference(id));
        GetBatch(item.Page, item.Source.Page, batches).Added.Add(PdfValue.FromReference(id));
    }

    /// <summary>Puts each page's queued annotations on the page.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="batches">The queued annotations by page.</param>
    private static void Commit(PdfObjectStore store, Dictionary<int, PageBatch> batches)
    {
        foreach (var batch in batches.Values)
        {
            if (batch.Added.Count == 0)
            {
                continue;
            }

            var current = PdfPageAnnotations.GetArray(store, batch.Page);
            var array = new PdfArray(store, (current?.Count ?? 0) + batch.Added.Count);
            if (current is not null)
            {
                foreach (var value in current.Items)
                {
                    array.Add(value);
                }
            }

            foreach (var value in batch.Added)
            {
                array.Add(value);
            }

            _ = PdfPageAnnotations.SetArray(store, batch.Page, array);
        }
    }

    /// <summary>Finds or creates the queue of a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="pageIndex">The page's index.</param>
    /// <param name="batches">The queues.</param>
    /// <returns>The page's queue.</returns>
    private static PageBatch GetBatch(PdfPage page, int pageIndex, Dictionary<int, PageBatch> batches)
    {
        ref var batch = ref CollectionsMarshal.GetValueRefOrAddDefault(batches, pageIndex, out _);
        return batch ??= new(page);
    }

    /// <summary>Finds the annotations already in the document that an import could replace.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annotations">The annotations being imported.</param>
    /// <returns>The index in its page's array of each named annotation, by page and name.</returns>
    private static Dictionary<NameKey, int> IndexNames(PdfDocument document, List<PdfInterchangeAnnotation> annotations)
    {
        var index = new Dictionary<NameKey, int>();
        var pages = new HashSet<int>();
        foreach (var annotation in annotations)
        {
            if (annotation.Name is not null && (uint)annotation.Page < (uint)document.PageCount)
            {
                _ = pages.Add(annotation.Page);
            }
        }

        foreach (var pageIndex in pages)
        {
            if (PdfPageAnnotations.GetArray(document.Objects, PdfDocumentPages.GetPage(document, pageIndex)) is not { } array)
            {
                continue;
            }

            for (var i = 0; i < array.Count; i++)
            {
                if (array.GetDictionary(i)?.GetText(KnownName.NM) is { } name)
                {
                    index[new(pageIndex, name)] = i;
                }
            }
        }

        return index;
    }

    /// <summary>An annotation name on a page.</summary>
    /// <param name="Page">The page index.</param>
    /// <param name="Name">The annotation's name.</param>
    private readonly record struct NameKey(int Page, string Name);

    /// <summary>Stores a stream as an object of the document.</summary>
    /// <param name="store">The document's objects.</param>
    private sealed class StoreStreams(PdfObjectStore store) : IInterchangeObjects
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PdfValue Add(PdfStream stream) => PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromStream(stream)));
    }

    /// <summary>An annotation as it was placed in the document.</summary>
    /// <param name="Source">The imported annotation.</param>
    /// <param name="Page">The page it is on.</param>
    /// <param name="Id">Its object id.</param>
    private sealed record PlacedAnnotation(PdfInterchangeAnnotation Source, PdfPage Page, PdfObjectId Id);

    /// <summary>The annotations queued for one page.</summary>
    /// <param name="Page">The page.</param>
    private sealed record PageBatch(PdfPage Page)
    {
        /// <summary>Gets the references to the annotations to add.</summary>
        internal List<PdfValue> Added { get; } = [];
    }
}
