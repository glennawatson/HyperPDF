// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Links captions to the figures and tables they describe. A caption is a short paragraph that starts with a word such
/// as "Figure" or "Table" and a number, and sits just above or below its figure or table, over the same part of the page.
/// The caption becomes the first or last child of the figure or table (ISO 32000-2 14.8.4.8.4).
/// </summary>
internal static class LayoutCaptions
{
    /// <summary>The farthest a caption may be from its figure or table, in body text heights.</summary>
    private const float MaxDistance = 3F;

    /// <summary>The share of the narrower box's width that must overlap for a caption to belong to a figure or table.</summary>
    private const float MinOverlap = 0.3F;

    /// <summary>The longest a caption is, in characters.</summary>
    private const int MaxLength = 400;

    /// <summary>The words that start a caption, followed by a number.</summary>
    private static readonly string[] Words = ["figure", "fig", "table", "tab", "chart", "graph", "diagram", "plate", "photo", "illustration", "exhibit"];

    /// <summary>Moves captions into the figures and tables they belong to.</summary>
    /// <param name="nodes">The page's top-level nodes in reading order; a caption is removed and added to its figure or table.</param>
    /// <param name="bodyHeight">The body text height.</param>
    internal static void Link(List<PdfSemanticNode> nodes, float bodyHeight)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.Role is not (PdfSemanticRole.Figure or PdfSemanticRole.Table))
            {
                continue;
            }

            var preferAfter = node.Role == PdfSemanticRole.Figure;
            var found = Find(nodes, i, preferAfter, bodyHeight) ?? Find(nodes, i, !preferAfter, bodyHeight);
            if (found is not { } index)
            {
                continue;
            }

            var paragraph = nodes[index];
            nodes.RemoveAt(index);
            i -= index < i ? 1 : 0;
            node.AddCaption(paragraph, paragraph.Bounds.Bottom <= node.Bounds.Top + float.Epsilon);
        }
    }

    /// <summary>Determines whether a text reads as a caption: a caption word and a number at its start.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for a caption.</returns>
    internal static bool IsCaption(ReadOnlySpan<char> text)
    {
        if (text.Length > MaxLength)
        {
            return false;
        }

        foreach (var word in Words)
        {
            if (text.StartsWith(word, StringComparison.OrdinalIgnoreCase) && NumberFollows(text[word.Length..]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a number follows a caption word, after an optional full stop and spaces.</summary>
    /// <param name="rest">The text after the word.</param>
    /// <returns><see langword="true"/> when the next character that is not a space or a full stop is a digit, after at most one of each.</returns>
    private static bool NumberFollows(ReadOnlySpan<char> rest)
    {
        if (!rest.IsEmpty && rest[0] == '.')
        {
            rest = rest[1..];
        }

        rest = rest.TrimStart();
        return !rest.IsEmpty && char.IsDigit(rest[0]);
    }

    /// <summary>Looks beside a figure or table for its caption.</summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="at">The figure or table's position.</param>
    /// <param name="after">Whether to look at the node after it rather than the one before.</param>
    /// <param name="bodyHeight">The body text height.</param>
    /// <returns>The caption's position, or <see langword="null"/> when the neighbour is not a caption for it.</returns>
    private static int? Find(List<PdfSemanticNode> nodes, int at, bool after, float bodyHeight)
    {
        var index = after ? at + 1 : at - 1;
        if ((uint)index >= (uint)nodes.Count)
        {
            return null;
        }

        var candidate = nodes[index];
        var target = nodes[at];
        if (candidate.Role != PdfSemanticRole.Paragraph || !IsCaption(candidate.Text))
        {
            return null;
        }

        var distance = after ? candidate.Bounds.Top - target.Bounds.Bottom : target.Bounds.Top - candidate.Bounds.Bottom;
        var overlap = LayoutGeometry.Overlap(candidate.Bounds.Left, candidate.Bounds.Right, target.Bounds.Left, target.Bounds.Right);
        var narrower = Math.Min(candidate.Bounds.Width, target.Bounds.Width);
        return distance <= MaxDistance * bodyHeight && overlap >= MinOverlap * narrower ? index : null;
    }
}
