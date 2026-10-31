// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Applies regenerated content and stores its resources.</summary>
public static class PdfPageContentApplication
{
    /// <summary>The entries of a small dictionary.</summary>
    internal const int SmallEntries = 4;

    /// <summary>
    /// Writes the regenerated content to the page as one new compressed stream, with any resources the content added. The
    /// edit runs in a transaction, so it can be undone. The content read here is stale afterwards; read the page again.
    /// </summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <exception cref = "InvalidOperationException">The content belongs to a form, not a page.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Apply(PdfPageContent state) => PdfPageContentApplication.Apply(state, PdfRegenerateMode.Preserve);

    /// <summary>Writes the regenerated content to the page, as <see cref = "PdfPageContentApplication.Apply(PdfPageContent)"/> does.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "mode">Whether objects nobody changed keep their original bytes or are written from the model.</param>
    /// <exception cref = "InvalidOperationException">The content belongs to a form, not a page.</exception>
    public static void Apply(PdfPageContent state, PdfRegenerateMode mode)
    {
        if (state.Page is null)
        {
            throw new InvalidOperationException("Only a page's content can be applied; a form's content is applied through the page that paints it.");
        }

        state.ApplyMode = mode;
        HyperPdfLibrary.Document.PdfDocumentPageContent.ApplyPageContent(state.Document, state);
    }

    /// <summary>Writes the regenerated content to the page.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "cancellationToken">Cancels the work before it starts.</param>
    /// <returns>A task that completes when the page has been changed.</returns>
    /// <exception cref = "InvalidOperationException">The content belongs to a form, not a page.</exception>
    /// <exception cref = "OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask ApplyAsync(PdfPageContent state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PdfPageContentApplication.Apply(state);
        return ValueTask.CompletedTask;
    }

    /// <summary>Stores the regenerated content in the page's object while inside a transaction.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    internal static void CommitToPage(PdfPageContent state)
    {
        var store = state.Document.Objects;
        var current = PdfPageAnnotations.GetPageDictionary(store, state.Page!);
        var copy = current.Clone();
        var bytes = PdfPageContentWriter.Regenerate(state, state.ApplyMode);
        var contents = StoreEditing.Add(store, PdfValue.FromStream(PdfPageContentApplication.CreateStream(new(store, 1), bytes)));
        copy.Set(KnownName.Contents, PdfValue.FromReference(contents));
        if (PdfPageContentApplication.BuildResources(state, store) is { } resources)
        {
            copy.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        StoreEditing.Replace(store, state.Page!.Id, PdfValue.FromDictionary(copy));
    }

    /// <summary>Stores the regenerated content as a new form XObject.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "store">The document's objects.</param>
    /// <returns>A reference to the new form.</returns>
    internal static PdfValue CommitAsForm(PdfPageContent state, PdfObjectStore store)
    {
        var bytes = PdfPageContentWriter.Regenerate(state);
        var dictionary = state.FormObject!.Stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        if (PdfPageContentApplication.BuildResources(state, store) is { } resources)
        {
            dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources));
        }

        return PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromStream(PdfPageContentApplication.CreateStream(dictionary, bytes))));
    }

    /// <summary>Copies the resources with every resource the content added, storing them.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "store">The document's objects.</param>
    /// <returns>The new resources, or <see langword="null"/> when nothing was added.</returns>
    internal static PdfDictionary? BuildResources(PdfPageContent state, PdfObjectStore store)
    {
        if (state.Pending.Count == 0 && state.Generated.Count == 0)
        {
            return null;
        }

        var resources = state.Resources?.Clone() ?? new PdfDictionary(store, PdfPageContentApplication.SmallEntries);
        var tables = new Dictionary<KnownName, PdfDictionary>();
        PdfPageContentApplication.AddAll(store, resources, tables, state.Pending);
        PdfPageContentApplication.AddAll(store, resources, tables, state.Generated);
        return resources;
    }

    /// <summary>Makes a stream holding content, Flate-compressed.</summary>
    /// <param name = "dictionary">The stream dictionary.</param>
    /// <param name = "content">The content.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream CreateStream(PdfDictionary dictionary, byte[] content)
    {
        var compressed = default(PooledBuffer);
        try
        {
            ZLibCodec.Compress(content, PdfCompressionLevel.Balanced, ref compressed, dictionary.Owner?.Context?.ScratchPools);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return new(dictionary, compressed.ToArray());
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Stores a list of resources and sets them in the resource tables.</summary>
    /// <param name = "store">The document's objects.</param>
    /// <param name = "resources">The resource dictionary being built.</param>
    /// <param name = "tables">The copied category tables.</param>
    /// <param name = "list">The resources.</param>
    internal static void AddAll(PdfObjectStore store, PdfDictionary resources, Dictionary<KnownName, PdfDictionary> tables, List<PendingResource> list)
    {
        foreach (var pending in list)
        {
            if (!tables.TryGetValue(pending.Category, out var table))
            {
                table = resources.GetDictionary(pending.Category)?.Clone() ?? new PdfDictionary(store, PdfPageContentApplication.SmallEntries);
                tables[pending.Category] = table;
                resources.Set(pending.Category, PdfValue.FromDictionary(table));
            }

            table.Set(pending.Name, PdfPageContentApplication.Store(store, pending));
        }
    }

    /// <summary>Stores one resource's value.</summary>
    /// <param name = "store">The document's objects.</param>
    /// <param name = "pending">The resource.</param>
    /// <returns>The value to set in the table: a reference for streams.</returns>
    internal static PdfValue Store(PdfObjectStore store, PendingResource pending)
    {
        if (pending.Form is { } form)
        {
            return PdfPageContentApplication.CommitAsForm(form, store);
        }

        if (pending.Value.AsStream() is { } stream)
        {
            ImageStreams.Promote(store, stream);
            return PdfValue.FromReference(StoreEditing.Add(store, pending.Value));
        }

        return pending.Value;
    }
}
