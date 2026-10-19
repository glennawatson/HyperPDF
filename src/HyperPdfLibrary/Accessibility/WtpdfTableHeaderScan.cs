// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks that a table's header cells say which cells they label (WTPDF 1.0, clause 8.2.5.26). A header cell with no <c>Scope</c> is assumed
/// to have the scope ISO 32000-2, Table 384 gives it by position. That assumption is wrong for a header inside the table, so such a
/// header needs a <c>Scope</c>; a <c>Headers</c> entry must name header cells of the table; and once any cell has <c>Headers</c>, every cell
/// that has headers needs it. The scan lays the cells on a grid held in pooled arrays, so a table that meets the rules allocates nothing.
/// </summary>
internal ref struct WtpdfTableHeaderScan
{
    /// <summary>The structure type of a header cell.</summary>
    private const string HeaderType = "TH";

    /// <summary>The clause of the table rule.</summary>
    private const string Clause = "8.2.5.26";

    /// <summary>The largest span the grid honours, so a damaged file cannot ask for a huge grid.</summary>
    private const int MaxSpan = 256;

    /// <summary>The most grid positions the scan lays out; a larger table is left to the regularity rule alone.</summary>
    private const int MaxGridPositions = 1 << 20;

    /// <summary>The table.</summary>
    private readonly PdfStructureElement _table;

    /// <summary>The table's rows, in order.</summary>
    private readonly ReadOnlySpan<PdfStructureElement> _rows;

    /// <summary>Receives the findings.</summary>
    private readonly List<PdfWtpdfFinding> _findings;

    /// <summary>Whether the table has a header cell, so the header rules apply.</summary>
    private readonly bool _hasHeaders;

    /// <summary>The cells, in reading order, with where each starts.</summary>
    private RentedList<TableCellPlace> _cells;

    /// <summary>For each grid position, one more than the index of the cell covering it, or zero when none does.</summary>
    private int[]? _cover;

    /// <summary>The number of columns.</summary>
    private int _width;

    /// <summary>Whether two cells cover the same grid position.</summary>
    private bool _overlaps;

    /// <summary>Initializes a new instance of the <see cref="WtpdfTableHeaderScan"/> struct.</summary>
    /// <param name="table">The table.</param>
    /// <param name="rows">The table's rows.</param>
    /// <param name="findings">Receives the findings.</param>
    private WtpdfTableHeaderScan(PdfStructureElement table, ReadOnlySpan<PdfStructureElement> rows, List<PdfWtpdfFinding> findings)
    {
        _table = table;
        _rows = rows;
        _findings = findings;
        _cells = default;
        _cover = null;
        _width = 0;
        _overlaps = false;
        _hasHeaders = HasHeaderCell(rows);
    }

    /// <summary>Checks a table's header cells.</summary>
    /// <param name="table">The Table.</param>
    /// <param name="rows">Its rows, in order.</param>
    /// <param name="findings">Receives the findings.</param>
    internal static void Check(PdfStructureElement table, ReadOnlySpan<PdfStructureElement> rows, List<PdfWtpdfFinding> findings)
    {
        var scan = new WtpdfTableHeaderScan(table, rows, findings);
        try
        {
            scan.Run();
        }
        finally
        {
            scan.Dispose();
        }
    }

    /// <summary>Determines whether any row holds a header cell.</summary>
    /// <param name="rows">The rows.</param>
    /// <returns><see langword="true"/> when a TH is present.</returns>
    private static bool HasHeaderCell(ReadOnlySpan<PdfStructureElement> rows)
    {
        foreach (var row in rows)
        {
            foreach (var kid in row.KidSpan)
            {
                if (kid.Element is { } cell && cell.MappedType == HeaderType)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Gets the scope ISO 32000-2, Table 384 gives a header by its position when it has none.</summary>
    /// <param name="row">The header's first row.</param>
    /// <param name="column">The header's first column.</param>
    /// <returns>The assumed scope.</returns>
    private static PdfTableScope DefaultScope(int row, int column)
    {
        if (row == 0 && column > 0)
        {
            return PdfTableScope.Column;
        }

        return column == 0 && row > 0 ? PdfTableScope.Row : PdfTableScope.Both;
    }

    /// <summary>Gets a header's scope, written or assumed.</summary>
    /// <param name="place">The header cell.</param>
    /// <returns>The scope.</returns>
    private static PdfTableScope EffectiveScope(in TableCellPlace place) =>
        place.Cell.Attributes.Scope != PdfTableScope.None ? place.Cell.Attributes.Scope : DefaultScope(place.Row, place.Column);

    /// <summary>Gives the pooled arrays back.</summary>
    private void Dispose()
    {
        _cells.Dispose();
        if (_cover is null)
        {
            return;
        }

        ArrayPool<int>.Shared.Return(_cover);
        _cover = null;
    }

    /// <summary>Lays out the grid and runs the rules.</summary>
    private void Run()
    {
        if (!Layout())
        {
            return;
        }

        AddWhen(_overlaps, "A table cell cannot overlap another cell: check the RowSpan and ColSpan values.");
        if (!_hasHeaders)
        {
            return;
        }

        AddWhen(HasUnscopedInterior(), "A header cell that is not in the first row or column needs a Scope, because the assumed scope (Both) does not say which cells it labels.");
        AddWhen(NamesUnknownHeader(), "A Headers entry must name the ID of a header cell (TH) in the same table.");
        AddWhen(HeadersIncomplete(), "When any cell of a table uses Headers, every cell that has headers needs a Headers entry.");
    }

    /// <summary>Places every cell on the grid.</summary>
    /// <returns><see langword="false"/> when the table is empty or too large to lay out.</returns>
    private bool Layout()
    {
        if (_rows.IsEmpty)
        {
            return false;
        }

        foreach (var kid in _rows[0].KidSpan)
        {
            _width += kid.Element is { } cell ? Math.Clamp(cell.Attributes.ColumnSpan, 1, MaxSpan) : 0;
        }

        var size = (long)_rows.Length * _width;
        if (_width == 0 || size > MaxGridPositions)
        {
            return false;
        }

        _cover = ArrayPool<int>.Shared.Rent((int)size);
        _cover.AsSpan(0, (int)size).Clear();
        for (var row = 0; row < _rows.Length; row++)
        {
            PlaceRow(row);
        }

        return true;
    }

    /// <summary>Places the cells of one row.</summary>
    /// <param name="row">The row's index.</param>
    private void PlaceRow(int row)
    {
        var column = 0;
        foreach (var kid in _rows[row].KidSpan)
        {
            if (kid.Element is not { } cell)
            {
                continue;
            }

            while (column < _width && _cover![(row * _width) + column] != 0)
            {
                column++;
            }

            if (column >= _width)
            {
                return;
            }

            _cells.Add(new(cell, row, column));
            Cover(cell, row, column);
            column += Math.Clamp(cell.Attributes.ColumnSpan, 1, MaxSpan);
        }
    }

    /// <summary>Marks every grid position the newest cell covers.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="row">Its first row.</param>
    /// <param name="column">Its first column.</param>
    private void Cover(PdfStructureElement cell, int row, int column)
    {
        var rows = Math.Min(Math.Clamp(cell.Attributes.RowSpan, 1, MaxSpan), _rows.Length - row);
        var columns = Math.Min(Math.Clamp(cell.Attributes.ColumnSpan, 1, MaxSpan), _width - column);
        for (var r = 0; r < rows; r++)
        {
            var span = _cover.AsSpan(((row + r) * _width) + column, columns);
            _overlaps |= span.IndexOfAnyExcept(0) >= 0;
            span.Fill(_cells.Count);
        }
    }

    /// <summary>Determines whether a header inside the table has no <c>Scope</c>.</summary>
    /// <returns><see langword="true"/> when one does.</returns>
    private readonly bool HasUnscopedInterior()
    {
        foreach (var place in _cells.Span)
        {
            if (place.Cell.MappedType == HeaderType && place.Cell.Attributes.Scope == PdfTableScope.None && place.Row > 0 && place.Column > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a <c>Headers</c> entry names something that is not a header cell of the table.</summary>
    /// <returns><see langword="true"/> when one does.</returns>
    private readonly bool NamesUnknownHeader()
    {
        var ids = ArrayPool<string>.Shared.Rent(_cells.Count);
        try
        {
            var count = CollectHeaderIds(ids);
            foreach (var place in _cells.Span)
            {
                foreach (var name in place.Cell.Attributes.Headers)
                {
                    if (Array.BinarySearch(ids, 0, count, name, StringComparer.Ordinal) < 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        finally
        {
            ArrayPool<string>.Shared.Return(ids, true);
        }
    }

    /// <summary>Writes the sorted ids of the table's header cells.</summary>
    /// <param name="ids">Receives the ids.</param>
    /// <returns>How many ids there are.</returns>
    private readonly int CollectHeaderIds(string[] ids)
    {
        var count = 0;
        foreach (var place in _cells.Span)
        {
            if (place.Cell.MappedType != HeaderType || place.Cell.ElementId is not { Length: > 0 } id)
            {
                continue;
            }

            ids[count] = id;
            count++;
        }

        Array.Sort(ids, 0, count, StringComparer.Ordinal);
        return count;
    }

    /// <summary>Determines whether some cell uses <c>Headers</c> while another cell that has headers does not.</summary>
    /// <returns><see langword="true"/> when one does.</returns>
    private readonly bool HeadersIncomplete()
    {
        var used = false;
        foreach (var place in _cells.Span)
        {
            used |= place.Cell.Attributes.Headers.Length > 0;
        }

        if (!used)
        {
            return false;
        }

        foreach (var place in _cells.Span)
        {
            if (place.Cell.MappedType != HeaderType && place.Cell.Attributes.Headers.Length == 0 && HasHeader(place))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a header found by ISO 32000-2's search (14.8.4.8.3) labels a cell.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns><see langword="true"/> when a header to its left labels its row, or one above labels its column.</returns>
    private readonly bool HasHeader(in TableCellPlace cell)
    {
        for (var column = cell.Column - 1; column >= 0; column--)
        {
            if (Labels(_cover![(cell.Row * _width) + column], PdfTableScope.Row))
            {
                return true;
            }
        }

        for (var row = cell.Row - 1; row >= 0; row--)
        {
            if (Labels(_cover![(row * _width) + cell.Column], PdfTableScope.Column))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a cell is a header whose scope covers a direction.</summary>
    /// <param name="covering">One more than the cell's index, or zero for an empty position.</param>
    /// <param name="wanted">Row or Column.</param>
    /// <returns><see langword="true"/> when it is a header with that scope or Both.</returns>
    private readonly bool Labels(int covering, PdfTableScope wanted)
    {
        if (covering == 0)
        {
            return false;
        }

        var place = _cells.Span[covering - 1];
        var scope = EffectiveScope(place);
        return place.Cell.MappedType == HeaderType && (scope == PdfTableScope.Both || scope == wanted);
    }

    /// <summary>Adds a finding for the table when a condition holds.</summary>
    /// <param name="condition">Whether the table breaks the rule.</param>
    /// <param name="message">The finding's text.</param>
    private readonly void AddWhen(bool condition, string message)
    {
        if (!condition)
        {
            return;
        }

        _findings.Add(new(PdfWtpdfProfile.Reuse, Clause, message, _table.Id.Number));
    }
}
