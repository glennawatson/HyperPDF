// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Works out a reading order and roles from geometry for pages without usable tags. Items form lines and lines split at
/// column gaps. Running headers, footers and page numbers are set aside as pagination artifacts. Tables found from
/// ruling lines or alignment, and images, join the lines as blocks of their own. The page is cut recursively at its widest
/// empty band (columns left to right, bands top to bottom), narrow sidebars are read after the text beside them, and each
/// region's lines group into headings, paragraphs and lists. Captions are linked to their figures and tables. Every node
/// is labelled with the origin it was given, so readers know the order is inferred. The items sit in one array in which
/// each line is a run, and each region a run of lines, so nothing but the nodes is allocated.
/// </summary>
[DebuggerDisplay("InferredLayoutBuilder: page {_pageIndex}, {_items.Count} items")]
internal sealed class InferredLayoutBuilder
{
    /// <summary>The least empty vertical band, in body text heights, that separates columns.</summary>
    private const float MinColumnGap = 1F;

    /// <summary>The least empty horizontal band, in body text heights, that separates blocks.</summary>
    private const float MinBandGap = 1F;

    /// <summary>The gap between lines, in line heights, that starts a new paragraph.</summary>
    private const float ParagraphGap = 0.6F;

    /// <summary>The change in line height that starts a new paragraph.</summary>
    private const float SizeChange = 1.25F;

    /// <summary>The deepest recursive cut.</summary>
    private const int MaxCutDepth = 32;

    /// <summary>The most numbers sorted on the stack before a rented array takes over.</summary>
    private const int StackNumbers = 256;

    /// <summary>How far, in body text heights, a continuation line may start left of the text of a list item's first line.</summary>
    private const float HangTolerance = 0.5F;

    /// <summary>The share of the page an image must cover, under text, to be taken for a scan of the page.</summary>
    private const float PageScanShare = 0.8F;

    /// <summary>The fewest items in a list.</summary>
    private const int MinListItems = 2;

    /// <summary>Where the nodes come from.</summary>
    private readonly PdfNodeOrigin _origin;

    /// <summary>The zero based page index.</summary>
    private readonly int _pageIndex;

    /// <summary>The items' text, back to back.</summary>
    private readonly string _text;

    /// <summary>The items.</summary>
    private readonly List<LayoutItem> _items;

    /// <summary>What the page draws, or <see langword="null"/> for a page read from text recognition.</summary>
    private readonly PdfMarkedContentPage? _content;

    /// <summary>What inference may use beyond the page's text.</summary>
    private readonly PdfLayoutInference _options;

    /// <summary>The median item height: the body text size.</summary>
    private float _bodyHeight;

    /// <summary>The tables found; made when the page may have one.</summary>
    private List<LayoutTable>? _tables;

    /// <summary>The images that are figures; made when the page has one.</summary>
    private List<PdfMarkedImage>? _images;

    /// <summary>Initializes a new instance of the <see cref="InferredLayoutBuilder"/> class.</summary>
    /// <param name="origin">Where the nodes come from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="text">The items' text, back to back.</param>
    /// <param name="items">The items.</param>
    internal InferredLayoutBuilder(PdfNodeOrigin origin, int pageIndex, string text, List<LayoutItem> items)
        : this(origin, pageIndex, text, items, null, PdfLayoutInference.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="InferredLayoutBuilder"/> class.</summary>
    /// <param name="origin">Where the nodes come from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="text">The items' text, back to back.</param>
    /// <param name="items">The items.</param>
    /// <param name="content">What the page draws, or <see langword="null"/>.</param>
    /// <param name="options">What inference may use beyond the page's text.</param>
    internal InferredLayoutBuilder(PdfNodeOrigin origin, int pageIndex, string text, List<LayoutItem> items, PdfMarkedContentPage? content, PdfLayoutInference options)
    {
        _origin = origin;
        _pageIndex = pageIndex;
        _text = text;
        _items = items;
        _content = content;
        _options = options;
    }

    /// <summary>Builds the nodes in reading order.</summary>
    /// <param name="output">Receives the nodes.</param>
    /// <param name="artifacts">Receives the pagination artifacts.</param>
    /// <param name="cancellationToken">Stops the work between regions.</param>
    internal void Build(List<PdfSemanticNode> output, List<PdfSemanticNode> artifacts, CancellationToken cancellationToken)
    {
        var count = _items.Count;
        CollectImages(count);
        var imageCount = _images?.Count ?? 0;
        if (count == 0 && imageCount == 0)
        {
            return;
        }

        var capacity = count + imageCount + 1;
        var sorted = ArrayPool<LayoutItem>.Shared.Rent(capacity);
        var lines = ArrayPool<LayoutLine>.Shared.Rent(capacity);
        var moved = ArrayPool<LayoutLine>.Shared.Rent(capacity);
        var regions = ArrayPool<LayoutRegion>.Shared.Rent(capacity);
        var text = ArrayPool<char>.Shared.Rent(TextLength(count) + 1);
        try
        {
            _items.CopyTo(sorted);
            _bodyHeight = count > 0 ? MedianHeight(sorted.AsSpan(0, count)) : 1F;
            var lineCount = LayoutLines.Build(sorted, count, lines);
            var nodes = new LayoutNodes(_origin, _pageIndex, _text, _bodyHeight, _options.GuessLanguage);
            SetAsidePagination(nodes, sorted, lines, ref lineCount, text, artifacts);
            FindTables(sorted, lines, ref lineCount);
            AddImages(lines, ref lineCount);
            var regionCount = 0;
            Cut(lines, moved, new(0, lineCount), 0, regions, ref regionCount);
            LayoutSidebars.Order(regions, regionCount, lines);
            for (var i = 0; i < regionCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddRegion(nodes, lines.AsSpan(regions[i].Start, regions[i].Count), regions[i].IsSidebar, sorted, text, output);
            }

            LayoutCaptions.Link(output, _bodyHeight);
            DescribeFigures(output);
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
        var median = Math.Max(heights[heights.Length / LayoutGeometry.Halves], float.Epsilon);
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
        var rented = region.Length > StackNumbers / LayoutGeometry.Halves ? ArrayPool<float>.Shared.Rent(region.Length * LayoutGeometry.Halves) : null;
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
                best = new((reach + starts[i]) / LayoutGeometry.Halves, width);
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

    /// <summary>Gets the box around some lines.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The box.</returns>
    private static PdfViewerRect BoundsOf(ReadOnlySpan<LayoutLine> lines)
    {
        var bounds = default(PdfViewerRect);
        foreach (var line in lines)
        {
            bounds = bounds.Union(line.Bounds);
        }

        return bounds;
    }

    /// <summary>Makes lists and paragraphs from segments that start with labels: the longest runs that make a list become lists.</summary>
    /// <param name="context">What the nodes are made from.</param>
    /// <param name="run">The lines.</param>
    /// <param name="labels">Each line's label.</param>
    /// <param name="starts">Where each segment starts.</param>
    /// <param name="first">The first segment.</param>
    /// <param name="count">The segments, all starting with a label.</param>
    /// <param name="output">Receives the nodes.</param>
    private static void FlushItems(in TextContext context, ReadOnlySpan<LayoutLine> run, ListLabel[] labels, int[] starts, int first, int count, List<PdfSemanticNode> output)
    {
        if (count == 0)
        {
            return;
        }

        var spans = ArrayPool<ListItemSpan>.Shared.Rent(count);
        var found = ArrayPool<ListLabel>.Shared.Rent(count);
        try
        {
            for (var k = 0; k < count; k++)
            {
                found[k] = labels[starts[first + k]];
                spans[k] = new(starts[first + k], starts[first + k + 1], found[k]);
            }

            var at = 0;
            while (at < count)
            {
                var length = LongestList(found, at, count, out var numbering);
                if (length >= MinListItems)
                {
                    output.Add(LayoutListNodes.Make(context.Nodes, run, spans.AsSpan(at, length), numbering, context.Items, context.Buffer));
                    at += length;
                    continue;
                }

                output.Add(context.Nodes.MakeText(run[spans[at].Start..spans[at].End], context.Items, context.Buffer, context.Column));
                at++;
            }
        }
        finally
        {
            ArrayPool<ListItemSpan>.Shared.Return(spans);
            ArrayPool<ListLabel>.Shared.Return(found);
        }
    }

    /// <summary>Finds the most labels from a position that make a list.</summary>
    /// <param name="labels">The labels.</param>
    /// <param name="at">The position to start from.</param>
    /// <param name="count">The number of labels.</param>
    /// <param name="numbering">Receives how the list's labels are made.</param>
    /// <returns>The number of labels in the list; one when not even two make a list.</returns>
    private static int LongestList(ListLabel[] labels, int at, int count, out PdfListNumbering numbering)
    {
        numbering = PdfListNumbering.None;
        var low = 1;
        var high = count - at;
        while (low < high)
        {
            var middle = (low + high + 1) / LayoutGeometry.Halves;
            if (LayoutListLabels.Resolve(labels.AsSpan(at, middle)) is not null)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (low >= MinListItems && LayoutListLabels.Resolve(labels.AsSpan(at, low)) is { } resolved)
        {
            numbering = resolved;
        }

        return low;
    }

    /// <summary>Gets the most characters a node's text can take: all the items' text and a space before each.</summary>
    /// <param name="count">The number of items.</param>
    /// <returns>The length of the text buffer.</returns>
    private int TextLength(int count) => count == 0 ? 0 : LayoutText.MaxLength(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_items));

    /// <summary>Lists the images that are figures: those not marked as artifacts, and not a scan of the whole page under text.</summary>
    /// <param name="textItems">The number of text items on the page.</param>
    private void CollectImages(int textItems)
    {
        if (_content is null || _content.Images.IsEmpty)
        {
            return;
        }

        var page = _content.Width * _content.Height;
        foreach (var image in _content.Images)
        {
            var isScan = textItems > 0 && page > 0 && image.Bounds.Width * image.Bounds.Height >= PageScanShare * page;
            if (!image.IsArtifact && !image.Bounds.IsEmpty && !isScan)
            {
                (_images ??= []).Add(image);
            }
        }
    }

    /// <summary>Takes running headers, footers and page numbers out of the lines and makes artifact nodes of them.</summary>
    /// <param name="nodes">Makes the nodes.</param>
    /// <param name="items">The array of items.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="buffer">Room for a node's text.</param>
    /// <param name="artifacts">Receives the artifact nodes.</param>
    private void SetAsidePagination(LayoutNodes nodes, LayoutItem[] items, LayoutLine[] lines, ref int lineCount, char[] buffer, List<PdfSemanticNode> artifacts)
    {
        if (_content is null || _content.Height <= 0 || !LayoutPagination.HasMarginLine(lines, lineCount, _content.Height))
        {
            return;
        }

        var found = new List<PaginationLine>();
        LayoutPagination.Extract(_text, items, lines, ref lineCount, _content.Height, _options.Pagination, found);
        foreach (var line in found)
        {
            artifacts.Add(nodes.MakeArtifact(line, items, buffer));
        }
    }

    /// <summary>Finds tables, and replaces the lines they hold with a line that stands for the table.</summary>
    /// <param name="items">The array of items.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    private void FindTables(LayoutItem[] items, LayoutLine[] lines, ref int lineCount)
    {
        var rulings = _content is null ? default : _content.Rulings;
        if (rulings.Length < LayoutRuledTables.MinRulings && !LayoutAlignedTables.MayHaveTable(lines, lineCount))
        {
            return;
        }

        List<PdfViewerRect>? frames = null;
        LayoutRuledTables.Find(rulings, _text, items, lines, ref lineCount, ref _tables, ref frames);
        LayoutAlignedTables.Find(_text, items, lines, ref lineCount, _bodyHeight, frames, ref _tables);
    }

    /// <summary>Adds a line for each figure, so the page is cut around it like text.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    private void AddImages(LayoutLine[] lines, ref int lineCount)
    {
        if (_images is not { } images)
        {
            return;
        }

        for (var i = 0; i < images.Count; i++)
        {
            lines[lineCount] = new(-1, 0, images[i].Bounds, LayoutLineKind.Image, i);
            lineCount++;
        }
    }

    /// <summary>Gives each figure the description the host provides.</summary>
    /// <param name="output">The page's nodes.</param>
    private void DescribeFigures(List<PdfSemanticNode> output)
    {
        if (_options.FigureAltText is not { } provider)
        {
            return;
        }

        foreach (var node in output)
        {
            if (node.Role != PdfSemanticRole.Figure)
            {
                continue;
            }

            var caption = node.Children.Count > 0 ? node.Children[0].Text : null;
            node.InferredAlternateText = provider(_pageIndex, node.Bounds, caption);
        }
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

    /// <summary>Makes the nodes of a region: its tables and figures in place, and its text as paragraphs, headings and lists.</summary>
    /// <param name="nodes">Makes the nodes.</param>
    /// <param name="region">The region's lines; sorted in place, top to bottom.</param>
    /// <param name="isSidebar">Whether the region is a sidebar, which gets a node to group it.</param>
    /// <param name="items">The array of items the lines are runs of.</param>
    /// <param name="buffer">Room for a node's text.</param>
    /// <param name="output">Receives the nodes.</param>
    private void AddRegion(LayoutNodes nodes, Span<LayoutLine> region, bool isSidebar, LayoutItem[] items, char[] buffer, List<PdfSemanticNode> output)
    {
        if (region.IsEmpty)
        {
            return;
        }

        region.Sort(CompareTopDown);
        var context = new TextContext(nodes, BoundsOf(region), items, buffer);
        var target = isSidebar ? new List<PdfSemanticNode>() : output;
        var i = 0;
        while (i < region.Length)
        {
            var kind = region[i].Kind;
            var end = i + 1;
            while (kind == LayoutLineKind.Text && end < region.Length && region[end].Kind == LayoutLineKind.Text)
            {
                end++;
            }

            switch (kind)
            {
                case LayoutLineKind.Table:
                {
                    target.Add(LayoutTableNodes.Make(nodes, _text, _tables![region[i].Slot], buffer));
                    break;
                }

                case LayoutLineKind.Image:
                {
                    target.Add(nodes.MakeFigure(_images![region[i].Slot]));
                    break;
                }

                default:
                {
                    AddText(context, region[i..end], target);
                    break;
                }
            }

            i = end;
        }

        if (!isSidebar)
        {
            return;
        }

        var group = nodes.MakeGroup(PdfSemanticRole.Group);
        foreach (var child in target)
        {
            LayoutNodes.Adopt(group, child);
        }

        output.Add(group);
    }

    /// <summary>Makes paragraphs, headings and lists from a run of lines of text.</summary>
    /// <param name="context">What the nodes are made from.</param>
    /// <param name="run">The lines, top to bottom.</param>
    /// <param name="output">Receives the nodes.</param>
    private void AddText(in TextContext context, ReadOnlySpan<LayoutLine> run, List<PdfSemanticNode> output)
    {
        var labels = ArrayPool<ListLabel>.Shared.Rent(run.Length);
        var starts = ArrayPool<int>.Shared.Rent(run.Length + 1);
        try
        {
            var segments = Segment(run, context.Items, labels, starts);
            var pending = 0;
            for (var s = 0; s < segments; s++)
            {
                if (labels[starts[s]].Exists)
                {
                    pending++;
                    continue;
                }

                FlushItems(context, run, labels, starts, s - pending, pending, output);
                pending = 0;
                output.Add(context.Nodes.MakeText(run[starts[s]..starts[s + 1]], context.Items, context.Buffer, context.Column));
            }

            FlushItems(context, run, labels, starts, segments - pending, pending, output);
        }
        finally
        {
            ArrayPool<ListLabel>.Shared.Return(labels);
            ArrayPool<int>.Shared.Return(starts);
        }
    }

    /// <summary>Splits a run of lines into paragraphs, and finds the label that starts each line.</summary>
    /// <param name="run">The lines.</param>
    /// <param name="items">The array of items.</param>
    /// <param name="labels">Receives each line's label.</param>
    /// <param name="starts">Receives where each segment starts, and at the end the line count.</param>
    /// <returns>The number of segments.</returns>
    private int Segment(ReadOnlySpan<LayoutLine> run, LayoutItem[] items, ListLabel[] labels, int[] starts)
    {
        var heading = LayoutNodes.HeadingRatio * _bodyHeight;
        var segments = 1;
        starts[0] = 0;
        labels[0] = run[0].Bounds.Height >= heading ? default : LayoutListLabels.Measure(_text, items, run[0]);
        var current = 0;
        for (var i = 1; i < run.Length; i++)
        {
            labels[i] = run[i].Bounds.Height >= heading ? default : LayoutListLabels.Measure(_text, items, run[i]);
            var continues = !labels[i].Exists && !Breaks(run[i - 1], run[i]);
            continues &= !labels[starts[current]].Exists || run[i].Bounds.Left >= labels[starts[current]].BodyLeft - (HangTolerance * _bodyHeight);
            if (continues)
            {
                continue;
            }

            starts[segments] = i;
            current = segments;
            segments++;
        }

        starts[segments] = run.Length;
        return segments;
    }
}
