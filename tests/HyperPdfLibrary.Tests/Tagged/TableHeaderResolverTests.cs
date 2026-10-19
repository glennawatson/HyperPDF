// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks table placement without expanding spans into absent rows or overflowing indices.</summary>
public sealed class TableHeaderResolverTests
{
    /// <summary>The number of columns occupied by the first cell.</summary>
    private const int WideSpan = 2;

    /// <summary>Retains large declared spans even when the table contains only one row.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task LargeSpansDoNotExpandAbsentRows()
    {
        var table = new PdfSemanticNode(PdfSemanticRole.Table, PdfNodeOrigin.Tagged, 0);
        var row = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        var first = Cell(int.MaxValue, int.MaxValue);
        var second = Cell(1, 1);
        var third = Cell(1, 1);
        row.Add(first);
        row.Add(second);
        row.Add(third);
        table.Add(row);

        TableHeaderResolver.Resolve(table);

        await Assert.That(first.RowSpan).IsEqualTo(int.MaxValue);
        await Assert.That(first.ColumnSpan).IsEqualTo(int.MaxValue);
        await Assert.That(first.Column).IsEqualTo(0);
        await Assert.That(second.Column).IsEqualTo(int.MaxValue);
        await Assert.That(third.Column).IsEqualTo(int.MaxValue);
    }

    /// <summary>A span occupying the following row pushes that row's next cell past it.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task FollowingRowsSkipOccupiedColumns()
    {
        var table = new PdfSemanticNode(PdfSemanticRole.Table, PdfNodeOrigin.Tagged, 0);
        var firstRow = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        var secondRow = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        firstRow.Add(Cell(WideSpan, WideSpan));
        var cell = Cell(1, 1);
        secondRow.Add(cell);
        table.Add(firstRow);
        table.Add(secondRow);

        TableHeaderResolver.Resolve(table);

        await Assert.That(cell.Row).IsEqualTo(1);
        await Assert.That(cell.Column).IsEqualTo(WideSpan);
    }

    /// <summary>Header coverage uses a wide sum when the column endpoint exceeds Int32.MaxValue.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task WideColumnHeadersStillLabelOverlappingCells()
    {
        var table = new PdfSemanticNode(PdfSemanticRole.Table, PdfNodeOrigin.Tagged, 0);
        var firstRow = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        var secondRow = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        var header = Cell(1, int.MaxValue, PdfSemanticRole.TableHeaderCell, PdfTableScope.Column);
        firstRow.Add(Cell(1, 1));
        firstRow.Add(header);
        var cell = Cell(1, WideSpan);
        secondRow.Add(cell);
        table.Add(firstRow);
        table.Add(secondRow);

        TableHeaderResolver.Resolve(table);

        await Assert.That(cell.Headers).Contains(header);
    }

    /// <summary>Header coverage uses a wide sum when the row endpoint exceeds Int32.MaxValue.</summary>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DeepRowHeadersStillLabelOverlappingCells()
    {
        var table = new PdfSemanticNode(PdfSemanticRole.Table, PdfNodeOrigin.Tagged, 0);
        var firstRow = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        var secondRow = new PdfSemanticNode(PdfSemanticRole.TableRow, PdfNodeOrigin.Tagged, 0);
        firstRow.Add(Cell(1, 1));
        var header = Cell(int.MaxValue, 1, PdfSemanticRole.TableHeaderCell, PdfTableScope.Row);
        var cell = Cell(1, 1);
        secondRow.Add(header);
        secondRow.Add(cell);
        table.Add(firstRow);
        table.Add(secondRow);

        TableHeaderResolver.Resolve(table);

        await Assert.That(cell.Headers).Contains(header);
    }

    /// <summary>Builds a cell carrying the supplied table spans.</summary>
    /// <param name="rows">The row span.</param>
    /// <param name="columns">The column span.</param>
    /// <returns>The cell.</returns>
    private static PdfSemanticNode Cell(int rows, int columns) => Cell(rows, columns, PdfSemanticRole.TableCell, PdfTableScope.None);

    /// <summary>Builds a cell or header carrying explicit spans and scope.</summary>
    /// <param name="rows">The row span.</param>
    /// <param name="columns">The column span.</param>
    /// <param name="role">The cell role.</param>
    /// <param name="scope">The header scope.</param>
    /// <returns>The cell.</returns>
    private static PdfSemanticNode Cell(int rows, int columns, PdfSemanticRole role, PdfTableScope scope)
    {
        var element = new PdfStructureElement(new PdfDictionary(null), default, null) { Attributes = new(string.Empty, null, scope, [], rows, columns, string.Empty, string.Empty) };
        return new(role, PdfNodeOrigin.Tagged, 0) { Element = element };
    }
}
