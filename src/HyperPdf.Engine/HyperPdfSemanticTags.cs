// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdf.Core.Reading;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine;

/// <summary>Finds the empty table cells and the artifacts of a tagged page, which the reading blocks leave out.</summary>
internal static class HyperPdfSemanticTags
{
    /// <summary>The most artifacts listed for a page, so a page of decoration cannot flood the reader.</summary>
    private const int MaxArtifacts = 64;

    /// <summary>The most table cells listed as empty for a page.</summary>
    private const int MaxEmptyCells = 512;

    /// <summary>The characters of stack space for the text of an artifact; a longer run uses a rented array.</summary>
    private const int StackText = 256;

    /// <summary>Adds a tagged page's empty cells, then its artifacts.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="items">Receives the items.</param>
    internal static void Add(HyperPdfDocument self, int pageIndex, List<PageSemanticItem> items)
    {
        if (PdfDocumentTagged.GetStructureTree(self.Document) is not { IsMarked: true })
        {
            return;
        }

        var page = PdfReadingStructure.ReadTagged(self.Document, pageIndex);
        var cells = new List<int>();
        var nodes = page.Nodes;
        for (var i = 0; i < nodes.Count; i++)
        {
            AddEmptyCells(nodes[i], items, cells, 0);
        }

        if (page.Content is { } content)
        {
            AddArtifacts(content, items);
        }
    }

    /// <summary>Adds the empty table cells of a node and the nodes under it.</summary>
    /// <param name="node">The node.</param>
    /// <param name="items">Receives the items.</param>
    /// <param name="scratch">A list reused to look for a cell's content.</param>
    /// <param name="depth">How far below the top the node is.</param>
    private static void AddEmptyCells(PdfSemanticNode node, List<PageSemanticItem> items, List<int> scratch, int depth)
    {
        if (depth > PdfLimits.MaxNesting || items.Count >= MaxEmptyCells)
        {
            return;
        }

        if (node.Element?.Type is PdfStructureType.TableDataCell or PdfStructureType.TableHeaderCell && IsEmpty(node, scratch))
        {
            items.Add(DescribeEmptyCell(node));
        }

        var children = node.Children;
        for (var i = 0; i < children.Count; i++)
        {
            AddEmptyCells(children[i], items, scratch, depth + 1);
        }
    }

    /// <summary>Describes an empty cell, with its place in the table and its language when that differs from the row's.</summary>
    /// <param name="node">The cell.</param>
    /// <returns>The item.</returns>
    private static PageSemanticItem DescribeEmptyCell(PdfSemanticNode node)
    {
        var changed = node.Language is { } own && node.Parent is { } parent && !string.Equals(own, parent.Language, StringComparison.OrdinalIgnoreCase);
        return new(PageSemanticKind.EmptyCell, "Empty cell", default)
        {
            Role = "table cell",
            Row = node.Row >= 0 ? node.Row + 1 : 0,
            Column = node.Column >= 0 ? node.Column + 1 : 0,
            Language = changed ? node.Language : null,
        };
    }

    /// <summary>Determines whether a cell has nothing to read: no content, no replacement text and no description.</summary>
    /// <param name="node">The cell.</param>
    /// <param name="scratch">A list reused to look for content.</param>
    /// <returns><see langword="true"/> when the cell is empty.</returns>
    private static bool IsEmpty(PdfSemanticNode node, List<int> scratch)
    {
        scratch.Clear();
        node.CollectItems(scratch);
        return scratch.Count == 0 && node.ActualText is null && node.AlternateText is null;
    }

    /// <summary>Adds each run of artifact text, such as a running header or a page number.</summary>
    /// <param name="content">What the page draws.</param>
    /// <param name="items">Receives the items.</param>
    private static void AddArtifacts(PdfMarkedContentPage content, List<PageSemanticItem> items)
    {
        var added = 0;
        var at = 0;
        while (at < content.GlyphCount && added < MaxArtifacts)
        {
            if (!content.Glyphs[at].IsArtifact)
            {
                at++;
                continue;
            }

            // A run of artifact glyphs is one item: its text, trimmed, and the box that holds its glyphs.
            var start = at;
            var bounds = default(PdfViewerRect);
            var length = 0;
            for (; at < content.GlyphCount && content.Glyphs[at].IsArtifact; at++)
            {
                length += content.GetText(at).Length;
                bounds = bounds.Union(content.Glyphs[at].Bounds);
            }

            if (RunText(content, start, at, length) is not { } words)
            {
                continue;
            }

            items.Add(new(PageSemanticKind.Artifact, words, new(bounds.Left, bounds.Top, bounds.Width, bounds.Height)) { Role = "artifact" });
            added++;
        }
    }

    /// <summary>Gathers the text of a run of glyphs without an intermediate string, so the trimmed text is the only allocation.</summary>
    /// <param name="content">What the page draws.</param>
    /// <param name="start">The first glyph.</param>
    /// <param name="end">The glyph after the last.</param>
    /// <param name="length">The characters the glyphs hold together.</param>
    /// <returns>The text with its white space trimmed, or <see langword="null"/> when nothing is left.</returns>
    private static string? RunText(PdfMarkedContentPage content, int start, int end, int length)
    {
        char[]? rented = null;
        try
        {
            Span<char> buffer = length <= StackText ? stackalloc char[StackText] : (rented = ArrayPool<char>.Shared.Rent(length));
            var written = 0;
            for (var i = start; i < end; i++)
            {
                var piece = content.GetText(i);
                piece.CopyTo(buffer[written..]);
                written += piece.Length;
            }

            var trimmed = buffer[..written].Trim();
            return trimmed.IsEmpty ? null : new string(trimmed);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
