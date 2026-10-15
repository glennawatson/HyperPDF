// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Turns the nodes of an inferred layout into the structure elements the optimiser writes: headings, paragraphs, tables
/// (Table, TR, TH, TD), lists (L, LI, Lbl, LBody), captions, figures and sidebars (Div). A node claims the units its glyphs
/// came from; units no node claims stay artifacts, which is where pagination artifacts end up.
/// </summary>
internal static class TagBlocks
{
    /// <summary>Makes the elements of a page's layout, in reading order, into the build's element list.</summary>
    /// <param name="layout">The inferred layout.</param>
    /// <param name="build">What the conversion reads and updates.</param>
    /// <param name="guessLanguage">Whether to carry each node's guessed language into its element.</param>
    internal static void Build(PdfSemanticPage layout, ref TagBuild build, bool guessLanguage)
    {
        for (var i = 0; i < layout.Nodes.Count; i++)
        {
            var node = layout.Nodes[i];
            if (node.Role == PdfSemanticRole.Figure)
            {
                AddFigure(node, ref build, guessLanguage);
            }
            else
            {
                _ = Convert(node, ref build, guessLanguage);
            }
        }
    }

    /// <summary>Determines whether every unit has an owning element.</summary>
    /// <param name="owners">The block owning each unit, or -1.</param>
    /// <returns><see langword="true"/> when every unit is owned.</returns>
    internal static bool AllOwned(ReadOnlySpan<int> owners) => owners.IndexOf(-1) < 0;

    /// <summary>Adds a figure for each image unit no element claimed, after the element whose content comes before it.</summary>
    /// <param name="build">The elements and the units' owners.</param>
    internal static void AddUnclaimedFigures(ref TagBuild build)
    {
        for (var unit = 0; unit < build.Units.Length; unit++)
        {
            if (build.Units[unit].Kind != TagUnitKind.Figure || build.Owners[unit] >= 0)
            {
                continue;
            }

            var position = 0;
            for (var at = 0; at < build.NodeCount; at += build.Nodes[at].Size)
            {
                position = FirstUnit(ref build, at) < unit ? at + build.Nodes[at].Size : position;
            }

            build.Owners[unit] = 0;
            var start = build.OwnedCount;
            build.AddOwned(unit);
            build.InsertNode(position, new(OptimizerNames.Figure, start, 1, 1, true, null, null, null));
        }
    }

    /// <summary>Gets the first unit of an element, which places it in content order.</summary>
    /// <param name="build">The elements and the units they own.</param>
    /// <param name="index">The element's index.</param>
    /// <returns>The unit, or <see cref="int.MaxValue"/> when the element holds none.</returns>
    private static int FirstUnit(ref TagBuild build, int index)
    {
        var first = int.MaxValue;
        var end = index + build.Nodes[index].Size;
        for (var i = index; i < end; i++)
        {
            var node = build.Nodes[i];
            if (node.UnitCount > 0)
            {
                first = Math.Min(first, build.OwnedUnits[node.UnitStart]);
            }
        }

        return first;
    }

    /// <summary>Makes the element of a node, with the elements of its children, at the end of the element list.</summary>
    /// <param name="node">The node.</param>
    /// <param name="build">What the conversion reads and updates.</param>
    /// <param name="guessLanguage">Whether to carry the guessed language.</param>
    /// <returns><see langword="true"/> when an element was made; <see langword="false"/> when the node holds nothing to tag.</returns>
    private static bool Convert(PdfSemanticNode node, ref TagBuild build, bool guessLanguage)
    {
        if (node.Children.Count == 0)
        {
            return Leaf(node, ref build, guessLanguage);
        }

        var start = build.NodeCount;
        build.AddNode(new(TagFor(node), 0, 0, 1, false, null, null, null));

        // A list item's label and body are often one text operator, which cannot be split; the body claims it and the optional label is left out.
        var reverse = node.Role == PdfSemanticRole.ListItem;
        var starts = default(RentedList<int>);
        try
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                var childStart = build.NodeCount;
                if (Convert(node.Children[reverse ? node.Children.Count - 1 - i : i], ref build, guessLanguage) && reverse)
                {
                    starts.Add(childStart);
                }
            }

            if (reverse)
            {
                ReverseSiblings(ref build, start + 1, starts.Span);
            }
        }
        finally
        {
            starts.Dispose();
        }

        if (build.NodeCount == start + 1)
        {
            build.TruncateNodes(start);
            return false;
        }

        build.SetNode(start, build.Nodes[start] with { Size = build.NodeCount - start });
        return true;
    }

    /// <summary>Puts sibling subtrees that were built last to first back in order.</summary>
    /// <param name="build">The elements.</param>
    /// <param name="first">The index where the first subtree starts.</param>
    /// <param name="starts">The index each subtree starts at, in the order they were built.</param>
    private static void ReverseSiblings(ref TagBuild build, int first, ReadOnlySpan<int> starts)
    {
        if (starts.Length < 2)
        {
            return;
        }

        var scratch = ArrayPool<TagNode>.Shared.Rent(build.NodeCount - first);
        try
        {
            build.ReverseSubtrees(first, scratch, starts);
        }
        finally
        {
            ArrayPool<TagNode>.Shared.Return(scratch, true);
        }
    }

    /// <summary>Makes the element of a node that holds text directly.</summary>
    /// <param name="node">The node.</param>
    /// <param name="build">What the conversion reads and updates.</param>
    /// <param name="guessLanguage">Whether to carry the guessed language.</param>
    /// <returns><see langword="true"/> when an element was made; <see langword="false"/> when the node has no content and is not a table cell.</returns>
    private static bool Leaf(PdfSemanticNode node, ref TagBuild build, bool guessLanguage)
    {
        build.Scratch.Clear();
        node.CollectItems(build.Scratch);
        var start = build.OwnedCount;
        OwnUnits(ref build, start);
        var count = build.OwnedCount - start;
        var isCell = node.Role is PdfSemanticRole.TableCell or PdfSemanticRole.TableHeaderCell;
        if (count == 0 && !isCell)
        {
            return false;
        }

        build.AddNode(new(
            TagFor(node),
            start,
            count,
            1,
            false,
            guessLanguage ? node.InferredLanguage : null,
            null,
            isCell ? new TagCell(node.Scope, node.RowSpan, node.ColumnSpan) : null));
        return true;
    }

    /// <summary>Determines whether a unit is an image no element has claimed.</summary>
    /// <param name="unit">The unit.</param>
    /// <param name="build">What the conversion reads.</param>
    /// <returns><see langword="true"/> for an unclaimed image unit.</returns>
    private static bool IsFreeFigure(int unit, ref TagBuild build) =>
        (uint)unit < (uint)build.Units.Length && build.Units[unit].Kind == TagUnitKind.Figure && build.Owners[unit] < 0;

    /// <summary>Adds a figure for an image unit, with its caption as a neighbouring element.</summary>
    /// <param name="node">The figure node.</param>
    /// <param name="build">What the conversion reads and updates.</param>
    /// <param name="guessLanguage">Whether to carry the guessed language.</param>
    private static void AddFigure(PdfSemanticNode node, ref TagBuild build, bool guessLanguage)
    {
        var unit = node.Mcid;
        if (!IsFreeFigure(unit, ref build))
        {
            return;
        }

        build.Owners[unit] = 0;
        var unitStart = build.OwnedCount;
        build.AddOwned(unit);
        var figure = new TagNode(OptimizerNames.Figure, unitStart, 1, 1, true, null, node.AlternateText, null);
        var caption = node.Children.Count > 0 ? node.Children[0] : null;
        var at = build.NodeCount;
        var captioned = caption is { Role: PdfSemanticRole.Caption } && Convert(caption, ref build, guessLanguage);
        var before = caption is not null && caption.Bounds.Bottom <= node.Bounds.Top + float.Epsilon;

        // The caption goes before the figure when it sits above it, and after it otherwise.
        build.InsertNode(before && captioned ? build.NodeCount : at, figure);
    }

    /// <summary>Gets the structure type of a layout node.</summary>
    /// <param name="node">The node.</param>
    /// <returns>The type.</returns>
    private static PdfName TagFor(PdfSemanticNode node) => node.Role switch
    {
        PdfSemanticRole.Heading => OptimizerNames.Heading(node.Level),
        PdfSemanticRole.List => OptimizerNames.List,
        PdfSemanticRole.ListItem => OptimizerNames.ListItem,
        PdfSemanticRole.ListLabel => OptimizerNames.ListLabel,
        PdfSemanticRole.ListBody => OptimizerNames.ListBody,
        PdfSemanticRole.Table => OptimizerNames.Table,
        PdfSemanticRole.TableRow => OptimizerNames.TableRow,
        PdfSemanticRole.TableHeaderCell => OptimizerNames.TableHeader,
        PdfSemanticRole.TableCell => OptimizerNames.TableData,
        PdfSemanticRole.Caption => OptimizerNames.Caption,
        PdfSemanticRole.Group => OptimizerNames.Division,
        _ => KnownName.P,
    };

    /// <summary>Claims the units the listed glyphs came from that no earlier node claimed.</summary>
    /// <param name="build">What the conversion reads and updates; the glyph indices are in the scratch list.</param>
    /// <param name="start">Where the claimed units start in the owned list.</param>
    private static void OwnUnits(ref TagBuild build, int start)
    {
        if (build.Content is not { } content)
        {
            return;
        }

        foreach (var item in build.Scratch)
        {
            var mcid = (uint)item < (uint)content.GlyphCount ? content.Glyphs[item].Mcid : -1;
            if ((uint)mcid >= (uint)build.Owners.Length || build.Owners[mcid] >= 0)
            {
                continue;
            }

            build.Owners[mcid] = 0;
            build.AddOwned(mcid);
        }

        build.SortOwned(start);
    }
}
