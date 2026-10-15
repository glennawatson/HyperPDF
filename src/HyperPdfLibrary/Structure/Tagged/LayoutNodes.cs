// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Makes the reading nodes of a page's layout: paragraphs, headings, lists, tables, figures and pagination artifacts.</summary>
[DebuggerDisplay("LayoutNodes: page {_pageIndex} ({_origin})")]
internal sealed class LayoutNodes
{
    /// <summary>The share of body text height a heading's lines reach.</summary>
    internal const float HeadingRatio = 1.3F;

    /// <summary>The share of body text height a level 1 heading's lines reach.</summary>
    private const float TopHeadingRatio = 1.8F;

    /// <summary>The most lines a heading has.</summary>
    private const int MaxHeadingLines = 3;

    /// <summary>The level of a smaller inferred heading.</summary>
    private const int SecondLevel = 2;

    /// <summary>Where the nodes come from.</summary>
    private readonly PdfNodeOrigin _origin;

    /// <summary>The zero based page index.</summary>
    private readonly int _pageIndex;

    /// <summary>The items' text, back to back.</summary>
    private readonly string _text;

    /// <summary>The median item height: the body text size.</summary>
    private readonly float _bodyHeight;

    /// <summary>Whether to guess the language of text.</summary>
    private readonly bool _guessLanguage;

    /// <summary>Initializes a new instance of the <see cref="LayoutNodes"/> class.</summary>
    /// <param name="origin">Where the nodes come from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="text">The items' text, back to back.</param>
    /// <param name="bodyHeight">The body text height.</param>
    /// <param name="guessLanguage">Whether to guess the language of text.</param>
    internal LayoutNodes(PdfNodeOrigin origin, int pageIndex, string text, float bodyHeight, bool guessLanguage)
    {
        _origin = origin;
        _pageIndex = pageIndex;
        _text = text;
        _bodyHeight = bodyHeight;
        _guessLanguage = guessLanguage;
    }

    /// <summary>Adds a child to a grouping node, growing its box to cover the child.</summary>
    /// <param name="parent">The grouping node.</param>
    /// <param name="child">The child.</param>
    internal static void Adopt(PdfSemanticNode parent, PdfSemanticNode child)
    {
        parent.Add(child);
        parent.Bounds = parent.Bounds.Union(child.Bounds);
    }

    /// <summary>Joins the text of lines.</summary>
    /// <param name="lines">The lines, top to bottom.</param>
    /// <param name="items">The array of items the lines are runs of.</param>
    /// <param name="buffer">Room for the text.</param>
    /// <returns>The joined text and where it came from.</returns>
    internal ComposedText Compose(ReadOnlySpan<LayoutLine> lines, LayoutItem[] items, char[] buffer)
    {
        var total = 0;
        foreach (var line in lines)
        {
            total += line.Count;
        }

        var indices = new int[total];
        var bounds = default(PdfViewerRect);
        var height = 0F;
        var length = 0;
        var written = 0;
        foreach (var line in lines)
        {
            LayoutText.Append(_text, items, line.Start, line.Count, line.Bounds.Height, buffer, ref length);
            for (var i = line.Start; i < line.Start + line.Count; i++)
            {
                indices[written] = items[i].Index;
                written++;
            }

            bounds = bounds.Union(line.Bounds);
            height += line.Bounds.Height;
        }

        return new(indices, bounds, height, length);
    }

    /// <summary>Makes a paragraph or heading from lines.</summary>
    /// <param name="lines">The lines, top to bottom.</param>
    /// <param name="items">The array of items the lines are runs of.</param>
    /// <param name="buffer">Room for the node's text.</param>
    /// <param name="column">The box around the region the lines are in, which their alignment is judged against.</param>
    /// <returns>The node.</returns>
    internal PdfSemanticNode MakeText(ReadOnlySpan<LayoutLine> lines, LayoutItem[] items, char[] buffer, PdfViewerRect column)
    {
        var composed = Compose(lines, items, buffer);
        var ratio = composed.Height / lines.Length / _bodyHeight;
        var heading = lines.Length <= MaxHeadingLines && ratio >= HeadingRatio;
        var level = ratio >= TopHeadingRatio ? 1 : SecondLevel;
        var node = Fill(heading ? PdfSemanticRole.Heading : PdfSemanticRole.Paragraph, composed, buffer);
        node.Level = heading ? level : 0;
        node.Alignment = LayoutAlignment.Detect(lines, column, _bodyHeight);
        return node;
    }

    /// <summary>Makes a node of some role from joined text.</summary>
    /// <param name="role">The role.</param>
    /// <param name="composed">The joined text.</param>
    /// <param name="buffer">The buffer the text was joined in.</param>
    /// <returns>The node.</returns>
    internal PdfSemanticNode Fill(PdfSemanticRole role, in ComposedText composed, char[] buffer)
    {
        var node = new PdfSemanticNode(role, _origin, _pageIndex) { Text = new(buffer.AsSpan(0, composed.Length).Trim()), Bounds = composed.Bounds };
        node.SetItems(composed.Indices);
        if (_guessLanguage)
        {
            node.InferredLanguage = LanguageGuesser.Guess(buffer.AsSpan(0, composed.Length));
        }

        return node;
    }

    /// <summary>Makes a grouping node whose text is its children's.</summary>
    /// <param name="role">The role.</param>
    /// <returns>The node, without children.</returns>
    internal PdfSemanticNode MakeGroup(PdfSemanticRole role)
    {
        var node = new PdfSemanticNode(role, _origin, _pageIndex);
        node.JoinChildText();
        return node;
    }

    /// <summary>Makes a figure.</summary>
    /// <param name="image">The image.</param>
    /// <returns>The node.</returns>
    internal PdfSemanticNode MakeFigure(in PdfMarkedImage image) =>
        new(PdfSemanticRole.Figure, _origin, _pageIndex) { Bounds = image.Bounds, Mcid = image.Mcid };

    /// <summary>Makes a pagination artifact from a line.</summary>
    /// <param name="found">The line and what kind of artifact it is.</param>
    /// <param name="items">The array of items the line is a run of.</param>
    /// <param name="buffer">Room for the node's text.</param>
    /// <returns>The node.</returns>
    internal PdfSemanticNode MakeArtifact(in PaginationLine found, LayoutItem[] items, char[] buffer)
    {
        var node = Fill(PdfSemanticRole.Artifact, Compose([found.Line], items, buffer), buffer);
        node.Pagination = found.Kind;
        return node;
    }
}
