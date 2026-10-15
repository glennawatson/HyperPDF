// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds tables drawn with ruling lines. Lines that touch form a connected drawing; one with horizontal and vertical
/// lines that frame a grid is a table. A square of the grid joins its neighbour where no line separates them, which
/// gives row and column spans. Drawings with only horizontal lines are handed back as frames for the alignment search.
/// </summary>
internal static class LayoutRuledTables
{
    /// <summary>The fewest lines a table drawing has.</summary>
    internal const int MinRulings = 3;

    /// <summary>The fewest horizontal lines of a drawing that frames text without vertical lines.</summary>
    private const int MinFrameRulings = 3;

    /// <summary>The fewest rows or columns a grid has.</summary>
    private const int MinAcross = 2;

    /// <summary>The share of a grid edge a line must cover to separate the squares on its sides.</summary>
    private const float CoverShare = 0.6F;

    /// <summary>How far, in points, a line may stop short of the grid's edge and still frame it.</summary>
    private const float FrameTolerance = 3F;

    /// <summary>How far, in points, a line may stray from the point where it meets another and still touch it.</summary>
    private const float TouchTolerance = 2F;

    /// <summary>The fewest items a table holds.</summary>
    private const int MinItems = 2;

    /// <summary>Finds the tables, takes the lines of text they hold out of the page's lines and adds a line for each table.</summary>
    /// <param name="rulings">The page's straight lines.</param>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items, grouped as the lines refer to them.</param>
    /// <param name="lines">The page's lines; the tables' lines are removed and a line is added for each table.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="tables">Receives the tables.</param>
    /// <param name="frames">Receives the boxes of drawings that frame text with horizontal lines only.</param>
    internal static void Find(
        ReadOnlySpan<LayoutRuling> rulings,
        string text,
        LayoutItem[] items,
        LayoutLine[] lines,
        ref int lineCount,
        ref List<LayoutTable>? tables,
        ref List<PdfViewerRect>? frames)
    {
        var source = new TableSource(text, items, lines);
        if (rulings.Length < MinRulings)
        {
            return;
        }

        var merged = ArrayPool<LayoutRuling>.Shared.Rent(rulings.Length);
        var parent = ArrayPool<int>.Shared.Rent(rulings.Length);
        var members = ArrayPool<LayoutRuling>.Shared.Rent(rulings.Length);
        try
        {
            var count = LayoutRulingMerger.Merge(rulings, merged);
            Link(merged.AsSpan(0, count), parent);
            for (var root = 0; root < count; root++)
            {
                if (parent[root] != root)
                {
                    continue;
                }

                var size = Gather(merged.AsSpan(0, count), parent, root, members);
                if (size >= MinRulings)
                {
                    Examine(members.AsSpan(0, size), source, ref lineCount, ref tables, ref frames);
                }
            }
        }
        finally
        {
            ArrayPool<LayoutRuling>.Shared.Return(merged);
            ArrayPool<int>.Shared.Return(parent);
            ArrayPool<LayoutRuling>.Shared.Return(members);
        }
    }

    /// <summary>Joins lines that touch into groups; each group's root is its first line.</summary>
    /// <param name="merged">The lines.</param>
    /// <param name="parent">Receives, for each line, the line that leads its group.</param>
    private static void Link(ReadOnlySpan<LayoutRuling> merged, int[] parent)
    {
        for (var i = 0; i < merged.Length; i++)
        {
            parent[i] = i;
        }

        for (var i = 0; i < merged.Length; i++)
        {
            for (var j = i + 1; j < merged.Length; j++)
            {
                if (!Touches(merged[i], merged[j]))
                {
                    continue;
                }

                var a = Leader(parent, i);
                var b = Leader(parent, j);
                parent[Math.Max(a, b)] = Math.Min(a, b);
            }
        }

        for (var i = 0; i < merged.Length; i++)
        {
            parent[i] = Leader(parent, i);
        }
    }

    /// <summary>Finds the line that leads a line's group.</summary>
    /// <param name="parent">The groups.</param>
    /// <param name="index">The line.</param>
    /// <returns>The leader.</returns>
    private static int Leader(int[] parent, int index)
    {
        while (parent[index] != index)
        {
            parent[index] = parent[parent[index]];
            index = parent[index];
        }

        return index;
    }

    /// <summary>Determines whether two lines meet or cross.</summary>
    /// <param name="a">The first line.</param>
    /// <param name="b">The second line.</param>
    /// <returns><see langword="true"/> when they touch.</returns>
    private static bool Touches(in LayoutRuling a, in LayoutRuling b)
    {
        var x = LayoutGeometry.Overlap(a.Bounds.Left - TouchTolerance, a.Bounds.Right + TouchTolerance, b.Bounds.Left, b.Bounds.Right);
        var y = LayoutGeometry.Overlap(a.Bounds.Top - TouchTolerance, a.Bounds.Bottom + TouchTolerance, b.Bounds.Top, b.Bounds.Bottom);
        return x > 0 && y > 0;
    }

    /// <summary>Copies the lines of one group.</summary>
    /// <param name="merged">The lines.</param>
    /// <param name="parent">The leader of each line.</param>
    /// <param name="root">The group's leader.</param>
    /// <param name="members">Receives the group's lines.</param>
    /// <returns>The number of lines in the group.</returns>
    private static int Gather(ReadOnlySpan<LayoutRuling> merged, int[] parent, int root, LayoutRuling[] members)
    {
        var size = 0;
        for (var i = root; i < merged.Length; i++)
        {
            if (parent[i] != root)
            {
                continue;
            }

            members[size] = merged[i];
            size++;
        }

        return size;
    }

    /// <summary>Reads one drawing: a table when its lines frame a grid, a frame for the alignment search when they are horizontal only.</summary>
    /// <param name="members">The drawing's lines.</param>
    /// <param name="source">The text, items and lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="tables">Receives a table.</param>
    /// <param name="frames">Receives a frame.</param>
    private static void Examine(ReadOnlySpan<LayoutRuling> members, in TableSource source, ref int lineCount, ref List<LayoutTable>? tables, ref List<PdfViewerRect>? frames)
    {
        var xs = Edges(members, false);
        var ys = Edges(members, true);
        var box = default(PdfViewerRect);
        var horizontal = 0;
        foreach (var ruling in members)
        {
            box = box.Union(ruling.Bounds);
            horizontal += ruling.IsHorizontal ? 1 : 0;
        }

        var framed = xs.Length > MinAcross && ys.Length > MinAcross && Frames(members, xs, ys);
        if (framed && TryGrid(members, xs, ys, box, source, ref lineCount, ref tables))
        {
            return;
        }

        if (xs.Length <= 1 && horizontal >= MinFrameRulings)
        {
            (frames ??= []).Add(box);
        }
    }

    /// <summary>Lists the distinct positions of the horizontal or vertical lines, in order.</summary>
    /// <param name="members">The lines.</param>
    /// <param name="horizontal">Whether to list the horizontal lines' heights rather than the vertical lines' positions.</param>
    /// <returns>The positions.</returns>
    private static float[] Edges(ReadOnlySpan<LayoutRuling> members, bool horizontal)
    {
        var positions = new List<float>();
        foreach (var ruling in members)
        {
            if (ruling.IsHorizontal == horizontal)
            {
                positions.Add(LayoutRulingMerger.Axis(ruling));
            }
        }

        positions.Sort();
        var edges = new List<float>();
        foreach (var position in positions)
        {
            if (edges.Count == 0 || position - edges[^1] > LayoutRulingMerger.AxisTolerance)
            {
                edges.Add(position);
            }
        }

        return [.. edges];
    }

    /// <summary>Determines whether the lines reach the grid's four edges, so the grid is closed.</summary>
    /// <param name="members">The lines.</param>
    /// <param name="xs">The vertical lines' positions.</param>
    /// <param name="ys">The horizontal lines' heights.</param>
    /// <returns><see langword="true"/> when the outer edges are drawn.</returns>
    private static bool Frames(ReadOnlySpan<LayoutRuling> members, float[] xs, float[] ys)
    {
        var left = float.MaxValue;
        var right = float.MinValue;
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var ruling in members)
        {
            if (ruling.IsHorizontal)
            {
                left = Math.Min(left, ruling.Bounds.Left);
                right = Math.Max(right, ruling.Bounds.Right);
            }
            else
            {
                top = Math.Min(top, ruling.Bounds.Top);
                bottom = Math.Max(bottom, ruling.Bounds.Bottom);
            }
        }

        return Math.Abs(left - xs[0]) <= FrameTolerance && Math.Abs(right - xs[^1]) <= FrameTolerance
            && Math.Abs(top - ys[0]) <= FrameTolerance && Math.Abs(bottom - ys[^1]) <= FrameTolerance;
    }

    /// <summary>Makes a table from a framed grid, when text sits in it.</summary>
    /// <param name="members">The lines.</param>
    /// <param name="xs">The vertical lines' positions.</param>
    /// <param name="ys">The horizontal lines' heights.</param>
    /// <param name="box">The box around the lines.</param>
    /// <param name="source">The text, items and lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="tables">Receives the table.</param>
    /// <returns><see langword="true"/> when a table was made.</returns>
    private static bool TryGrid(ReadOnlySpan<LayoutRuling> members, float[] xs, float[] ys, PdfViewerRect box, in TableSource source, ref int lineCount, ref List<LayoutTable>? tables)
    {
        var (text, items, lines) = source;
        var columns = xs.Length - 1;
        var rows = ys.Length - 1;
        var placed = Place(box, xs, ys, items, lines, lineCount);
        if (placed.Count < MinItems)
        {
            return false;
        }

        var anchors = Anchors(members, xs, ys);
        var table = LayoutTableBuilder.Create(text, box, rows, columns, anchors, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(placed));
        ClaimLines(box, lines, ref lineCount);
        tables ??= [];
        lines[lineCount] = new(-1, 0, box, LayoutLineKind.Table, tables.Count);
        lineCount++;
        tables.Add(table);
        return true;
    }

    /// <summary>Puts the items of the lines inside a box into the squares of the grid.</summary>
    /// <param name="box">The box.</param>
    /// <param name="xs">The vertical lines' positions.</param>
    /// <param name="ys">The horizontal lines' heights.</param>
    /// <param name="items">The items.</param>
    /// <param name="lines">The page's lines.</param>
    /// <param name="lineCount">The number of lines.</param>
    /// <returns>The items and their squares.</returns>
    private static List<PlacedItem> Place(PdfViewerRect box, float[] xs, float[] ys, LayoutItem[] items, LayoutLine[] lines, int lineCount)
    {
        var columns = xs.Length - 1;
        var placed = new List<PlacedItem>();
        for (var i = 0; i < lineCount; i++)
        {
            var line = lines[i];
            if (line.Kind != LayoutLineKind.Text || !LayoutGeometry.Contains(box, line.Bounds.CenterX, line.Bounds.CenterY, 0))
            {
                continue;
            }

            for (var k = line.Start; k < line.Start + line.Count; k++)
            {
                var column = Slot(xs, items[k].Bounds.CenterX);
                var row = Slot(ys, items[k].Bounds.CenterY);
                placed.Add(new(items[k], (row * columns) + column));
            }
        }

        return placed;
    }

    /// <summary>Finds which gap between lines a position falls in; positions outside the grid go to the nearest gap.</summary>
    /// <param name="edges">The lines' positions.</param>
    /// <param name="position">The position.</param>
    /// <returns>The gap, from zero.</returns>
    private static int Slot(float[] edges, float position)
    {
        var slot = 0;
        for (var i = 1; i < edges.Length - 1; i++)
        {
            slot += position >= edges[i] ? 1 : 0;
        }

        return slot;
    }

    /// <summary>Removes the lines of text whose centre is inside a box.</summary>
    /// <param name="box">The box.</param>
    /// <param name="lines">The page's lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    private static void ClaimLines(PdfViewerRect box, LayoutLine[] lines, ref int lineCount)
    {
        var kept = 0;
        for (var i = 0; i < lineCount; i++)
        {
            var line = lines[i];
            if (line.Kind == LayoutLineKind.Text && LayoutGeometry.Contains(box, line.Bounds.CenterX, line.Bounds.CenterY, 0))
            {
                continue;
            }

            lines[kept] = line;
            kept++;
        }

        lineCount = kept;
    }

    /// <summary>Joins squares that no line separates into cells.</summary>
    /// <param name="members">The lines.</param>
    /// <param name="xs">The vertical lines' positions.</param>
    /// <param name="ys">The horizontal lines' heights.</param>
    /// <returns>The anchor of each square.</returns>
    private static int[] Anchors(ReadOnlySpan<LayoutRuling> members, float[] xs, float[] ys)
    {
        var columns = xs.Length - 1;
        var rows = ys.Length - 1;
        var anchors = new int[rows * columns];
        for (var i = 0; i < anchors.Length; i++)
        {
            anchors[i] = i;
        }

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var square = (row * columns) + column;
                if (column + 1 < columns && !Covered(members, false, xs[column + 1], ys[row], ys[row + 1]))
                {
                    Join(anchors, square, square + 1);
                }

                if (row + 1 < rows && !Covered(members, true, ys[row + 1], xs[column], xs[column + 1]))
                {
                    Join(anchors, square, square + columns);
                }
            }
        }

        for (var i = 0; i < anchors.Length; i++)
        {
            anchors[i] = Leader(anchors, i);
        }

        return anchors;
    }

    /// <summary>Joins the groups of two squares, keeping the earlier square as the leader.</summary>
    /// <param name="anchors">The groups.</param>
    /// <param name="a">The first square.</param>
    /// <param name="b">The second square.</param>
    private static void Join(int[] anchors, int a, int b)
    {
        var first = Leader(anchors, a);
        var second = Leader(anchors, b);
        anchors[Math.Max(first, second)] = Math.Min(first, second);
    }

    /// <summary>Determines whether lines cover most of an edge between two squares.</summary>
    /// <param name="members">The lines.</param>
    /// <param name="horizontal">Whether the edge runs across, so horizontal lines cover it.</param>
    /// <param name="axis">Where the edge is across its direction.</param>
    /// <param name="from">Where the edge starts along its direction.</param>
    /// <param name="to">Where the edge ends.</param>
    /// <returns><see langword="true"/> when a line separates the squares.</returns>
    private static bool Covered(ReadOnlySpan<LayoutRuling> members, bool horizontal, float axis, float from, float to)
    {
        var covered = 0F;
        foreach (var ruling in members)
        {
            if (ruling.IsHorizontal == horizontal && Math.Abs(LayoutRulingMerger.Axis(ruling) - axis) <= LayoutRulingMerger.AxisTolerance)
            {
                covered += LayoutGeometry.Overlap(from, to, LayoutRulingMerger.Start(ruling), LayoutRulingMerger.End(ruling));
            }
        }

        return covered >= CoverShare * (to - from);
    }
}
