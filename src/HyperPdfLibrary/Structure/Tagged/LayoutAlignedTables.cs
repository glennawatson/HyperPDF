// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds tables from how text lines up. Pieces of text that sit on one line and are far enough apart to be cells make a
/// row; rows that follow each other with the same columns make a table. The columns are the bands of the page that the
/// pieces cover, found from the narrowest pieces first so a heading that spans two columns does not join them.
/// Running text in columns is told from a table by how much of its column each piece fills.
/// </summary>
internal static class LayoutAlignedTables
{
    /// <summary>The fewest rows with more than one cell that make a table.</summary>
    private const int MinRows = 3;

    /// <summary>The fewest rows with more than one cell for a table inside ruling lines.</summary>
    private const int MinFramedRows = 2;

    /// <summary>The fewest columns for a table where no column holds numbers.</summary>
    private const int MinPlainColumns = 3;

    /// <summary>The fewest columns of any table.</summary>
    private const int MinColumns = 2;

    /// <summary>The most a piece may fill of the distance to the next column, on average, in a table.</summary>
    private const float MaxFill = 0.7F;

    /// <summary>The largest gap between rows, in body text heights, inside a table.</summary>
    private const float MaxRowGap = 1.5F;

    /// <summary>The share of a column's pieces that must be numbers for the column to be numeric.</summary>
    private const float NumericShare = 0.6F;

    /// <summary>The margin, in points, around a frame within which a line is inside it.</summary>
    private const float FrameMargin = 2F;

    /// <summary>Finds the tables, takes the pieces they hold out of the page's lines and adds a line for each table.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items, grouped as the lines refer to them.</param>
    /// <param name="lines">The page's lines in row order; the tables' pieces are removed and a line is added for each table.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="bodyHeight">The body text height.</param>
    /// <param name="frames">The boxes of horizontal-ruled drawings, where fewer rows and columns are enough.</param>
    /// <param name="tables">Receives the tables.</param>
    internal static void Find(string text, LayoutItem[] items, LayoutLine[] lines, ref int lineCount, float bodyHeight, List<PdfViewerRect>? frames, ref List<LayoutTable>? tables)
    {
        var source = new TableSource(text, items, lines);
        var rowStarts = ArrayPool<int>.Shared.Rent(lineCount + 1);
        try
        {
            var rows = RowStarts(lines, lineCount, rowStarts);
            var first = 0;
            while (first < rows)
            {
                if (Cells(lines, rowStarts, first) < MinColumns)
                {
                    first++;
                    continue;
                }

                var last = ExtendRun(lines, rowStarts, rows, first, bodyHeight);
                if (TryRun(source, ref lineCount, rowStarts, new(first, last), frames, ref tables))
                {
                    // The lines moved up, so the rows after the run are found again.
                    rows = RowStarts(lines, lineCount, rowStarts);
                    first = Math.Min(first, rows);
                    continue;
                }

                first = last + 1;
            }
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rowStarts);
        }
    }

    /// <summary>Determines whether enough rows of the page have more than one piece of text for a table, so other pages skip the search.</summary>
    /// <param name="lines">The lines in row order.</param>
    /// <param name="lineCount">The number of lines.</param>
    /// <returns><see langword="true"/> when a table may be found.</returns>
    internal static bool MayHaveTable(LayoutLine[] lines, int lineCount)
    {
        var multiRows = 0;
        var row = default(PdfViewerRect);
        var pieces = 0;
        for (var i = 0; i < lineCount; i++)
        {
            if (i == 0 || lines[i].Kind != LayoutLineKind.Text || !LayoutGeometry.SameRow(row, lines[i].Bounds))
            {
                row = default;
                pieces = 0;
            }

            row = row.Union(lines[i].Bounds);
            pieces++;
            multiRows += pieces == MinColumns ? 1 : 0;
        }

        return multiRows >= MinFramedRows;
    }

    /// <summary>Finds where each row of pieces starts in the lines.</summary>
    /// <param name="lines">The lines in row order.</param>
    /// <param name="lineCount">The number of lines.</param>
    /// <param name="rowStarts">Receives each row's first line, then the line count.</param>
    /// <returns>The number of rows.</returns>
    private static int RowStarts(LayoutLine[] lines, int lineCount, int[] rowStarts)
    {
        var rows = 0;
        var row = default(PdfViewerRect);
        for (var i = 0; i < lineCount; i++)
        {
            if (i == 0 || lines[i].Kind != LayoutLineKind.Text || !LayoutGeometry.SameRow(row, lines[i].Bounds))
            {
                rowStarts[rows] = i;
                rows++;
                row = default;
            }

            row = row.Union(lines[i].Bounds);
        }

        rowStarts[rows] = lineCount;
        return rows;
    }

    /// <summary>Finds the last row of a run of rows that sit close together, starting at a row with more than one cell.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="rowStarts">Each row's first line.</param>
    /// <param name="rows">The number of rows.</param>
    /// <param name="first">The run's first row.</param>
    /// <param name="bodyHeight">The body text height.</param>
    /// <returns>The last row of the run, which has more than one cell.</returns>
    private static int ExtendRun(LayoutLine[] lines, int[] rowStarts, int rows, int first, float bodyHeight)
    {
        var last = first;
        var previousBottom = RowBounds(lines, rowStarts, first).Bottom;
        for (var row = first + 1; row < rows; row++)
        {
            var bounds = RowBounds(lines, rowStarts, row);
            if (bounds.Top - previousBottom > MaxRowGap * bodyHeight)
            {
                break;
            }

            previousBottom = bounds.Bottom;
            if (Cells(lines, rowStarts, row) > 1)
            {
                last = row;
            }
        }

        return last;
    }

    /// <summary>Gets the box around a row.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="rowStarts">Each row's first line.</param>
    /// <param name="row">The row.</param>
    /// <returns>The box.</returns>
    private static PdfViewerRect RowBounds(LayoutLine[] lines, int[] rowStarts, int row)
    {
        var bounds = default(PdfViewerRect);
        for (var i = rowStarts[row]; i < rowStarts[row + 1]; i++)
        {
            bounds = bounds.Union(lines[i].Bounds);
        }

        return bounds;
    }

    /// <summary>Counts the pieces of text in a row.</summary>
    /// <param name="lines">The lines.</param>
    /// <param name="rowStarts">Each row's first line.</param>
    /// <param name="row">The row.</param>
    /// <returns>The number of pieces; zero for a row that is a table or an image.</returns>
    private static int Cells(LayoutLine[] lines, int[] rowStarts, int row)
    {
        var count = 0;
        for (var i = rowStarts[row]; i < rowStarts[row + 1]; i++)
        {
            count += lines[i].Kind == LayoutLineKind.Text ? 1 : 0;
        }

        return count;
    }

    /// <summary>Makes a table of a run of rows when it looks like one.</summary>
    /// <param name="source">The text, items and lines.</param>
    /// <param name="lineCount">The number of lines; updated when a table is made.</param>
    /// <param name="rowStarts">Each row's first line.</param>
    /// <param name="run">The run's first and last rows.</param>
    /// <param name="frames">The ruled frames.</param>
    /// <param name="tables">Receives the table.</param>
    /// <returns><see langword="true"/> when a table was made.</returns>
    private static bool TryRun(in TableSource source, ref int lineCount, int[] rowStarts, RowRun run, List<PdfViewerRect>? frames, ref List<LayoutTable>? tables)
    {
        var (first, last) = run;
        var lines = source.Lines;
        if (last <= first)
        {
            return false;
        }

        var span = lines.AsSpan(rowStarts[first], rowStarts[last + 1] - rowStarts[first]);
        if (!AllText(span))
        {
            return false;
        }

        var multiRows = 0;
        for (var row = first; row <= last; row++)
        {
            multiRows += Cells(lines, rowStarts, row) > 1 ? 1 : 0;
        }

        var box = Bounds(span);
        var framed = InFrame(frames, box);
        if (multiRows < (framed ? MinFramedRows : MinRows))
        {
            return false;
        }

        var rented = ArrayPool<ColumnBand>.Shared.Rent(span.Length);
        try
        {
            var columns = LayoutColumnBands.Find(span, rented);
            var bands = rented.AsSpan(0, columns);
            if (columns < MinColumns || !LooksLikeTable(source, span, bands, framed))
            {
                return false;
            }

            Build(source, ref lineCount, rowStarts, run, bands, box, ref tables);
            return true;
        }
        finally
        {
            ArrayPool<ColumnBand>.Shared.Return(rented);
        }
    }

    /// <summary>Determines whether every line is a line of text.</summary>
    /// <param name="span">The lines.</param>
    /// <returns><see langword="true"/> when none is a table or an image.</returns>
    private static bool AllText(ReadOnlySpan<LayoutLine> span)
    {
        foreach (var line in span)
        {
            if (line.Kind != LayoutLineKind.Text)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Determines whether the centre of a box is inside one of the frames.</summary>
    /// <param name="frames">The frames.</param>
    /// <param name="box">The box.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool InFrame(List<PdfViewerRect>? frames, PdfViewerRect box)
    {
        if (frames is null)
        {
            return false;
        }

        foreach (var frame in frames)
        {
            if (LayoutGeometry.Contains(frame, box.CenterX, box.CenterY, FrameMargin))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Gets the box around the text lines of some lines.</summary>
    /// <param name="span">The lines.</param>
    /// <returns>The box.</returns>
    private static PdfViewerRect Bounds(ReadOnlySpan<LayoutLine> span)
    {
        var bounds = default(PdfViewerRect);
        foreach (var line in span)
        {
            bounds = bounds.Union(line.Bounds);
        }

        return bounds;
    }

    /// <summary>
    /// Decides whether columns of pieces are a table and not running text: a table of several columns, or of two when one
    /// holds numbers, whose pieces leave room before the next column.
    /// </summary>
    /// <param name="source">The text, items and lines.</param>
    /// <param name="span">The run's lines.</param>
    /// <param name="bands">The columns.</param>
    /// <param name="framed">Whether ruling lines frame the run.</param>
    /// <returns><see langword="true"/> for a table.</returns>
    private static bool LooksLikeTable(in TableSource source, ReadOnlySpan<LayoutLine> span, ReadOnlySpan<ColumnBand> bands, bool framed)
    {
        var numericColumns = 0;
        var fill = 0F;
        var measured = 0;
        for (var b = 0; b < bands.Length; b++)
        {
            var column = Measure(source, span, bands, b);
            fill += column.Fill;
            measured += column.Measured;
            numericColumns += column.Pieces > 0 && column.Numbers >= NumericShare * column.Pieces ? 1 : 0;
        }

        var plain = bands.Length >= MinPlainColumns || numericColumns > 0 || framed;
        return plain && (measured == 0 || fill / measured <= MaxFill);
    }

    /// <summary>Counts the pieces that start in a column, how many are numbers, and how much of the way to the next column they reach.</summary>
    /// <param name="source">The text, items and lines.</param>
    /// <param name="span">The run's lines.</param>
    /// <param name="bands">The columns.</param>
    /// <param name="band">The column.</param>
    /// <returns>What was counted.</returns>
    private static ColumnStats Measure(in TableSource source, ReadOnlySpan<LayoutLine> span, ReadOnlySpan<ColumnBand> bands, int band)
    {
        var pieces = 0;
        var numbers = 0;
        var fill = 0F;
        var measured = 0;
        var hasNext = band + 1 < bands.Length;
        foreach (var line in span)
        {
            if (line.Kind != LayoutLineKind.Text || LayoutColumnBands.First(bands, line.Bounds) != band)
            {
                continue;
            }

            pieces++;
            numbers += LayoutGeometry.IsNumeric(source.Text, source.Items.AsSpan(line.Start, line.Count)) ? 1 : 0;
            fill += hasNext ? line.Bounds.Width / (bands[band + 1].Left - bands[band].Left) : 0F;
            measured += hasNext ? 1 : 0;
        }

        return new(pieces, numbers, fill, measured);
    }

    /// <summary>Makes the table of a run, takes its pieces out of the lines and adds the table's line.</summary>
    /// <param name="source">The text, items and lines.</param>
    /// <param name="lineCount">The number of lines; updated.</param>
    /// <param name="rowStarts">Each row's first line.</param>
    /// <param name="run">The run's first and last rows.</param>
    /// <param name="bands">The columns.</param>
    /// <param name="box">The box around the run.</param>
    /// <param name="tables">Receives the table.</param>
    private static void Build(in TableSource source, ref int lineCount, int[] rowStarts, RowRun run, ReadOnlySpan<ColumnBand> bands, PdfViewerRect box, ref List<LayoutTable>? tables)
    {
        var (first, last) = run;
        var (text, items, lines) = source;
        var rowCount = last - first + 1;
        var columns = bands.Length;
        var anchors = new int[rowCount * columns];
        for (var i = 0; i < anchors.Length; i++)
        {
            anchors[i] = i;
        }

        var placed = new List<PlacedItem>();
        for (var row = first; row <= last; row++)
        {
            for (var i = rowStarts[row]; i < rowStarts[row + 1]; i++)
            {
                if (lines[i].Kind != LayoutLineKind.Text)
                {
                    continue;
                }

                PlacePiece(lines[i], items, bands, (row - first) * columns, anchors, placed);
            }
        }

        var table = LayoutTableBuilder.Create(text, box, rowCount, columns, anchors, CollectionsMarshal.AsSpan(placed));
        var start = rowStarts[first];
        var end = rowStarts[last + 1];
        Array.Copy(lines, end, lines, start, lineCount - end);
        lineCount -= end - start;
        tables ??= [];
        lines[lineCount] = new(-1, 0, box, LayoutLineKind.Table, tables.Count);
        lineCount++;
        tables.Add(table);
    }

    /// <summary>Puts a piece of text into the squares of the columns it covers, as one cell.</summary>
    /// <param name="piece">The piece.</param>
    /// <param name="items">The items.</param>
    /// <param name="bands">The columns.</param>
    /// <param name="rowOffset">The position of the row's first square.</param>
    /// <param name="anchors">The anchor of each square.</param>
    /// <param name="placed">Receives the piece's items.</param>
    private static void PlacePiece(in LayoutLine piece, LayoutItem[] items, ReadOnlySpan<ColumnBand> bands, int rowOffset, int[] anchors, List<PlacedItem> placed)
    {
        var from = LayoutColumnBands.First(bands, piece.Bounds);
        var to = LayoutColumnBands.Last(bands, piece.Bounds);
        var anchor = anchors[rowOffset + from];
        for (var column = from; column <= to; column++)
        {
            anchors[rowOffset + column] = anchor;
        }

        for (var i = piece.Start; i < piece.Start + piece.Count; i++)
        {
            placed.Add(new(items[i], rowOffset + from));
        }
    }

    /// <summary>What a column holds, for telling a table from running text.</summary>
    /// <param name="Pieces">The pieces that start in the column.</param>
    /// <param name="Numbers">How many of them are numbers.</param>
    /// <param name="Fill">The sum of how much of the way to the next column each piece reaches.</param>
    /// <param name="Measured">How many pieces <paramref name="Fill"/> sums.</param>
    private readonly record struct ColumnStats(int Pieces, int Numbers, float Fill, int Measured);
}
