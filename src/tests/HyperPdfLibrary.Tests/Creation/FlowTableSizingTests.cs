// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests for <see cref="PdfFlowTable.FillAvailableWidth"/>, <see cref="PdfFlowTable.ColumnSizing"/>, cell font sizes, and <see cref="PdfTableRow.Height"/>.</summary>
public sealed class FlowTableSizingTests
{
    /// <summary>The default test page width in points.</summary>
    private const float PageWidth = 300F;

    /// <summary>A wider test page width in points.</summary>
    private const float WidePageWidth = 440F;

    /// <summary>The default test page height in points.</summary>
    private const float PageHeight = 400F;

    /// <summary>A shorter page height used to test multi-page pagination with cell font sizes.</summary>
    private const float ShortPageHeight = 160F;

    /// <summary>The default page margin in points.</summary>
    private const float Margin = 20F;

    /// <summary>A wider page margin in points.</summary>
    private const float WideMargin = 40F;

    /// <summary>The default body font size in points.</summary>
    private const float BodySize = 11F;

    /// <summary>A small font size in points for compact rows.</summary>
    private const float SmallFontSize = 6F;

    /// <summary>A large font size in points for tall cells.</summary>
    private const float LargeFontSize = 20F;

    /// <summary>An explicit row height in points.</summary>
    private const float ExplicitRowHeight = 48F;

    /// <summary>The border width in points so borders cover whole pixels.</summary>
    private const float BorderWidth = 2F;

    /// <summary>The relative weight of the first column (20%).</summary>
    private const float TwentyPercent = 20F;

    /// <summary>The relative weight of the second column (80%).</summary>
    private const float EightyPercent = 80F;

    /// <summary>The fixed width of the first column in mixed sizing.</summary>
    private const float FixedColumnWidth = 60F;

    /// <summary>The proportional weight of the third column in mixed sizing.</summary>
    private const float ThreeWeight = 3F;

    /// <summary>A row of pixels inside the first table row, between its top border and its text.</summary>
    private const int GapRow = 23;

    /// <summary>A row of pixels inside the first table row when using <see cref="WideMargin"/>.</summary>
    private const int WideGapRow = 43;

    /// <summary>The pixel column of the left margin.</summary>
    private const int TableLeft = 20;

    /// <summary>The expected divider column for a 20%/80% split across 260 pt content width (20 + 52 = 72).</summary>
    private const int ExpectedTwentyPercentDivider = 72;

    /// <summary>The expected divider column after a 60 pt fixed first column (20 + 60 = 80).</summary>
    private const int ExpectedFixedDivider = 80;

    /// <summary>The number of columns in a two-column table.</summary>
    private const int TwoColumns = 2;

    /// <summary>The number of columns in a three-column table.</summary>
    private const int ThreeColumns = 3;

    /// <summary>The number of rows in the multi-page compact table.</summary>
    private const int CompactTableRows = 12;

    /// <summary>The threshold below which a channel counts as dark border ink.</summary>
    private const int Dark = 128;

    /// <summary>The pixel tolerance when comparing border positions.</summary>
    private const int EdgeTolerance = 3;

    /// <summary><see cref="PdfFlowTable.FillAvailableWidth"/> expands short auto-sized columns to fill the available content width across different page widths and margins.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FillAvailableWidthExpandsShortColumnsToContentBounds()
    {
        var narrowTable = new PdfFlowTable([PdfTableRow.FromText("A", "B"), PdfTableRow.FromText("1", "2")]) { FillAvailableWidth = true, Border = PdfStroke.Black(BorderWidth) };
        var wideTable = new PdfFlowTable([PdfTableRow.FromText("A", "B"), PdfTableRow.FromText("1", "2")]) { FillAvailableWidth = true, Border = PdfStroke.Black(BorderWidth) };

        var narrowEdges = FindEdges(RenderTable(narrowTable, PageWidth, PageHeight, Margin), GapRow);
        var wideEdges = FindEdges(RenderTable(wideTable, WidePageWidth, PageHeight, WideMargin), WideGapRow);

        await Assert.That(narrowEdges.Count).IsEqualTo(TwoColumns + 1);
        await Assert.That(Math.Abs(narrowEdges[0] - (int)Margin)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(narrowEdges[TwoColumns] - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);

        await Assert.That(wideEdges.Count).IsEqualTo(TwoColumns + 1);
        await Assert.That(Math.Abs(wideEdges[0] - (int)WideMargin)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(wideEdges[TwoColumns] - (int)(WidePageWidth - WideMargin))).IsLessThanOrEqualTo(EdgeTolerance);
    }

    /// <summary>Proportional / relative column widths divide the available content width according to their weights.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RelativeColumnSizingDividesAvailableWidthProportionally()
    {
        var table = new PdfFlowTable([PdfTableRow.FromText("Key", "Value"), PdfTableRow.FromText("K1", "V1")])
        {
            ColumnSizing = [PdfTableColumnWidth.Relative(TwentyPercent), PdfTableColumnWidth.Relative(EightyPercent)],
            Border = PdfStroke.Black(BorderWidth),
        };

        var edges = FindEdges(RenderTable(table, PageWidth, PageHeight, Margin), GapRow);

        await Assert.That(edges.Count).IsEqualTo(TwoColumns + 1);
        await Assert.That(Math.Abs(edges[0] - TableLeft)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(edges[1] - ExpectedTwentyPercentDivider)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(edges[TwoColumns] - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);
    }

    /// <summary>Mixing fixed, auto, and proportional column widths works in both simple and rich tables.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedFixedAutoAndProportionalColumnSizingWorksInSimpleAndRichTables()
    {
        var simpleTable = new PdfFlowTable([PdfTableRow.FromText("ID", "Tag", "Details"), PdfTableRow.FromText("01", "OK", " Longer description")])
        {
            ColumnSizing = [PdfTableColumnWidth.Fixed(FixedColumnWidth), PdfTableColumnWidth.Auto, PdfTableColumnWidth.Proportional(ThreeWeight)],
            Border = PdfStroke.Black(BorderWidth),
        };
        var richRow = new PdfTableRow([new("01") { FontSize = SmallFontSize }, new("OK"), new("Longer description")]);
        var richTable = new PdfFlowTable([PdfTableRow.FromText("ID", "Tag", "Details"), richRow])
        {
            ColumnSizing = [PdfTableColumnWidth.Fixed(FixedColumnWidth), PdfTableColumnWidth.Auto, PdfTableColumnWidth.Proportional(ThreeWeight)],
            Border = PdfStroke.Black(BorderWidth),
        };

        var simpleEdges = FindEdges(RenderTable(simpleTable, PageWidth, PageHeight, Margin), GapRow);
        var richEdges = FindEdges(RenderTable(richTable, PageWidth, PageHeight, Margin), GapRow);

        await Assert.That(simpleEdges.Count).IsEqualTo(ThreeColumns + 1);
        await Assert.That(Math.Abs(simpleEdges[1] - ExpectedFixedDivider)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(simpleEdges[ThreeColumns] - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(richEdges[1] - ExpectedFixedDivider)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(richEdges[ThreeColumns] - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);
    }

    /// <summary>Cell-level <see cref="PdfTableCell.FontSize"/> produces the same row height as table-level <see cref="PdfFlowTable.FontSize"/> for both empty and non-empty rows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CellFontSizeMatchesTableFontSizeRowHeightForEmptyAndNonEmptyRows()
    {
        var emptyTableFont = new PdfFlowTable([new([new(string.Empty), new(string.Empty)])])
        {
            HeaderRows = 0,
            FontSize = SmallFontSize,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };
        var emptyCellFont = new PdfFlowTable([new([new(string.Empty) { FontSize = SmallFontSize }, new(string.Empty) { FontSize = SmallFontSize }])])
        {
            HeaderRows = 0,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };
        var emptyDefaultFont = new PdfFlowTable([new([new(string.Empty), new(string.Empty)])])
        {
            HeaderRows = 0,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };

        var emptyTableBottom = LastDarkRow(RenderTable(emptyTableFont, PageWidth, PageHeight, Margin), TableLeft);
        var emptyCellBottom = LastDarkRow(RenderTable(emptyCellFont, PageWidth, PageHeight, Margin), TableLeft);
        var emptyDefaultBottom = LastDarkRow(RenderTable(emptyDefaultFont, PageWidth, PageHeight, Margin), TableLeft);

        await Assert.That(Math.Abs(emptyCellBottom - emptyTableBottom)).IsLessThanOrEqualTo(1);
        await Assert.That(emptyCellBottom).IsLessThan(emptyDefaultBottom);

        var textTableFont = new PdfFlowTable([new([new("Alpha"), new("Beta")])])
        {
            HeaderRows = 0,
            FontSize = SmallFontSize,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };
        var textCellFont = new PdfFlowTable([new([new("Alpha") { FontSize = SmallFontSize }, new("Beta") { FontSize = SmallFontSize }])])
        {
            HeaderRows = 0,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };

        var textTableBottom = LastDarkRow(RenderTable(textTableFont, PageWidth, PageHeight, Margin), TableLeft);
        var textCellBottom = LastDarkRow(RenderTable(textCellFont, PageWidth, PageHeight, Margin), TableLeft);

        await Assert.That(Math.Abs(textCellBottom - textTableBottom)).IsLessThanOrEqualTo(1);
    }

    /// <summary>Mixed font sizes in a row size the row to the tallest cell, explicit <see cref="PdfTableRow.Height"/> sets a minimum height, and compact cell fonts fit more rows per page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedCellFontSizesExplicitRowHeightAndPaginationWorkConsistently()
    {
        var smallRowTable = new PdfFlowTable([new([new("A") { FontSize = SmallFontSize }, new("B") { FontSize = SmallFontSize }])])
        {
            HeaderRows = 0,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };
        var mixedRowTable = new PdfFlowTable([new([new("A") { FontSize = SmallFontSize }, new("B") { FontSize = LargeFontSize }])])
        {
            HeaderRows = 0,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };
        var explicitRowTable = new PdfFlowTable([new([new("A") { FontSize = SmallFontSize }, new("B") { FontSize = SmallFontSize }]) { Height = ExplicitRowHeight }])
        {
            HeaderRows = 0,
            ColumnWidths = [FixedColumnWidth, FixedColumnWidth],
            Border = PdfStroke.Black(BorderWidth),
        };

        var smallBottom = LastDarkRow(RenderTable(smallRowTable, PageWidth, PageHeight, Margin), TableLeft);
        var mixedBottom = LastDarkRow(RenderTable(mixedRowTable, PageWidth, PageHeight, Margin), TableLeft);
        var explicitBottom = LastDarkRow(RenderTable(explicitRowTable, PageWidth, PageHeight, Margin), TableLeft);

        await Assert.That(mixedBottom).IsGreaterThan(smallBottom);
        await Assert.That(explicitBottom).IsGreaterThan(mixedBottom);

        var compactRows = new List<PdfTableRow>();
        var defaultRows = new List<PdfTableRow>();
        for (var i = 0; i < CompactTableRows; i++)
        {
            compactRows.Add(new([new($"R{i:D2}") { FontSize = SmallFontSize }]));
            defaultRows.Add(new([new($"R{i:D2}")]));
        }

        var compactPages = CountPages(new(compactRows) { HeaderRows = 0 }, ShortPageHeight);
        var defaultPages = CountPages(new(defaultRows) { HeaderRows = 0 }, ShortPageHeight);

        await Assert.That(compactPages).IsLessThan(defaultPages);
    }

    /// <summary>Invalid column sizing or row height throws <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidColumnSizingOrRowHeightThrowsArgumentOutOfRangeException()
    {
        await Assert.That(static () => PdfTableColumnWidth.Fixed(0F)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => PdfTableColumnWidth.Proportional(-1F)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => PdfTableColumnWidth.Relative(float.NaN)).Throws<ArgumentOutOfRangeException>();

        var badRowTable = new PdfFlowTable([new([new("A")]) { Height = 0F }]) { HeaderRows = 0 };
        await Assert.That(() => RenderTable(badRowTable, PageWidth, PageHeight, Margin)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Renders a single table on the first page.</summary>
    /// <param name="table">The table to render.</param>
    /// <param name="pageWidth">The page width.</param>
    /// <param name="pageHeight">The page height.</param>
    /// <param name="margin">The page margin.</param>
    /// <returns>The rendered first page.</returns>
    private static RenderedImage RenderTable(PdfFlowTable table, float pageWidth, float pageHeight, float margin)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        _ = PdfTextFlow.Add(creator, [table], new(font) { PageWidth = pageWidth, PageHeight = pageHeight, Margin = margin, BodySize = BodySize }, CancellationToken.None);
        var pdf = creator.ToArray();
        using var page = new RenderTestPage(pdf);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(page.Document, 0), 0, 1F, out var width, out var height);
        return page.RenderTile(new(0, 1F, 0, 0, 0, PdfRenderFlags.None), width, height);
    }

    /// <summary>Counts the pages needed to lay out a table.</summary>
    /// <param name="table">The table.</param>
    /// <param name="pageHeight">The page height.</param>
    /// <returns>The number of pages.</returns>
    private static int CountPages(PdfFlowTable table, float pageHeight)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        return PdfTextFlow.Add(creator, [table], new(font) { PageWidth = PageWidth, PageHeight = pageHeight, Margin = Margin, BodySize = BodySize }, CancellationToken.None);
    }

    /// <summary>Finds the left edge of each run of dark ink along a row.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="y">The pixel row.</param>
    /// <returns>The left edges of dark ink runs.</returns>
    private static List<int> FindEdges(RenderedImage image, int y)
    {
        var edges = new List<int>();
        var inside = false;
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = CreationTestKit.PixelAt(image, x, y);
            var dark = pixel.Red < Dark && pixel.Green < Dark && pixel.Blue < Dark;
            if (dark && !inside)
            {
                edges.Add(x);
            }

            inside = dark;
        }

        return edges;
    }

    /// <summary>Finds the lowest row with dark ink in a column.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="x">The pixel column.</param>
    /// <returns>The row index from the top.</returns>
    private static int LastDarkRow(RenderedImage image, int x)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            var pixel = CreationTestKit.PixelAt(image, x, y);
            if (pixel.Red < Dark && pixel.Green < Dark && pixel.Blue < Dark)
            {
                return y;
            }
        }

        return -1;
    }
}
