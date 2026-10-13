// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Works out a reading order from geometry for pages without usable tags: items form lines, lines split at column
/// gaps, the page is cut recursively at its widest empty band (columns left to right, bands top to bottom), and each
/// region's lines group into paragraphs. Text much larger than the page's body text becomes a heading. Every node is
/// labelled with the origin it was given, so readers know the order is inferred. The items sit in one array in which
/// each line is a run, and each region a run of lines, so nothing but the nodes is allocated.
/// </summary>
[DebuggerDisplay("InferredLayoutBuilder: page {_pageIndex}, {_items.Count} items")]
internal sealed class InferredLayoutBuilder
{
    /// <summary>How far, as a share of height, two items' centres may differ and still sit on one line.</summary>
    private const float LineTolerance = 0.5F;

    /// <summary>The gap inside a line, in line heights, that separates two columns.</summary>
    private const float ColumnGapInLine = 1.2F;

    /// <summary>The least empty vertical band, in body text heights, that separates columns.</summary>
    private const float MinColumnGap = 1F;

    /// <summary>The least empty horizontal band, in body text heights, that separates blocks.</summary>
    private const float MinBandGap = 1F;

    /// <summary>The gap between lines, in line heights, that starts a new paragraph.</summary>
    private const float ParagraphGap = 0.6F;

    /// <summary>The change in line height that starts a new paragraph.</summary>
    private const float SizeChange = 1.25F;

    /// <summary>The share of body text height a heading's lines reach.</summary>
    private const float HeadingRatio = 1.3F;

    /// <summary>The share of body text height a level 1 heading's lines reach.</summary>
    private const float TopHeadingRatio = 1.8F;

    /// <summary>The most lines a heading has.</summary>
    private const int MaxHeadingLines = 3;

    /// <summary>The deepest recursive cut.</summary>
    private const int MaxCutDepth = 32;

    /// <summary>The gap between items, in line heights, that separates words.</summary>
    private const float WordGap = 0.15F;

    /// <summary>The level of a smaller inferred heading.</summary>
    private const int SecondLevel = 2;

    /// <summary>The divisor that finds a middle.</summary>
    private const int Halves = 2;

    /// <summary>The most numbers sorted on the stack before a rented array takes over.</summary>
    private const int StackNumbers = 256;

    /// <summary>Where the nodes come from.</summary>
    private readonly PdfNodeOrigin _origin;

    /// <summary>The zero based page index.</summary>
    private readonly int _pageIndex;

    /// <summary>The items' text, back to back.</summary>
    private readonly string _text;

    /// <summary>The items.</summary>
    private readonly List<LayoutItem> _items;

    /// <summary>The median item height: the body text size.</summary>
    private float _bodyHeight;

    /// <summary>Initializes a new instance of the <see cref="InferredLayoutBuilder"/> class.</summary>
    /// <param name="origin">Where the nodes come from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="text">The items' text, back to back.</param>
    /// <param name="items">The items.</param>
    internal InferredLayoutBuilder(PdfNodeOrigin origin, int pageIndex, string text, List<LayoutItem> items)
    {
        _origin = origin;
        _pageIndex = pageIndex;
        _text = text;
        _items = items;
    }

    /// <summary>Builds the nodes in reading order.</summary>
    /// <param name="output">Receives the nodes.</param>
    internal void Build(List<PdfSemanticNode> output)
    {
        var count = _items.Count;
        if (count == 0)
        {
            return;
        }

        var sorted = ArrayPool<LayoutItem>.Shared.Rent(count);
        var lines = ArrayPool<LayoutLine>.Shared.Rent(count);
        var moved = ArrayPool<LayoutLine>.Shared.Rent(count);
        var regions = ArrayPool<LayoutRegion>.Shared.Rent(count);
        var text = ArrayPool<char>.Shared.Rent(TextLength(count));
        try
        {
            _items.CopyTo(sorted);
            _bodyHeight = MedianHeight(sorted.AsSpan(0, count));
            var lineCount = BuildLines(sorted, count, lines);
            var regionCount = 0;
            Cut(lines, moved, new(0, lineCount), 0, regions, ref regionCount);
            for (var i = 0; i < regionCount; i++)
            {
                AddParagraphs(lines.AsSpan(regions[i].Start, regions[i].Count), sorted, text, output);
            }
        }
        finally
        {
            ArrayPool<LayoutItem>.Shared.Return(sorted);
            ArrayPool<LayoutLine>.Shared.Return(lines);
            ArrayPool<LayoutLine>.Shared.Return(moved);
            ArrayPool<LayoutRegion>.Shared.Return(regions);
            ArrayPool<char>.Shared.Return(text);
        }
    }

    /// <summary>Gets the median item height.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The height; at least a small positive number.</returns>
    private static float MedianHeight(ReadOnlySpan<LayoutItem> items)
    {
        var rented = items.Length > StackNumbers ? ArrayPool<float>.Shared.Rent(items.Length) : null;
        Span<float> heights = rented is null ? stackalloc float[StackNumbers] : rented;
        heights = heights[..items.Length];
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = items[i].Bounds.Height;
        }

        heights.Sort();
        var median = Math.Max(heights[heights.Length / Halves], float.Epsilon);
        if (rented is not null)
        {
            ArrayPool<float>.Shared.Return(rented);
        }

        return median;
    }

    /// <summary>Finds the widest empty band across a region, along one axis.</summary>
    /// <param name="region">The lines.</param>
    /// <param name="alongX">Whether to look for a vertical band (between columns) rather than a horizontal one.</param>
    /// <returns>The band, or one of zero width.</returns>
    private static LayoutGap FindGap(ReadOnlySpan<LayoutLine> region, bool alongX)
    {
        var rented = region.Length > StackNumbers / Halves ? ArrayPool<float>.Shared.Rent(region.Length * Halves) : null;
        Span<float> buffer = rented is null ? stackalloc float[StackNumbers] : rented;
        var starts = buffer[..region.Length];
        var ends = buffer.Slice(region.Length, region.Length);
        for (var i = 0; i < region.Length; i++)
        {
            var bounds = region[i].Bounds;
            starts[i] = alongX ? bounds.Left : bounds.Top;
            ends[i] = alongX ? bounds.Right : bounds.Bottom;
        }

        starts.Sort(ends);
        var best = default(LayoutGap);
        var reach = ends[0];
        for (var i = 1; i < starts.Length; i++)
        {
            var width = starts[i] - reach;
            if (width > best.Width)
            {
                best = new((reach + starts[i]) / Halves, width);
            }

            reach = Math.Max(reach, ends[i]);
        }

        if (rented is not null)
        {
            ArrayPool<float>.Shared.Return(rented);
        }

        return best;
    }

    /// <summary>Sorts lines top to bottom, then left to right.</summary>
    /// <param name="a">The first line.</param>
    /// <param name="b">The second line.</param>
    /// <returns>The order.</returns>
    private static int CompareTopDown(LayoutLine a, LayoutLine b)
    {
        var top = a.Bounds.Top.CompareTo(b.Bounds.Top);
        return top != 0 ? top : a.Bounds.Left.CompareTo(b.Bounds.Left);
    }

    /// <summary>Determines whether an item sits on a line.</summary>
    /// <param name="row">The box around the line so far.</param>
    /// <param name="item">The item.</param>
    /// <returns><see langword="true"/> when their centres are close for their heights.</returns>
    private static bool SameRow(PdfViewerRect row, in LayoutItem item) =>
        Math.Abs(item.Bounds.CenterY - row.CenterY) <= LineTolerance * Math.Min(item.Bounds.Height, row.Height);

    /// <summary>Sorts a line left to right and splits it where a gap is wide enough to be between columns.</summary>
    /// <param name="items">The array of items.</param>
    /// <param name="start">The position of the line's first item.</param>
    /// <param name="count">The line's items.</param>
    /// <param name="row">The box around the line.</param>
    /// <param name="lines">Receives the pieces.</param>
    /// <param name="lineCount">The pieces so far; advanced past those added.</param>
    private static void SplitColumns(LayoutItem[] items, int start, int count, PdfViewerRect row, LayoutLine[] lines, ref int lineCount)
    {
        items.AsSpan(start, count).Sort(static (a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        var limit = ColumnGapInLine * row.Height;
        var pieceStart = start;
        var pieceBounds = default(PdfViewerRect);
        for (var i = start; i < start + count; i++)
        {
            if (i > pieceStart && items[i].Bounds.Left - pieceBounds.Right > limit)
            {
                lines[lineCount] = new(pieceStart, i - pieceStart, pieceBounds);
                lineCount++;
                pieceStart = i;
                pieceBounds = default;
            }

            pieceBounds = pieceBounds.Union(items[i].Bounds);
        }

        lines[lineCount] = new(pieceStart, start + count - pieceStart, pieceBounds);
        lineCount++;
    }

    /// <summary>Determines whether a line starts a new paragraph.</summary>
    /// <param name="previous">The line above.</param>
    /// <param name="line">The line.</param>
    /// <returns><see langword="true"/> at a wide gap or a change of size.</returns>
    private static bool Breaks(in LayoutLine previous, in LayoutLine line)
    {
        var a = previous.Bounds.Height;
        var b = line.Bounds.Height;
        return (line.Bounds.Top - previous.Bounds.Bottom) > ParagraphGap * Math.Max(a, b) || Math.Max(a, b) > SizeChange * Math.Min(a, b);
    }

    /// <summary>Determines whether a space must go before text so it does not run into what came before.</summary>
    /// <param name="written">The text so far.</param>
    /// <param name="next">The next text.</param>
    /// <returns><see langword="true"/> when neither side has white space.</returns>
    private static bool NeedsSpace(ReadOnlySpan<char> written, ReadOnlySpan<char> next) =>
        !written.IsEmpty && !char.IsWhiteSpace(written[^1]) && !next.IsEmpty && !char.IsWhiteSpace(next[0]);

    /// <summary>Groups the items into lines and splits each line at its column gaps.</summary>
    /// <param name="items">The array holding the items; sorted in place into lines.</param>
    /// <param name="count">The number of items.</param>
    /// <param name="lines">Receives the line pieces.</param>
    /// <returns>The number of line pieces.</returns>
    private static int BuildLines(LayoutItem[] items, int count, LayoutLine[] lines)
    {
        items.AsSpan(0, count).Sort(static (a, b) => a.Bounds.CenterY.CompareTo(b.Bounds.CenterY));
        var lineCount = 0;
        var rowStart = 0;
        var rowBounds = default(PdfViewerRect);
        for (var i = 0; i < count; i++)
        {
            if (i > rowStart && !SameRow(rowBounds, items[i]))
            {
                SplitColumns(items, rowStart, i - rowStart, rowBounds, lines, ref lineCount);
                rowStart = i;
                rowBounds = default;
            }

            rowBounds = rowBounds.Union(items[i].Bounds);
        }

        SplitColumns(items, rowStart, count - rowStart, rowBounds, lines, ref lineCount);
        return lineCount;
    }

    /// <summary>Moves the lines before a gap to the front of a region, and the others after them, each side in its order.</summary>
    /// <param name="region">The lines.</param>
    /// <param name="moved">Room to move them through, as long as the region.</param>
    /// <param name="vertical">Whether the gap is a vertical band, so lines divide by their centres across.</param>
    /// <param name="gap">The gap.</param>
    /// <returns>The lines before the gap.</returns>
    private static int Partition(Span<LayoutLine> region, Span<LayoutLine> moved, bool vertical, LayoutGap gap)
    {
        var first = 0;
        foreach (var line in region)
        {
            if (Centre(line, vertical) < gap.Position)
            {
                first++;
            }
        }

        var before = 0;
        var after = first;
        foreach (var line in region)
        {
            if (Centre(line, vertical) < gap.Position)
            {
                moved[before] = line;
                before++;
            }
            else
            {
                moved[after] = line;
                after++;
            }
        }

        moved.CopyTo(region);
        return first;
    }

    /// <summary>Gets where a line's centre is across or down the page.</summary>
    /// <param name="line">The line.</param>
    /// <param name="vertical">Whether to give the horizontal position, for a cut by a vertical band.</param>
    /// <returns>The centre.</returns>
    private static float Centre(in LayoutLine line, bool vertical) => vertical ? line.Bounds.CenterX : line.Bounds.CenterY;

    /// <summary>Gets the most characters a node's text can take: all the items' text and a space before each.</summary>
    /// <param name="count">The number of items.</param>
    /// <returns>The length of the text buffer.</returns>
    private int TextLength(int count)
    {
        var length = count;
        foreach (var item in CollectionsMarshal.AsSpan(_items))
        {
            length += item.TextLength;
        }

        return length;
    }

    /// <summary>Cuts a region at its widest empty band until no band is wide enough, collecting the regions in reading order.</summary>
    /// <param name="lines">The array of lines; a region's lines are moved so each side of a cut is a run.</param>
    /// <param name="moved">Room to move lines through, as long as <paramref name="lines"/>.</param>
    /// <param name="region">The run of lines in the region.</param>
    /// <param name="depth">How deep the cut is.</param>
    /// <param name="output">Receives the regions that are not cut further.</param>
    /// <param name="outputCount">The regions so far; advanced past those added.</param>
    private void Cut(LayoutLine[] lines, LayoutLine[] moved, LayoutRegion region, int depth, LayoutRegion[] output, ref int outputCount)
    {
        if (region.Count == 0)
        {
            return;
        }

        var span = lines.AsSpan(region.Start, region.Count);
        if (region.Count == 1 || depth >= MaxCutDepth)
        {
            output[outputCount] = region;
            outputCount++;
            return;
        }

        var columns = FindGap(span, true);
        var bands = FindGap(span, false);
        var vertical = columns.Width >= MinColumnGap * _bodyHeight && columns.Width >= bands.Width;
        if (!vertical && bands.Width < MinBandGap * _bodyHeight)
        {
            output[outputCount] = region;
            outputCount++;
            return;
        }

        var firstCount = Partition(span, moved.AsSpan(region.Start, region.Count), vertical, vertical ? columns : bands);
        Cut(lines, moved, new(region.Start, firstCount), depth + 1, output, ref outputCount);
        Cut(lines, moved, new(region.Start + firstCount, region.Count - firstCount), depth + 1, output, ref outputCount);
    }

    /// <summary>Groups a region's lines into paragraphs.</summary>
    /// <param name="region">The lines; sorted in place, top to bottom.</param>
    /// <param name="items">The array of items the lines are runs of.</param>
    /// <param name="text">Room for a node's text.</param>
    /// <param name="output">Receives the paragraph and heading nodes.</param>
    private void AddParagraphs(Span<LayoutLine> region, LayoutItem[] items, char[] text, List<PdfSemanticNode> output)
    {
        if (region.IsEmpty)
        {
            return;
        }

        region.Sort(CompareTopDown);
        var start = 0;
        for (var i = 1; i < region.Length; i++)
        {
            if (!Breaks(region[i - 1], region[i]))
            {
                continue;
            }

            output.Add(MakeNode(region[start..i], items, text));
            start = i;
        }

        output.Add(MakeNode(region[start..], items, text));
    }

    /// <summary>Makes a paragraph or heading node from lines.</summary>
    /// <param name="lines">The lines, top to bottom.</param>
    /// <param name="items">The array of items the lines are runs of.</param>
    /// <param name="text">Room for the node's text.</param>
    /// <returns>The node.</returns>
    private PdfSemanticNode MakeNode(ReadOnlySpan<LayoutLine> lines, LayoutItem[] items, char[] text)
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
            AppendLine(line, items, text, ref length, indices, ref written);
            bounds = bounds.Union(line.Bounds);
            height += line.Bounds.Height;
        }

        var ratio = height / lines.Length / _bodyHeight;
        var heading = lines.Length <= MaxHeadingLines && ratio >= HeadingRatio;
        var level = ratio >= TopHeadingRatio ? 1 : SecondLevel;
        var role = heading ? PdfSemanticRole.Heading : PdfSemanticRole.Paragraph;
        var node = new PdfSemanticNode(role, _origin, _pageIndex) { Level = heading ? level : 0, Text = new(text, 0, length), Bounds = bounds };
        node.SetItems(indices);
        return node;
    }

    /// <summary>Appends a line's text and items, with spaces at word gaps and between lines.</summary>
    /// <param name="line">The line.</param>
    /// <param name="items">The array of items the line is a run of.</param>
    /// <param name="text">The node's text so far.</param>
    /// <param name="length">The characters of text written; advanced past those added.</param>
    /// <param name="indices">Receives the item indices.</param>
    /// <param name="written">The indices written; advanced past those added.</param>
    private void AppendLine(in LayoutLine line, LayoutItem[] items, char[] text, ref int length, int[] indices, ref int written)
    {
        var limit = WordGap * line.Bounds.Height;
        var previousRight = float.NaN;
        for (var i = line.Start; i < line.Start + line.Count; i++)
        {
            var item = items[i];
            var next = _text.AsSpan(item.TextStart, item.TextLength);
            var separated = float.IsNaN(previousRight) || item.Bounds.Left - previousRight > limit;
            if (separated && NeedsSpace(text.AsSpan(0, length), next))
            {
                text[length] = ' ';
                length++;
            }

            next.CopyTo(text.AsSpan(length));
            length += next.Length;
            indices[written] = item.Index;
            written++;
            previousRight = item.Bounds.Right;
        }
    }
}
