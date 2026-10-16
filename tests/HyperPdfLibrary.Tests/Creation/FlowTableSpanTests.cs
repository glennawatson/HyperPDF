// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests for merged table cells using <see cref="PdfTableCell.ColumnSpan"/> and <see cref="PdfTableCell.RowSpan"/> (Issue #210).</summary>
public sealed class FlowTableSpanTests
{
    /// <summary>The width of the test pages in points.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test pages in points.</summary>
    private const float PageHeight = 400F;

    /// <summary>The page margin in points.</summary>
    private const float Margin = 20F;

    /// <summary>The body font size in points.</summary>
    private const float BodySize = 11F;

    /// <summary>The width of each of the three test columns.</summary>
    private const float ColWidth = 80F;

    /// <summary>The border stroke width in points.</summary>
    private const float BorderWidth = 2F;

    /// <summary>The left edge of the table in pixels.</summary>
    private const int TableLeft = 20;

    /// <summary>The boundary between column 0 and column 1 in pixels.</summary>
    private const int FirstDivider = 100;

    /// <summary>The boundary between column 1 and column 2 in pixels.</summary>
    private const int SecondDivider = 180;

    /// <summary>The right edge of the 3-column table in pixels.</summary>
    private const int TableRight = 260;

    /// <summary>A pixel row inside the first table row, between its top border and its text.</summary>
    private const int RowZeroGapY = 23;

    /// <summary>A pixel row inside the second table row, between its top border and its text.</summary>
    private const int RowOneGapY = 47;

    /// <summary>The top pixel row of text in the first row.</summary>
    private const int RowZeroTextTop = 25;

    /// <summary>The bottom pixel row of text in the first row.</summary>
    private const int RowZeroTextBottom = 36;

    /// <summary>The pixel threshold below which a channel counts as dark ink.</summary>
    private const int DarkThreshold = 128;

    /// <summary>The number of columns in the three-column test table.</summary>
    private const int ThreeColumns = 3;

    /// <summary>The span of two columns or rows.</summary>
    private const int SpanTwo = 2;

    /// <summary>The invalid span beyond the 3-column table.</summary>
    private const int InvalidSpanFour = 4;

    /// <summary>The tolerance in pixels for centred text alignment.</summary>
    private const int CenterTolerance = 3;

    /// <summary>One half, for centring calculations.</summary>
    private const float Half = 0.5F;

    /// <summary>The inset in pixels used when scanning inside a cell away from its borders.</summary>
    private const int ScanInset = 4;

    /// <summary>The number of body groups in the multi-page spanned table test.</summary>
    private const int MultiPageGroups = 25;

    /// <summary>The number of words in a wrapped spanned cell.</summary>
    private const int WrappedWordCount = 12;

    /// <summary>The expected number of TD elements in the tagged span test.</summary>
    private const int ExpectedTaggedDataCells = 5;

    /// <summary>The label of the row-spanned group cell.</summary>
    private const string GroupLabel = "Group";

    /// <summary>A cell with <c>ColumnSpan &gt; 1</c> spans the combined width of the covered columns and omits the interior vertical border.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColumnSpanMergesWidthAndOmitsInteriorVerticalBorder()
    {
        var row0 = new PdfTableRow([new PdfTableCell("Merged") { ColumnSpan = SpanTwo }, new("C")]);
        var row1 = PdfTableRow.FromText("A", "B", "C");
        var table = new PdfFlowTable([row0, row1]) { HeaderRows = 0, ColumnWidths = [ColWidth, ColWidth, ColWidth], Border = PdfStroke.Black(BorderWidth) };

        var render = RenderFirstPage(table);

        await Assert.That(IsDark(render, TableLeft, RowZeroGapY)).IsTrue();
        await Assert.That(IsDark(render, FirstDivider, RowZeroGapY)).IsFalse();
        await Assert.That(IsDark(render, SecondDivider, RowZeroGapY)).IsTrue();
        await Assert.That(IsDark(render, TableRight, RowZeroGapY)).IsTrue();

        await Assert.That(IsDark(render, FirstDivider, RowOneGapY)).IsTrue();
        await Assert.That(IsDark(render, SecondDivider, RowOneGapY)).IsTrue();
    }

    /// <summary>A full-width section row (<c>ColumnSpan</c> equal to the column count) renders centred text across the entire table width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FullWidthSectionRowCentersTextAcrossEntireTable()
    {
        var sectionRow = new PdfTableRow([new PdfTableCell("Section") { ColumnSpan = ThreeColumns, Alignment = PdfTableAlignment.Center }]);
        var dataRow = PdfTableRow.FromText("One", "Two", "Three");
        var table = new PdfFlowTable([sectionRow, dataRow]) { HeaderRows = 0, ColumnWidths = [ColWidth, ColWidth, ColWidth], Border = PdfStroke.Black(BorderWidth) };

        var render = RenderFirstPage(table);
        var ink = FindHorizontalInk(render, TableLeft + ScanInset, TableRight - ScanInset, RowZeroTextTop, RowZeroTextBottom);
        const float expectedCenter = (TableLeft + TableRight) * Half;
        var actualCenter = (ink.First + ink.Last) * Half;

        await Assert.That(IsDark(render, FirstDivider, RowZeroGapY)).IsFalse();
        await Assert.That(IsDark(render, SecondDivider, RowZeroGapY)).IsFalse();
        await Assert.That(Math.Abs(actualCenter - expectedCenter)).IsLessThanOrEqualTo(CenterTolerance);
    }

    /// <summary>A cell with <c>RowSpan &gt; 1</c> covers multiple rows, omits the interior horizontal border in its column, and shifts subsequent rows' cells past the occupied column.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowSpanCoversMultipleRowsAndShiftsSubsequentCells()
    {
        var row0 = new PdfTableRow([new PdfTableCell(GroupLabel) { RowSpan = SpanTwo }, new("R0C1"), new("R0C2")]);
        var row1 = new PdfTableRow([new("R1C1"), new("R1C2")]);
        var table = new PdfFlowTable([row0, row1]) { HeaderRows = 0, ColumnWidths = [ColWidth, ColWidth, ColWidth], Border = PdfStroke.Black(BorderWidth) };

        var pdf = CreatePdf(table, out _);
        var render = RenderAt(pdf, 0);
        var text = await CreatedFiles.TextOfAsync(pdf, 0);
        var boundaryY = FindHorizontalBorderRow(render, FirstDivider + ScanInset, RowZeroTextBottom, RowOneGapY);

        await Assert.That(boundaryY).IsGreaterThan(0);
        await Assert.That(IsDark(render, TableLeft + (int)(ColWidth * Half), boundaryY)).IsFalse();
        await Assert.That(IsDark(render, FirstDivider + (int)(ColWidth * Half), boundaryY)).IsTrue();
        await Assert.That(IsDark(render, SecondDivider + (int)(ColWidth * Half), boundaryY)).IsTrue();
        await Assert.That(text).Contains(GroupLabel);
        await Assert.That(text).Contains("R0C1");
        await Assert.That(text).Contains("R1C1");
        await Assert.That(text).Contains("R1C2");
    }

    /// <summary>A table combining row spans, column spans, normal cells and text wrapping lays out without overlapping text or losing content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CombinedRowAndColumnSpansWithWrappingLayOutCleanly()
    {
        var wrapped = string.Join(' ', Enumerable.Range(0, WrappedWordCount).Select(static i => $"wrap{i:D2}"));
        PdfTableRow[] rows =
        [
            new([new PdfTableCell("Title") { ColumnSpan = ThreeColumns, Alignment = PdfTableAlignment.Center }]),
            new([new PdfTableCell("Side") { RowSpan = SpanTwo }, new PdfTableCell(wrapped) { ColumnSpan = SpanTwo }]),
            new([new("LeftVal"), new("RightVal")]),
        ];
        var table = new PdfFlowTable(rows) { HeaderRows = 1, ColumnWidths = [ColWidth, ColWidth, ColWidth], Border = PdfStroke.Black(BorderWidth) };

        var pdf = CreatePdf(table, out var pages);
        var text = await CreatedFiles.TextOfAsync(pdf, 0);

        await Assert.That(pages).IsEqualTo(1);
        await Assert.That(text).Contains("Title");
        await Assert.That(text).Contains("Side");
        await Assert.That(text).Contains("LeftVal");
        await Assert.That(text).Contains("RightVal");
        for (var i = 0; i < WrappedWordCount; i++)
        {
            await Assert.That(text).Contains($"wrap{i:D2}");
        }
    }

    /// <summary>A table with merged cells that crosses page breaks preserves all cell text without duplication or loss.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SpannedTableAcrossPageBreaksPreservesAllTextWithoutDuplication()
    {
        var rows = new List<PdfTableRow> { new([new(GroupLabel), new PdfTableCell("Details") { ColumnSpan = SpanTwo }]) };
        for (var g = 0; g < MultiPageGroups; g++)
        {
            rows.Add(new([new PdfTableCell($"Grp{g:D2}") { RowSpan = SpanTwo }, new($"KeyA{g:D2}"), new($"ValA{g:D2}")]));
            rows.Add(new([new($"KeyB{g:D2}"), new($"ValB{g:D2}")]));
        }

        var pdf = CreatePdf(new(rows) { HeaderRows = 1, ColumnWidths = [ColWidth, ColWidth, ColWidth], Border = PdfStroke.Black(1F) }, out var pages);
        var pageTexts = new string[pages];
        for (var p = 0; p < pages; p++)
        {
            pageTexts[p] = await CreatedFiles.TextOfAsync(pdf, p);
        }

        var combined = string.Concat(pageTexts);
        await Assert.That(pages).IsGreaterThan(1);
        for (var g = 0; g < MultiPageGroups; g++)
        {
            await Assert.That(CountOccurrences(combined, $"Grp{g:D2}")).IsEqualTo(1);
            await Assert.That(CountOccurrences(combined, $"KeyA{g:D2}")).IsEqualTo(1);
            await Assert.That(CountOccurrences(combined, $"ValA{g:D2}")).IsEqualTo(1);
            await Assert.That(CountOccurrences(combined, $"KeyB{g:D2}")).IsEqualTo(1);
            await Assert.That(CountOccurrences(combined, $"ValB{g:D2}")).IsEqualTo(1);
        }
    }

    /// <summary>A tagged table with row and column spans records the spans in the structure tree and passes PDF/UA-2 accessibility checks.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedSpannedTablePassesAccessibilityChecks(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = "Spans", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        PdfTableRow[] rows =
        [
            new([new("Category"), new PdfTableCell("Metrics") { ColumnSpan = SpanTwo }]),
            new([new PdfTableCell("Alpha") { RowSpan = SpanTwo }, new("M1"), new("100")]),
            new([new("M2"), new("200")]),
        ];
        var table = new PdfFlowTable(rows) { HeaderRows = 1, ColumnWidths = [ColWidth, ColWidth, ColWidth], Border = PdfStroke.Black(1F) };

        _ = PdfTextFlow.Add(creator, [table], CreateOptions(font), cancellationToken);
        _ = creator.Finish();
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(CountStructureType(root, "TH")).IsEqualTo(SpanTwo);
        await Assert.That(CountStructureType(root, "TD")).IsEqualTo(ExpectedTaggedDataCells);
        await Assert.That(accessibility.Findings).IsEmpty();
    }

    /// <summary>Zero or negative spans throw <see cref="ArgumentOutOfRangeException"/>, and spans exceeding table bounds or overlapping throw <see cref="ArgumentException"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidSpanConfigurationsThrowUsefulExceptions()
    {
        await Assert.That(static () => LayoutTable(new([new([new PdfTableCell("A") { ColumnSpan = 0 }])]))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => LayoutTable(new([new([new PdfTableCell("A") { RowSpan = 0 }])]))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => LayoutTable(new([new([new PdfTableCell("A") { ColumnSpan = -1 }])]))).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => LayoutTable(new([new([new PdfTableCell("A") { RowSpan = -1 }])]))).Throws<ArgumentOutOfRangeException>();

        // RowSpan extends past the last row of the table.
        await Assert.That(static () => LayoutTable(new([new([new PdfTableCell("A") { RowSpan = SpanTwo }, new("B")])]))).Throws<ArgumentException>();

        // Header RowSpan extends into body rows.
        await Assert.That(static () => LayoutTable(new([new([new PdfTableCell("H") { RowSpan = SpanTwo }, new("H2")]), PdfTableRow.FromText("B")]))).Throws<ArgumentException>();

        // ColumnSpan exceeds the established column count of the table.
        await Assert.That(static () => LayoutTable(new([PdfTableRow.FromText("A", "B", "C"), new([new PdfTableCell("TooWide") { ColumnSpan = InvalidSpanFour }])]))).Throws<ArgumentException>();

        // ColumnSpan overlaps an active RowSpan from an earlier row.
        PdfTableRow[] overlapping =
        [
            new([new("A"), new PdfTableCell("Tall") { RowSpan = SpanTwo }, new("C")]),
            new([new PdfTableCell("Wide") { ColumnSpan = SpanTwo }, new("D")]),
        ];
        await Assert.That(() => LayoutTable(new(overlapping) { HeaderRows = 0 })).Throws<ArgumentException>();
    }

    /// <summary>Creates the test flow options.</summary>
    /// <param name="font">The body font.</param>
    /// <returns>The flow options.</returns>
    private static PdfFlowOptions CreateOptions(PdfCanvasFont font) =>
        new(font) { PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin, BodySize = BodySize };

    /// <summary>Lays out a table in a new document.</summary>
    /// <param name="table">The table.</param>
    private static void LayoutTable(PdfFlowTable table) => _ = CreatePdf(table, out _);

    /// <summary>Creates an untagged PDF containing a single table.</summary>
    /// <param name="table">The table.</param>
    /// <param name="pages">Receives the page count.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreatePdf(PdfFlowTable table, out int pages)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        pages = PdfTextFlow.Add(creator, [table], CreateOptions(font), CancellationToken.None);
        return creator.ToArray();
    }

    /// <summary>Renders the first page of a single-table document at 1 pixel per point.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The rendered image.</returns>
    private static RenderedImage RenderFirstPage(PdfFlowTable table) => RenderAt(CreatePdf(table, out _), 0);

    /// <summary>Renders a page at 1 pixel per point.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The rendered image.</returns>
    private static RenderedImage RenderAt(byte[] pdf, int pageIndex)
    {
        using var page = new RenderTestPage(pdf);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(page.Document, pageIndex), 0, 1F, out var width, out var height);
        return page.RenderTile(new(pageIndex, 1F, 0, 0, 0, PdfRenderFlags.None), width, height);
    }

    /// <summary>Checks whether a pixel is dark ink.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="x">The pixel column.</param>
    /// <param name="y">The pixel row from the top.</param>
    /// <returns><see langword="true"/> when all channels are below <see cref="DarkThreshold"/>.</returns>
    private static bool IsDark(RenderedImage image, int x, int y)
    {
        var pixel = CreationTestKit.PixelAt(image, x, y);
        return pixel.Red < DarkThreshold && pixel.Green < DarkThreshold && pixel.Blue < DarkThreshold;
    }

    /// <summary>Finds the first pixel row in a vertical range where a horizontal border line appears at column <paramref name="x"/>.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="x">The pixel column to scan.</param>
    /// <param name="startY">The start row.</param>
    /// <param name="endY">The end row.</param>
    /// <returns>The pixel row of the border, or -1 if none is found.</returns>
    private static int FindHorizontalBorderRow(RenderedImage image, int x, int startY, int endY)
    {
        for (var y = startY; y <= endY; y++)
        {
            if (IsDark(image, x, y))
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>Finds the leftmost and rightmost dark ink columns within a rectangular region.</summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="left">The left pixel bound.</param>
    /// <param name="right">The right pixel bound.</param>
    /// <param name="top">The top pixel bound.</param>
    /// <param name="bottom">The bottom pixel bound.</param>
    /// <returns>The horizontal ink bounds.</returns>
    private static InkBounds FindHorizontalInk(RenderedImage image, int left, int right, int top, int bottom)
    {
        var first = -1;
        var last = -1;
        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                if (!IsDark(image, x, y))
                {
                    continue;
                }

                first = first < 0 ? x : first;
                last = x;
            }
        }

        return new(first, last);
    }

    /// <summary>Counts non-overlapping occurrences of a substring.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="value">The substring to count.</param>
    /// <returns>The occurrence count.</returns>
    private static int CountOccurrences(string text, string value) => text.Split(value).Length - 1;

    /// <summary>Counts structure elements with a given raw type name.</summary>
    /// <param name="element">The root structure element.</param>
    /// <param name="type">The structure type name.</param>
    /// <returns>The number of matching elements.</returns>
    private static int CountStructureType(PdfStructureElement element, string type)
    {
        var count = element.RawType == type ? 1 : 0;
        foreach (var kid in element.Kids)
        {
            count += kid.Element is { } child ? CountStructureType(child, type) : 0;
        }

        return count;
    }

    /// <summary>The leftmost and rightmost ink columns in a scan region.</summary>
    /// <param name="First">The leftmost ink column.</param>
    /// <param name="Last">The rightmost ink column.</param>
    private readonly record struct InkBounds(int First, int Last);
}
