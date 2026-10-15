// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads page labels.</summary>
public static class PdfDocumentLabels
{
    /// <summary>The largest temporary list retained between reads.</summary>
    private const int MaxKeptRanges = 4096;

    /// <summary>Gets a page's label, for example "iv".</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The zero based page index.</param>
    /// <returns>The label, or <see langword="null"/> when the document defines no labels or the label is empty.</returns>
    public static string? GetPageLabel(PdfDocument document, int index)
    {
        if ((uint)index >= (uint)document.PageCount || document.Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        // An empty string stands for a label that is null, so a slot that was never read stays null.
        var revision = PdfDocumentCaches.Revision(document);
        var labels = PdfDocumentCaches.PerPage(document, ref document.State.PageLabels, revision);
        if ((uint)index >= (uint)labels.Length)
        {
            // An edit can shrink the page set between the initial range check and cache creation.
            return null;
        }

        var cached = Volatile.Read(ref labels[index]);
        if (cached is null)
        {
            cached = PdfDocumentLabels.ReadPageLabel(document, index) ?? string.Empty;
            Volatile.Write(ref labels[index], cached);
        }

        return cached.Length == 0 ? null : cached;
    }

    /// <summary>Gets the labels of some pages as style and number, for pages copied into another document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <returns>The label of each page, or <see langword="null"/> when the document defines no labels.</returns>
    internal static PdfPageLabel[]? CollectSourceLabels(PdfDocument document, ReadOnlySpan<PdfPage> pages)
    {
        if (document.Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        // Pages before the first range are labelled with their decimal page number; one shared style keeps them in runs.
        var ranges = PdfDocumentLabels.GetLabelRanges(document);
        var decimalStyle = new PdfDictionary(document.Objects);
        decimalStyle.Set(KnownName.S, PdfValue.FromName(KnownName.D));
        var labels = new PdfPageLabel[pages.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var index = pages[i].Index;
            labels[i] = PdfDocumentLabels.FindRange(ranges, index) is { } range ? new(range.Label, index - range.Start + range.Label.GetInt32(KnownName.St, 1)) : new(decimalStyle, index + 1);
        }

        return labels;
    }

    /// <summary>Finds the range with the largest start at or before a page.</summary>
    /// <param name="ranges">The sorted ranges.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The range, or <see langword="null"/>.</returns>
    internal static PageLabelRange? FindRange(PageLabelRange[] ranges, int index)
    {
        PageLabelRange? found = null;
        foreach (var range in ranges)
        {
            if (range.Start > index)
            {
                break;
            }

            found = range;
        }

        return found;
    }

    /// <summary>Reads the label ranges once.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The ranges, sorted by start page.</returns>
    internal static PageLabelRange[] GetLabelRanges(PdfDocument document)
    {
        var revision = PdfDocumentCaches.Revision(document);
        var ranges = Volatile.Read(ref document.State.LabelRanges);
        if (ranges is not null)
        {
            return ranges;
        }

        var entries = document.Objects.ScratchPools.Slots<List<NameTreeEntry>>().Take() ?? [];
        var list = document.Objects.ScratchPools.Slots<List<PageLabelRange>>().Take() ?? [];
        try
        {
            NameTree.EnumerateNumbers(document.Catalog.GetDictionary(KnownName.PageLabels), entries);
            foreach (var entry in entries)
            {
                if (entry.Key.IsNumber && entry.Value.AsDictionary() is { } label)
                {
                    list.Add(new(entry.Key.AsInt32(), label));
                }
            }

            list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
            ranges = [.. list];
            return PdfDocumentCaches.Publish(document, ref document.State.LabelRanges, ranges, revision);
        }
        finally
        {
            ReturnLists(document, entries, list);
        }
    }

    /// <summary>Clears temporary lists and retains bounded buffers.</summary>
    /// <param name="document">The owner of the working lists.</param>
    /// <param name="entries">The temporary tree entries.</param>
    /// <param name="ranges">The temporary ranges.</param>
    private static void ReturnLists(PdfDocument document, List<NameTreeEntry> entries, List<PageLabelRange> ranges)
    {
        var keep = entries.Capacity <= MaxKeptRanges && ranges.Capacity <= MaxKeptRanges;
        entries.Clear();
        ranges.Clear();
        if (!keep)
        {
            return;
        }

        _ = document.Objects.ScratchPools.Slots<List<NameTreeEntry>>().Offer(entries);
        _ = document.Objects.ScratchPools.Slots<List<PageLabelRange>>().Offer(ranges);
    }

    /// <summary>Reads a page's label.</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The zero based page index.</param>
    /// <returns>The label, or <see langword="null"/> when the document defines no labels or the label is empty.</returns>
    private static string? ReadPageLabel(PdfDocument document, int index)
    {
        if (document.Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        var ranges = PdfDocumentLabels.GetLabelRanges(document);
        var range = PdfDocumentLabels.FindRange(ranges, index);
        if (range is null)
        {
            return (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        var dictionary = range.Value.Label;
        var prefix = dictionary.Get(KnownName.P);
        var number = index - range.Value.Start + dictionary.GetInt32(KnownName.St, 1);
        var style = document.Objects.Names.GetSpelling(dictionary.GetName(KnownName.S));
        return PageLabelText.Create(prefix.Kind == PdfKind.String ? prefix.AsStringBytes() : default, number, style.Length == 1 ? style[0] : default);
    }
}
