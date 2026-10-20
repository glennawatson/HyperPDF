// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Documents;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;

namespace HyperPdf.Engine;

/// <summary>Reads a document's part tree (<c>/DPartRoot</c>) and article threads (<c>/Threads</c>) for the structure panel.</summary>
internal static class HyperPdfDocumentParts
{
    /// <summary>The most parts listed, so a damaged tree cannot flood the panel.</summary>
    private const int MaxParts = 5000;

    /// <summary>The most pairs of metadata shown for one part.</summary>
    private const int MaxDetails = 8;

    /// <summary>The metadata keys that name a part, best first.</summary>
    private static readonly string[] NameKeys = ["Title", "Name", "DocumentName", "Label", "ID"];

    /// <summary>Gets the document's parts.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The top-level parts; empty when the document has none or the tree cannot be read.</returns>
    internal static IReadOnlyList<DocumentPart> GetParts(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return [];
        }

        try
        {
            if (PdfDocumentWebCapture.GetDocumentParts(self.Document)?.Root is not { } top)
            {
                return [];
            }

            var budget = MaxParts;
            var level = top.Children.Length > 0 ? top.Children : [top];
            return ConvertLevel(level, self.Document.PageCount, 0, ref budget);
        }
        catch (PdfException)
        {
            // A damaged part tree only means no parts are listed; the pages still show.
            return [];
        }
    }

    /// <summary>Gets the document's article threads.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The threads; empty when the document has none or they cannot be read.</returns>
    internal static IReadOnlyList<ArticleThread> GetThreads(HyperPdfDocument self)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        if (self.IsDisposed)
        {
            return [];
        }

        try
        {
            var threads = PdfDocumentCatalog.GetThreads(self.Document);
            var result = new List<ArticleThread>(threads.Length);
            for (var i = 0; i < threads.Length; i++)
            {
                var beads = ConvertBeads(self, threads[i].Beads);
                if (beads.Count > 0)
                {
                    result.Add(new(threads[i].Title is { Length: > 0 } title ? title : string.Create(CultureInfo.CurrentCulture, $"Article {i + 1}"), beads));
                }
            }

            return result;
        }
        catch (PdfException)
        {
            return [];
        }
    }

    /// <summary>Converts the parts of one level, and below them.</summary>
    /// <param name="nodes">The library's parts.</param>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="depth">How far below the top the level is.</param>
    /// <param name="budget">The parts that may still be listed.</param>
    /// <returns>The parts.</returns>
    private static List<DocumentPart> ConvertLevel(PdfDocumentPartNode[] nodes, int pageCount, int depth, ref int budget)
    {
        var result = new List<DocumentPart>(nodes.Length);
        for (var i = 0; i < nodes.Length && budget > 0; i++)
        {
            budget--;
            var node = nodes[i];
            var children = depth < PdfLimits.MaxNesting ? ConvertLevel(node.Children, pageCount, depth + 1, ref budget) : [];
            var (name, details) = Describe(node.Metadata, i);
            var (start, end) = PagesOf(node, pageCount);
            result.Add(new(name, details, start, end, children));
        }

        return result;
    }

    /// <summary>Limits a part's pages to the pages the document has.</summary>
    /// <param name="node">The library's part.</param>
    /// <param name="pageCount">The number of pages.</param>
    /// <returns>The first and last page, or none when the part names no page the document has.</returns>
    private static PageSpan PagesOf(PdfDocumentPartNode node, int pageCount) =>
        node.StartPage is { } first && first < pageCount ? new(first, Math.Min(node.EndPage ?? first, pageCount - 1)) : new(null, null);

    /// <summary>Works out a part's name and the details beneath it from its metadata.</summary>
    /// <param name="metadata">The part's metadata.</param>
    /// <param name="position">The part's position among its siblings.</param>
    /// <returns>The name and the details.</returns>
    private static NameAndDetails Describe(IReadOnlyDictionary<string, string> metadata, int position)
    {
        var named = FindName(metadata);
        return new(named?.Value ?? string.Create(CultureInfo.CurrentCulture, $"Part {position + 1}"), FormatDetails(metadata, named?.Key));
    }

    /// <summary>Finds the metadata entry that names a part.</summary>
    /// <param name="metadata">The part's metadata.</param>
    /// <returns>The entry, or <see langword="null"/> when none of the naming keys has a value.</returns>
    private static KeyValuePair<string, string>? FindName(IReadOnlyDictionary<string, string> metadata)
    {
        foreach (var key in NameKeys)
        {
            foreach (var entry in metadata)
            {
                if (entry.Value.Length > 0 && string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    /// <summary>Writes a part's metadata as "key: value" pairs, leaving out the entry that names it.</summary>
    /// <param name="metadata">The part's metadata.</param>
    /// <param name="nameKey">The key that names the part, or <see langword="null"/>.</param>
    /// <returns>The pairs, in key order.</returns>
    private static string FormatDetails(IReadOnlyDictionary<string, string> metadata, string? nameKey)
    {
        var keys = new List<string>(metadata.Keys);
        keys.Sort(StringComparer.Ordinal);
        var details = new StringBuilder();
        var shown = 0;
        foreach (var key in keys)
        {
            if (key == nameKey || metadata[key].Length == 0)
            {
                continue;
            }

            if (shown >= MaxDetails)
            {
                break;
            }

            _ = details.Append(details.Length == 0 ? string.Empty : "; ").Append(key).Append(": ").Append(metadata[key]);
            shown++;
        }

        return details.ToString();
    }

    /// <summary>Converts a thread's beads, dropping those on pages the document does not have.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="beads">The library's beads.</param>
    /// <returns>The beads, in reading order.</returns>
    private static List<ArticleBead> ConvertBeads(HyperPdfDocument self, PdfBead[] beads)
    {
        var result = new List<ArticleBead>(beads.Length);
        foreach (var bead in beads)
        {
            if ((uint)bead.PageIndex >= (uint)self.Document.PageCount)
            {
                continue;
            }

            var bounds = bead.Bounds is { } area ? HyperPdfAnnotationReading.ToPageRect(PdfDocumentPages.GetPage(self.Document, bead.PageIndex), area) : (HyperPdf.Core.Geometry.PageRect?)null;
            result.Add(new(bead.PageIndex, bounds));
        }

        return result;
    }

    /// <summary>The first and last page of a part.</summary>
    /// <param name="Start">The zero based first page, or <see langword="null"/>.</param>
    /// <param name="End">The zero based last page, or <see langword="null"/>.</param>
    private readonly record struct PageSpan(int? Start, int? End);

    /// <summary>A part's name and the details beneath it.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Details">The details.</param>
    private readonly record struct NameAndDetails(string Name, string Details);
}
