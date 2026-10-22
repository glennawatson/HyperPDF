// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Normalises the content of a document's pages (ISO 32000-2 §7.8.2, §8.9.7): a <c>/Contents</c> array becomes one stream,
/// and large inline images become image XObjects. A page keeps its rendering. The changes are made in the document in
/// memory as one undoable edit, and a page that needs none is not touched.
/// </summary>
internal static class PdfContentNormaliser
{
    /// <summary>Normalises the pages as the options ask.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="options">The layout, which holds the changes to make.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of pages changed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Apply(PdfObjectStore store, in PdfCompactOptions options, CancellationToken cancellationToken) =>
        Apply(store, options.Normalisation, options.InlineImageThreshold, options.CompressionLevel, cancellationToken);

    /// <summary>Normalises the pages.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="changes">The changes to make.</param>
    /// <param name="threshold">The inline image size in bytes above which an image is moved; zero means the default.</param>
    /// <param name="level">The Flate level of the content streams made.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The number of pages changed.</returns>
    internal static int Apply(PdfObjectStore store, PdfContentNormalisation changes, int threshold, PdfCompressionLevel level, CancellationToken cancellationToken)
    {
        if (changes == PdfContentNormalisation.None)
        {
            return 0;
        }

        var pages = FindPages(store, cancellationToken);
        var edit = new NormalisationEdit(store);
        try
        {
            var limit = threshold > 0 ? threshold : PdfCompactOptions.DefaultInlineImageThreshold;
            foreach (var page in pages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                NormalisePage(store, edit, page, changes, limit, level, cancellationToken);
            }
        }
        catch
        {
            edit.Rollback();
            throw;
        }

        edit.Commit();
        return edit.Pages;
    }

    /// <summary>Finds the pages that are indirect objects, in page order.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The page ids.</returns>
    private static List<PdfObjectId> FindPages(PdfObjectStore store, CancellationToken cancellationToken)
    {
        var pages = new List<PdfObjectId>();
        var seen = new HashSet<int>();
        var pending = new Stack<PdfValue>();
        pending.Push(store.Catalog.GetRaw(KnownName.Pages));
        while (pending.TryPop(out var entry))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.IsReference || !seen.Add(entry.AsReference().Number) || StoreReading.Resolve(store, entry).AsDictionary() is not { } node)
            {
                continue;
            }

            if (node.GetArray(KnownName.Kids) is { } kids && !node.IsName(KnownName.Type, KnownName.Page))
            {
                for (var i = kids.Count - 1; i >= 0; i--)
                {
                    pending.Push(kids.GetRaw(i));
                }

                continue;
            }

            pages.Add(entry.AsReference());
        }

        return pages;
    }

    /// <summary>Normalises one page.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="edit">The edit the changes go into.</param>
    /// <param name="pageId">The page.</param>
    /// <param name="changes">The changes to make.</param>
    /// <param name="threshold">The inline image size above which an image is moved.</param>
    /// <param name="level">The Flate level of the content stream made.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    private static void NormalisePage(
        PdfObjectStore store,
        NormalisationEdit edit,
        PdfObjectId pageId,
        PdfContentNormalisation changes,
        int threshold,
        PdfCompressionLevel level,
        CancellationToken cancellationToken)
    {
        if (StoreReading.GetObject(store, pageId).AsDictionary() is not { } page)
        {
            return;
        }

        var contents = page.Get(KnownName.Contents);
        var plan = Plan(contents, changes);
        if (plan == PdfContentNormalisation.None)
        {
            return;
        }

        var content = default(PooledBuffer);
        var rewritten = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(contents, ref content, cancellationToken);
            var resources = new PageResourceEdit(store, InheritedResources(page));

            // An inline image starts with the BI operator, so content with no BI in it needs no token by token search.
            var moved = (plan & PdfContentNormalisation.ExternaliseInlineImages) == 0 || content.WrittenSpan.IndexOf("BI"u8) < 0
                ? 0
                : InlineImageExternaliser.Externalise(store, content.WrittenSpan, resources, threshold, edit.Transaction, ref rewritten, cancellationToken);
            if ((plan & PdfContentNormalisation.CoalesceContents) == 0 && moved == 0)
            {
                return;
            }

            var id = AddContent(edit.Transaction, moved > 0 ? rewritten.WrittenSpan : content.WrittenSpan, level);
            var copy = page.Clone();
            copy.Set(KnownName.Contents, PdfValue.FromReference(id));
            if (resources.Changed is { } changed)
            {
                copy.Set(KnownName.Resources, PdfValue.FromDictionary(changed));
            }

            edit.Transaction.Replace(pageId, PdfValue.FromDictionary(copy));
            edit.Pages++;
        }
        finally
        {
            rewritten.Dispose();
            content.Dispose();
        }
    }

    /// <summary>Works out which of the requested changes a page's content could use.</summary>
    /// <param name="contents">The page's <c>/Contents</c> value.</param>
    /// <param name="changes">The changes requested.</param>
    /// <returns>The changes to try on this page.</returns>
    private static PdfContentNormalisation Plan(PdfValue contents, PdfContentNormalisation changes)
    {
        var plan = PdfContentNormalisation.None;
        var array = contents.AsArray();
        if ((changes & PdfContentNormalisation.CoalesceContents) != 0 && array is { Count: > 1 })
        {
            plan |= PdfContentNormalisation.CoalesceContents;
        }

        if ((changes & PdfContentNormalisation.ExternaliseInlineImages) != 0 && (contents.AsStream() is not null || array is { Count: > 0 }))
        {
            plan |= PdfContentNormalisation.ExternaliseInlineImages;
        }

        return plan;
    }

    /// <summary>Adds a content stream, Flate-compressed.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="content">The decoded content.</param>
    /// <param name="level">The Flate level.</param>
    /// <returns>The new object's id.</returns>
    private static PdfObjectId AddContent(PdfEditTransaction transaction, ReadOnlySpan<byte> content, PdfCompressionLevel level)
    {
        var compressed = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(content, level, ref compressed);
            var dictionary = new PdfDictionary(null, 1);
            dictionary.Set(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            return transaction.Add(PdfValue.FromStream(new(dictionary, compressed.ToArray())));
        }
        finally
        {
            compressed.Dispose();
        }
    }

    /// <summary>Gets a page's resources: its own, or the nearest ancestor's (ISO 32000-2 Table 31).</summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The resources, or <see langword="null"/> when none are found.</returns>
    private static PdfDictionary? InheritedResources(PdfDictionary page)
    {
        var node = page;
        for (var depth = 0; node is not null && depth <= PdfLimits.MaxPageTreeDepth; depth++)
        {
            if (node.GetDictionary(KnownName.Resources) is { } resources)
            {
                return resources;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return null;
    }
}
