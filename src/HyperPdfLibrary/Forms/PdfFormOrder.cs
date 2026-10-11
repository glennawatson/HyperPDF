// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Forms;

/// <summary>Order operations over shared form state.</summary>
public static class PdfFormOrder
{
    /// <summary>The order value of a widget the structure tree does not list, so it sorts after those it does.</summary>
    private const int UnlistedOrdinal = int.MaxValue;

    /// <summary>
    /// Appends the full names of the fields that have a calculate action, in the order the form wants them recalculated
    /// (the AcroForm's <c>/CO</c> array). A name appears once. Fields the array does not list are not appended.
    /// </summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "output">The list receiving the names.</param>
    /// <exception cref = "ArgumentNullException"><paramref name = "output"/> is <see langword="null"/>.</exception>
    public static void GetCalculationOrder(PdfForm form, List<string> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!PdfFormReading.HasForm(form) || form.Document.Catalog.GetDictionary(KnownName.AcroForm)?.GetArray(KnownName.CO)is not { } order)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < order.Count; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            if (order.GetDictionary(i)is { } field && FieldAttributes.GetFullName(field)is { Length: > 0 } name && seen.Add(name))
            {
                output.Add(name);
            }
        }
    }

    /// <summary>Gets the order a page asks its annotations to be visited in.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <returns>The order; <see cref = "PdfTabOrder.Unspecified"/> when the page names none or does not exist.</returns>
    public static PdfTabOrder GetTabOrder(PdfForm form, int pageIndex)
    {
        if ((uint)pageIndex >= (uint)form.Document.PageCount)
        {
            return PdfTabOrder.Unspecified;
        }

        var name = PdfDocumentPages.GetPage(form.Document, pageIndex).Dictionary.GetName(KnownName.Tabs);
        if (name.Is(KnownName.R))
        {
            return PdfTabOrder.Row;
        }

        if (name.Is(KnownName.C))
        {
            return PdfTabOrder.Column;
        }

        return name.Is(KnownName.S) ? PdfTabOrder.Structure : PdfTabOrder.Unspecified;
    }

    /// <summary>
    /// Appends the indexes of a page's form widgets in the order the Tab key visits them, following the page's
    /// <c>/Tabs</c> entry. A page with no entry, or a structure order with no structure tree, uses <c>/Annots</c> order.
    /// </summary>
    /// <param name = "form">The shared form state.</param>
    /// <param name = "pageIndex">The zero based page index.</param>
    /// <param name = "output">The list receiving the widget indexes, as <see cref = "PdfFormWidget.Index"/> gives them.</param>
    /// <exception cref = "ArgumentNullException"><paramref name = "output"/> is <see langword="null"/>.</exception>
    public static void GetTabSequence(PdfForm form, int pageIndex, List<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (!PdfFormReading.HasForm(form) || PdfFormReading.GetAnnotations(form, pageIndex)is not { } annotations)
        {
            return;
        }

        var order = PdfFormOrder.GetTabOrder(form, pageIndex);
        var page = PdfDocumentPages.GetPage(form.Document, pageIndex);
        var ordinals = order == PdfTabOrder.Structure ? PdfFormOrder.ReadStructureOrdinals(form) : null;
        var keys = new List<TabKey>(annotations.Count);
        for (var i = 0; i < annotations.Count; i++)
        {
            PdfCancellation.ThrowIfCancelled();
            if (PdfFormReading.Resolve(form, pageIndex, annotations, i)is { Type: not PdfFieldType.Unknown } widget)
            {
                keys.Add(PdfFormOrder.CreateKey(page, widget, ordinals));
            }
        }

        if (order == PdfTabOrder.Row)
        {
            keys.Sort(PdfFormOrder.CompareRows);
        }
        else if (order == PdfTabOrder.Column)
        {
            keys.Sort(PdfFormOrder.CompareColumns);
        }
        else if (ordinals is not null)
        {
            keys.Sort(PdfFormOrder.CompareStructure);
        }

        foreach (var key in keys)
        {
            output.Add(key.Index);
        }
    }

    /// <summary>Numbers the object kids of an element and its descendants in document order.</summary>
    /// <param name = "element">The element.</param>
    /// <param name = "ordinals">The places, added to.</param>
    /// <param name = "depth">The depth, which bounds the walk.</param>
    internal static void Number(PdfStructureElement element, Dictionary<int, int> ordinals, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return;
        }

        foreach (var kid in element.Kids)
        {
            PdfCancellation.ThrowIfCancelled();
            if (kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid)
            {
                _ = ordinals.TryAdd(kid.Object.Number, ordinals.Count);
            }
            else if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                PdfFormOrder.Number(child, ordinals, depth + 1);
            }
        }
    }

    /// <summary>Numbers the annotation objects the structure tree refers to, in tree order.</summary>
    /// <param name = "form">The shared form state.</param>
    /// <returns>The places by object number; <see langword="null"/> when the document has no structure tree.</returns>
    internal static Dictionary<int, int>? ReadStructureOrdinals(PdfForm form)
    {
        if (PdfDocumentTagged.GetStructureTree(form.Document)is not { } tree)
        {
            return null;
        }

        var ordinals = new Dictionary<int, int>();
        foreach (var root in tree.Roots)
        {
            PdfFormOrder.Number(root, ordinals, 0);
        }

        return ordinals;
    }

    /// <summary>Orders keys by row, then by column, then by <c>/Annots</c> position.</summary>
    /// <param name = "a">The first key.</param>
    /// <param name = "b">The second key.</param>
    /// <returns>The comparison.</returns>
    private static int CompareRows(TabKey a, TabKey b)
    {
        var byRow = a.Y.CompareTo(b.Y);
        if (byRow != 0)
        {
            return byRow;
        }

        var byColumn = a.X.CompareTo(b.X);
        return byColumn != 0 ? byColumn : a.Index.CompareTo(b.Index);
    }

    /// <summary>Orders keys by column, then by row, then by <c>/Annots</c> position.</summary>
    /// <param name = "a">The first key.</param>
    /// <param name = "b">The second key.</param>
    /// <returns>The comparison.</returns>
    private static int CompareColumns(TabKey a, TabKey b)
    {
        var byColumn = a.X.CompareTo(b.X);
        if (byColumn != 0)
        {
            return byColumn;
        }

        var byRow = a.Y.CompareTo(b.Y);
        return byRow != 0 ? byRow : a.Index.CompareTo(b.Index);
    }

    /// <summary>Orders keys by their place in the structure tree, then by <c>/Annots</c> position.</summary>
    /// <param name = "a">The first key.</param>
    /// <param name = "b">The second key.</param>
    /// <returns>The comparison.</returns>
    private static int CompareStructure(TabKey a, TabKey b)
    {
        var byOrdinal = a.Ordinal.CompareTo(b.Ordinal);
        return byOrdinal != 0 ? byOrdinal : a.Index.CompareTo(b.Index);
    }

    /// <summary>Makes the sort key of a widget.</summary>
    /// <param name = "page">The widget's page.</param>
    /// <param name = "widget">The widget.</param>
    /// <param name = "ordinals">The structure tree places of annotation objects by object number, or <see langword="null"/>.</param>
    /// <returns>The key.</returns>
    private static TabKey CreateKey(PdfPage page, ResolvedWidget widget, Dictionary<int, int>? ordinals)
    {
        // Rows and columns follow what the reader sees, so the position is in viewer space, where y grows downwards.
        var rect = widget.Widget.TryGetRectangle(KnownName.Rect, out var found) ? page.ToViewerRectangle(found) : default;
        var ordinal = widget.WidgetId.IsValid && ordinals is not null && ordinals.TryGetValue(widget.WidgetId.Number, out var place) ? place : UnlistedOrdinal;
        return new(widget.Index, rect.Left, rect.Bottom, ordinal);
    }

    /// <summary>The position of a widget for tab ordering.</summary>
    /// <param name = "Index">The widget's index in the page's annotations.</param>
    /// <param name = "X">The left edge in viewer space.</param>
    /// <param name = "Y">The top edge in viewer space.</param>
    /// <param name = "Ordinal">The place of the widget in the structure tree.</param>
    private readonly record struct TabKey(int Index, float X, float Y, int Ordinal);
}
