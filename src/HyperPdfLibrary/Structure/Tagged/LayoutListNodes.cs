// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Makes the nodes of an inferred list: L, and for each item LI with its Lbl label and LBody text (ISO 32000-2 14.8.4.3.3).</summary>
internal static class LayoutListNodes
{
    /// <summary>Makes a list's nodes.</summary>
    /// <param name="nodes">Makes the nodes.</param>
    /// <param name="run">The lines the items are runs of.</param>
    /// <param name="listItems">The items of the list.</param>
    /// <param name="numbering">How the list's labels are made.</param>
    /// <param name="items">The array of layout items the lines are runs of.</param>
    /// <param name="buffer">Room for a node's text.</param>
    /// <returns>The list node.</returns>
    internal static PdfSemanticNode Make(LayoutNodes nodes, ReadOnlySpan<LayoutLine> run, ReadOnlySpan<ListItemSpan> listItems, PdfListNumbering numbering, LayoutItem[] items, char[] buffer)
    {
        var list = nodes.MakeGroup(PdfSemanticRole.List);
        list.ListNumbering = numbering;
        foreach (var item in listItems)
        {
            LayoutNodes.Adopt(list, MakeItem(nodes, run, item, items, buffer));
        }

        return list;
    }

    /// <summary>Makes an item's nodes.</summary>
    /// <param name="nodes">Makes the nodes.</param>
    /// <param name="run">The lines.</param>
    /// <param name="span">The item.</param>
    /// <param name="items">The array of layout items.</param>
    /// <param name="buffer">Room for a node's text.</param>
    /// <returns>The list item node.</returns>
    private static PdfSemanticNode MakeItem(LayoutNodes nodes, ReadOnlySpan<LayoutLine> run, in ListItemSpan span, LayoutItem[] items, char[] buffer)
    {
        var first = run[span.Start];
        var labelItems = span.Label.ItemCount;
        var label = new LayoutLine(first.Start, labelItems, Bounds(items, first.Start, labelItems));
        var body = new LayoutLine[span.End - span.Start];
        body[0] = new(first.Start + labelItems, first.Count - labelItems, Bounds(items, first.Start + labelItems, first.Count - labelItems));
        run[(span.Start + 1)..span.End].CopyTo(body.AsSpan(1));
        var node = nodes.MakeGroup(PdfSemanticRole.ListItem);
        LayoutNodes.Adopt(node, nodes.Fill(PdfSemanticRole.ListLabel, nodes.Compose([label], items, buffer), buffer));
        LayoutNodes.Adopt(node, nodes.Fill(PdfSemanticRole.ListBody, nodes.Compose(body, items, buffer), buffer));
        return node;
    }

    /// <summary>Gets the box around some items.</summary>
    /// <param name="items">The array of layout items.</param>
    /// <param name="start">The position of the first item.</param>
    /// <param name="count">The number of items.</param>
    /// <returns>The box.</returns>
    private static PdfViewerRect Bounds(LayoutItem[] items, int start, int count)
    {
        var bounds = default(PdfViewerRect);
        for (var i = start; i < start + count; i++)
        {
            bounds = bounds.Union(items[i].Bounds);
        }

        return bounds;
    }
}
