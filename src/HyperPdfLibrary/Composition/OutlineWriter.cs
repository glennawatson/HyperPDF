// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Composition;

/// <summary>Writes the document outline (ISO 32000-2 12.3.3), the tree of headings a reader shows beside the pages.</summary>
internal static class OutlineWriter
{
    /// <summary>The entries of an outline item dictionary.</summary>
    private const int ItemEntries = 8;

    /// <summary>Writes the outline.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="entries">The entries, in reading order.</param>
    /// <param name="destinations">Where each entry's destination name is.</param>
    /// <param name="pageIds">The object of each page, in order.</param>
    /// <returns>The outline root's object, or an invalid id when there are no entries.</returns>
    internal static PdfObjectId Write(PdfDocumentBuilder builder, List<OutlineEntry> entries, Dictionary<string, DestinationPoint> destinations, List<PdfObjectId> pageIds)
    {
        var roots = new List<OutlineNode>();
        var stack = new List<OutlineNode>();
        foreach (var entry in entries)
        {
            if (!destinations.ContainsKey(entry.Destination))
            {
                continue;
            }

            while (stack.Count > 0 && stack[^1].Entry.Level >= entry.Level)
            {
                stack.RemoveAt(stack.Count - 1);
            }

            var parent = stack.Count > 0 ? stack[^1] : null;
            var node = new OutlineNode(entry, builder.Reserve(), parent);
            (parent?.Children ?? roots).Add(node);
            stack.Add(node);
        }

        if (roots.Count == 0)
        {
            return default;
        }

        var rootId = builder.Reserve();
        WriteChildren(builder, roots, rootId, destinations, pageIds);
        var root = new PdfDictionary(builder.Owner, ItemEntries);
        root.Set(KnownName.Type, PdfValue.FromName(KnownName.Outlines));
        root.Set(KnownName.First, PdfValue.FromReference(roots[0].Id));
        root.Set(KnownName.Last, PdfValue.FromReference(roots[^1].Id));
        root.Set(KnownName.Count, PdfValue.FromInteger(CountAll(roots)));
        builder.Set(rootId, PdfValue.FromDictionary(root));
        return rootId;
    }

    /// <summary>Counts entries at every level.</summary>
    /// <param name="nodes">The top level entries.</param>
    /// <returns>The count.</returns>
    private static int CountAll(List<OutlineNode> nodes)
    {
        var count = nodes.Count;
        foreach (var node in nodes)
        {
            count += node.Descendants;
        }

        return count;
    }

    /// <summary>Writes entries that share a parent, and everything below them.</summary>
    /// <param name="builder">The document.</param>
    /// <param name="nodes">The entries.</param>
    /// <param name="parentId">The parent's object.</param>
    /// <param name="destinations">Where each entry's destination name is.</param>
    /// <param name="pageIds">The object of each page, in order.</param>
    private static void WriteChildren(PdfDocumentBuilder builder, List<OutlineNode> nodes, PdfObjectId parentId, Dictionary<string, DestinationPoint> destinations, List<PdfObjectId> pageIds)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var item = new PdfDictionary(builder.Owner, ItemEntries);
            item.Set(KnownName.Title, PdfValue.FromString(PdfText.Encode(node.Entry.Title)));
            item.Set(KnownName.Parent, PdfValue.FromReference(parentId));
            if (i > 0)
            {
                item.Set(KnownName.Prev, PdfValue.FromReference(nodes[i - 1].Id));
            }

            if (i + 1 < nodes.Count)
            {
                item.Set(KnownName.Next, PdfValue.FromReference(nodes[i + 1].Id));
            }

            if (node.Children.Count > 0)
            {
                item.Set(KnownName.First, PdfValue.FromReference(node.Children[0].Id));
                item.Set(KnownName.Last, PdfValue.FromReference(node.Children[^1].Id));
                item.Set(KnownName.Count, PdfValue.FromInteger(node.Descendants));
            }

            var point = destinations[node.Entry.Destination];
            item.Set(KnownName.Dest, DestinationArrays.Create(builder, pageIds[point.Page - 1], point));
            builder.Set(node.Id, PdfValue.FromDictionary(item));
            WriteChildren(builder, node.Children, node.Id, destinations, pageIds);
        }
    }
}
