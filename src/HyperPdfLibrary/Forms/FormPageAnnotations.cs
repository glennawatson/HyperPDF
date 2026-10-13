// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Collects the widgets added to pages and writes each page's <c>/Annots</c> array once, so adding a thousand widgets
/// costs one copy of each page's array, not one per widget. It also takes widgets off pages.
/// </summary>
[DebuggerDisplay("FormPageAnnotations: {_added.Count} pages")]
internal sealed class FormPageAnnotations
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The widgets to add to each page, by page index.</summary>
    private readonly Dictionary<int, List<PdfValue>> _added = [];

    /// <summary>The number of annotations each page had before the additions, by page index.</summary>
    private readonly Dictionary<int, int> _counts = [];

    /// <summary>Initializes a new instance of the <see cref="FormPageAnnotations"/> class.</summary>
    /// <param name="document">The document.</param>
    internal FormPageAnnotations(PdfDocument document) => _document = document;

    /// <summary>Takes widgets off the pages that show them.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="widgets">The object numbers of the widgets to take off.</param>
    /// <param name="cancellationToken">Cancels the walk over the pages.</param>
    internal static void Remove(PdfDocument document, PdfEditTransaction transaction, HashSet<int> widgets, CancellationToken cancellationToken)
    {
        var copies = new Dictionary<int, PdfDictionary>();
        for (var pageIndex = 0; pageIndex < document.PageCount && widgets.Count > 0; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, pageIndex);
            if (page.Dictionary.GetArray(KnownName.Annots) is not { } annotations || !ContainsAny(annotations, widgets))
            {
                continue;
            }

            var kept = new PdfArray(document.Objects, annotations.Count);
            foreach (var item in annotations.Items)
            {
                if (!item.IsReference || !widgets.Contains(item.AsReference().Number))
                {
                    kept.Add(item);
                }
            }

            var copy = PdfDocumentPageOperations.CloneForEdit(transaction, page);
            if (copy.GetRaw(KnownName.Annots).AsReference() is { IsValid: true } id)
            {
                transaction.Replace(id, PdfValue.FromArray(kept));
                continue;
            }

            copy.Set(KnownName.Annots, PdfValue.FromArray(kept));
            copies[pageIndex] = copy;
        }

        PdfPageStamping.StoreAll(document, transaction, copies);
    }

    /// <summary>Queues a widget for a page.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="widget">A reference to the widget.</param>
    /// <returns>The index the widget will have in the page's <c>/Annots</c> array.</returns>
    internal int Add(int pageIndex, PdfValue widget)
    {
        if (!_added.TryGetValue(pageIndex, out var list))
        {
            list = [];
            _added[pageIndex] = list;
            _counts[pageIndex] = PdfDocumentPages.GetPage(_document, pageIndex).Dictionary.GetArray(KnownName.Annots)?.Count ?? 0;
        }

        list.Add(widget);
        return _counts[pageIndex] + list.Count - 1;
    }

    /// <summary>Writes the queued widgets to their pages.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="tabs">Whether pages that get widgets must also tab through their annotations in structure order (<c>/Tabs /S</c>, ISO 14289-1 7.18.3).</param>
    internal void Commit(PdfEditTransaction transaction, bool tabs)
    {
        var copies = new Dictionary<int, PdfDictionary>();
        foreach (var (pageIndex, widgets) in _added)
        {
            var page = PdfDocumentPages.GetPage(_document, pageIndex);
            var copy = PdfDocumentPageOperations.CloneForEdit(transaction, page);
            var annotations = copy.GetArray(KnownName.Annots)?.Clone() ?? new PdfArray(_document.Objects, widgets.Count);
            foreach (var widget in widgets)
            {
                annotations.Add(widget);
            }

            var changed = false;
            if (copy.GetRaw(KnownName.Annots).AsReference() is { IsValid: true } id)
            {
                transaction.Replace(id, PdfValue.FromArray(annotations));
            }
            else
            {
                copy.Set(KnownName.Annots, PdfValue.FromArray(annotations));
                changed = true;
            }

            if (tabs && !copy.ContainsKey(KnownName.Tabs))
            {
                copy.Set(KnownName.Tabs, PdfValue.FromName(KnownName.S));
                changed = true;
            }

            if (changed)
            {
                copies[pageIndex] = copy;
            }
        }

        PdfPageStamping.StoreAll(_document, transaction, copies);
    }

    /// <summary>Determines whether an annotation array refers to any of some objects.</summary>
    /// <param name="annotations">The array.</param>
    /// <param name="widgets">The object numbers.</param>
    /// <returns><see langword="true"/> when one is referred to.</returns>
    private static bool ContainsAny(PdfArray annotations, HashSet<int> widgets)
    {
        foreach (var item in annotations.Items)
        {
            if (item.IsReference && widgets.Contains(item.AsReference().Number))
            {
                return true;
            }
        }

        return false;
    }
}
