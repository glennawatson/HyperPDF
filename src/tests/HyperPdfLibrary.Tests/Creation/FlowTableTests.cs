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

/// <summary>Table flow blocks: column widths, alignment, wrapping, borders, page breaks, repeated headers and tagging.</summary>
public sealed class FlowTableTests
{
    /// <summary>The width of the test pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test pages.</summary>
    private const float PageHeight = 400F;

    /// <summary>The page margin.</summary>
    private const float Margin = 20F;

    /// <summary>The height of the test pages in pixels.</summary>
    private const int PageRows = 400;

    /// <summary>The body size of the test text.</summary>
    private const float BodySize = 11F;

    /// <summary>The line pitch of the test text: the body size times the default line spacing.</summary>
    private const float Leading = 15.4F;

    /// <summary>The default cell padding.</summary>
    private const float Padding = 4F;

    /// <summary>The text of the label cell in the tall row.</summary>
    private const string LabelCell = "Label";

    /// <summary>The level of the second heading.</summary>
    private const int SecondLevel = 2;

    /// <summary>One half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The number of sides of a cell that have padding along one axis.</summary>
    private const float Sides = 2F;

    /// <summary>The width of the borders the tests look for, so a border covers whole pixels.</summary>
    private const float BorderWidth = 2F;

    /// <summary>The width of a thin border for tables that run over many pages.</summary>
    private const float ThinBorder = 1F;

    /// <summary>The first column's width.</summary>
    private const float FirstWidth = 60F;

    /// <summary>The second column's width.</summary>
    private const float SecondWidth = 100F;

    /// <summary>The third column's width.</summary>
    private const float ThirdWidth = 60F;

    /// <summary>The left edge of the second column, as a pixel column.</summary>
    private const int SecondEdge = 80;

    /// <summary>The right edge of the table of two explicit columns, as a pixel column.</summary>
    private const int TableRight = 180;

    /// <summary>The left edge of the table, as a pixel column.</summary>
    private const int TableLeft = 20;

    /// <summary>A row of pixels inside the first row, between its top border and its text.</summary>
    private const int GapRow = 23;

    /// <summary>A pixel column to the right of the two explicit columns.</summary>
    private const int BeyondTable = 200;

    /// <summary>A pixel row well below the two-row table.</summary>
    private const int BelowTable = 120;

    /// <summary>The first pixel row of the first row's text.</summary>
    private const int FirstTextTop = 26;

    /// <summary>The last pixel row of the first row's text.</summary>
    private const int FirstTextBottom = 34;

    /// <summary>The first pixel row of the second row's text.</summary>
    private const int SecondTextTop = 50;

    /// <summary>The last pixel row of the second row's text.</summary>
    private const int SecondTextBottom = 57;

    /// <summary>How far an aligned line may sit from where it should, in pixels.</summary>
    private const int AlignTolerance = 2;

    /// <summary>The space the border and the next column take from a scan of one column's text.</summary>
    private const int ScanInset = 3;

    /// <summary>The right edge of the text area, as a pixel column.</summary>
    private const int TextRight = 280;

    /// <summary>The right edge a table of long text must reach at least, as a pixel column.</summary>
    private const int WideEnough = 270;

    /// <summary>The level below which a channel counts as ink.</summary>
    private const int Dark = 128;

    /// <summary>The rows in the table that runs over many pages.</summary>
    private const int LongRows = 80;

    /// <summary>The words in the cell that runs across pages.</summary>
    private const int TallWords = 300;

    /// <summary>The pixels at the edge of the page that are checked for clipping, kept clear of the margin and a border.</summary>
    private const int EdgeGuard = 3;

    /// <summary>The number of repeats of a word in the wrapping cell.</summary>
    private const int WrapWords = 10;

    /// <summary>The number of rows in the tagged table.</summary>
    private const int TaggedRows = 40;

    /// <summary>The number of columns in the tagged table.</summary>
    private const int TaggedColumns = 2;

    /// <summary>The size of an image in pixels.</summary>
    private const int ImageSize = 4;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The part of PDF/UA claimed.</summary>
    private const int ClaimedPart = 2;

    /// <summary>The tolerance of a border's bottom edge, in pixels.</summary>
    private const int EdgeTolerance = 2;

    /// <summary>Explicit column widths put the vertical borders where the widths say and the table stops at its last column.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplicitWidthsPlaceTheBorders()
    {
        var table = new PdfFlowTable([PdfTableRow.FromText("AA", "BB"), PdfTableRow.FromText("CC", "DD")]) { ColumnWidths = [FirstWidth, SecondWidth], Border = PdfStroke.Black(BorderWidth) };

        var render = RenderFirstPage(CreateFlow(table), CreateOptions);
        const int row = GapRow;

        await Assert.That(IsDark(render, TableLeft, row)).IsTrue();
        await Assert.That(IsDark(render, SecondEdge, row)).IsTrue();
        await Assert.That(IsDark(render, SecondEdge - 1, row)).IsTrue();
        await Assert.That(IsDark(render, TableRight, row)).IsTrue();
        await Assert.That(IsDark(render, TableRight - 1, row)).IsTrue();
        await Assert.That(IsDark(render, BeyondTable, row)).IsFalse();
        await Assert.That(IsDark(render, TableLeft + ScanInset, row)).IsFalse();
        await Assert.That(IsDark(render, SecondEdge, BelowTable)).IsFalse();
    }

    /// <summary>Columns with no width are sized to their text and the table stays narrower than the page when the text is short.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoWidthsFollowTheText()
    {
        var table = new PdfFlowTable([PdfTableRow.FromText("N", "Description"), PdfTableRow.FromText("1", "A longer description")]) { Border = PdfStroke.Black(BorderWidth) };

        var render = RenderFirstPage(CreateFlow(table), CreateOptions);
        var edges = FindEdges(render, GapRow);

        await Assert.That(edges.Count).IsEqualTo(TaggedColumns + 1);
        await Assert.That(edges[2] - edges[1]).IsGreaterThan(edges[1] - edges[0]);
        await Assert.That(edges[2]).IsLessThan(WideEnough);
    }

    /// <summary>Text wider than the page shares the page's width between the columns and wraps, so the table ends at the margin.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AutoWidthsShrinkToThePage()
    {
        var longText = string.Join(' ', Enumerable.Repeat("overlong", TallWords / WrapWords));
        var table = new PdfFlowTable([PdfTableRow.FromText("Key", longText)]) { HeaderRows = 0, Border = PdfStroke.Black(BorderWidth) };

        var render = RenderFirstPage(CreateFlow(table), CreateOptions);
        var edges = FindEdges(render, GapRow);

        await Assert.That(edges.Count).IsEqualTo(TaggedColumns + 1);
        await Assert.That(edges[2]).IsGreaterThan(WideEnough);
        await Assert.That(edges[2]).IsLessThanOrEqualTo(TextRight);
    }

    /// <summary>Left, centred and right alignment put a column's text where it belongs, and a cell can override its column.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AlignmentPlacesTheText()
    {
        var overridden = new PdfTableRow([new PdfTableCell("L") { Alignment = PdfTableAlignment.Right }, new("M"), new("R")]);
        var table = new PdfFlowTable([PdfTableRow.FromText("L", "M", "R"), overridden])
        {
            HeaderRows = 0,
            ColumnWidths = [FirstWidth, SecondWidth, ThirdWidth],
            ColumnAlignments = [PdfTableAlignment.Left, PdfTableAlignment.Center, PdfTableAlignment.Right],
            Border = PdfStroke.Black(BorderWidth),
        };

        var render = RenderFirstPage(CreateFlow(table), CreateOptions);
        var firstEnd = TableLeft + (int)FirstWidth;
        var secondEnd = firstEnd + (int)SecondWidth;
        var thirdEnd = secondEnd + (int)ThirdWidth;
        var left = FindInk(render, TableLeft + ScanInset, firstEnd - ScanInset, FirstTextTop, FirstTextBottom);
        var middle = FindInk(render, firstEnd + ScanInset, secondEnd - ScanInset, FirstTextTop, FirstTextBottom);
        var right = FindInk(render, secondEnd + ScanInset, thirdEnd - ScanInset, FirstTextTop, FirstTextBottom);
        var forcedRight = FindInk(render, TableLeft + ScanInset, firstEnd - ScanInset, SecondTextTop, SecondTextBottom);

        await Assert.That(Math.Abs(left.First - (TableLeft + Padding))).IsLessThanOrEqualTo(AlignTolerance);
        await Assert.That(Math.Abs(((middle.First + middle.Last) * Half) - (firstEnd + (SecondWidth * Half)))).IsLessThanOrEqualTo(AlignTolerance);
        await Assert.That(Math.Abs(right.Last - (thirdEnd - Padding))).IsLessThanOrEqualTo(AlignTolerance);
        await Assert.That(Math.Abs(forcedRight.Last - (firstEnd - Padding))).IsLessThanOrEqualTo(AlignTolerance);
    }

    /// <summary>A cell with a lot of text wraps to its column and makes its row as tall as the wrapped lines need.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongCellTextWrapsAndGrowsTheRow()
    {
        var text = string.Join(' ', Enumerable.Repeat("word", WrapWords));
        var table = new PdfFlowTable([PdfTableRow.FromText(text, "x")]) { HeaderRows = 0, ColumnWidths = [FirstWidth, FirstWidth], Border = PdfStroke.Black(BorderWidth) };
        var lines = new List<PdfTextLine>();
        var creator = CreateCreator(tagged: false);
        PdfTextWrapper.Wrap(creator.EmbedFont(CreationTestKit.OpenFont()), BodySize, text, FirstWidth - (Padding * Sides), lines);

        var render = RenderFirstPage(CreateFlow(table), CreateOptions);
        var expectedBottom = Margin + (Padding * Sides) + (lines.Count * Leading);

        await Assert.That(lines.Count).IsGreaterThan(1);
        await Assert.That(Math.Abs(LastDarkRow(render, TableLeft) - expectedBottom)).IsLessThanOrEqualTo(EdgeTolerance);
    }

    /// <summary>A table with no borders draws no lines at all.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoBorderDrawsNoLines()
    {
        var table = new PdfFlowTable([PdfTableRow.FromText(string.Empty, string.Empty), PdfTableRow.FromText(string.Empty, string.Empty)]) { Border = null };

        var render = RenderFirstPage(CreateFlow(table), CreateOptions);

        await Assert.That(IsBlank(render, 0, PageRows)).IsTrue();
    }

    /// <summary>A table that runs over many pages repeats its header on each, keeps every row whole and leaves the margins clear.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ManyRowsRunOntoNewPagesWithRepeatedHeaders()
    {
        var rows = new List<PdfTableRow> { PdfTableRow.FromText("Name", "Value") };
        for (var i = 0; i < LongRows; i++)
        {
            rows.Add(PdfTableRow.FromText($"Item{i:D3}", $"Value{i:D3}"));
        }

        var pdf = CreatePdf(new(rows) { Border = PdfStroke.Black(ThinBorder) }, out var pages);
        var texts = await ReadPagesAsync(pdf, pages);

        await Assert.That(pages).IsGreaterThan(1);
        for (var p = 0; p < pages; p++)
        {
            await Assert.That(CountOf(texts[p], "Name")).IsEqualTo(1);
            await Assert.That(IsBlank(RenderAt(pdf, p), 0, (int)Margin - EdgeGuard)).IsTrue();
            await Assert.That(IsBlank(RenderAt(pdf, p), PageRows - (int)Margin + EdgeGuard, PageRows)).IsTrue();
        }

        var all = string.Concat(texts);
        for (var i = 0; i < LongRows; i++)
        {
            await Assert.That(CountOf(all, $"Item{i:D3}")).IsEqualTo(1);
            await Assert.That(texts.Count(text => text.Contains($"Item{i:D3}", StringComparison.Ordinal) && text.Contains($"Value{i:D3}", StringComparison.Ordinal))).IsEqualTo(1);
        }
    }

    /// <summary>A row taller than a page continues on the next pages without losing or repeating text, and the header repeats above each part.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowTallerThanAPageContinuesOnTheNextPage()
    {
        var words = Enumerable.Range(0, TallWords).Select(static i => $"w{i:D3}");
        var rows = new[] { PdfTableRow.FromText("Head", "Body"), PdfTableRow.FromText(LabelCell, string.Join(' ', words)), PdfTableRow.FromText("After", "Done") };

        var pdf = CreatePdf(new(rows) { ColumnWidths = [FirstWidth, SecondWidth], Border = PdfStroke.Black(ThinBorder) }, out var pages);
        var texts = await ReadPagesAsync(pdf, pages);
        var all = string.Concat(texts);

        await Assert.That(pages).IsGreaterThan(TaggedColumns);
        for (var i = 0; i < TallWords; i++)
        {
            await Assert.That(CountOf(all, $"w{i:D3}")).IsEqualTo(1);
        }

        for (var p = 0; p < pages; p++)
        {
            await Assert.That(CountOf(texts[p], "Head")).IsEqualTo(1);
            await Assert.That(IsBlank(RenderAt(pdf, p), 0, (int)Margin - EdgeGuard)).IsTrue();
            await Assert.That(IsBlank(RenderAt(pdf, p), PageRows - (int)Margin + EdgeGuard, PageRows)).IsTrue();
        }

        await Assert.That(CountOf(all, LabelCell)).IsEqualTo(1);
        await Assert.That(CountOf(texts[^1], "Done")).IsEqualTo(1);
    }

    /// <summary>A tagged report with headings, paragraphs, an image and a table across pages has the table structure, no accessibility findings and a PDF/UA claim.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedReportKeepsTheTableStructure(CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: true);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var image = creator.AddRaster(CreateSquare(), PdfImageOptions.Default, cancellationToken);
        var rows = new List<PdfTableRow> { PdfTableRow.FromText("Name", "Value") };
        for (var i = 0; i < TaggedRows; i++)
        {
            rows.Add(PdfTableRow.FromText($"Item{i:D3}", $"Value{i:D3}"));
        }

        PdfFlowBlock[] blocks =
        [
            new PdfFlowHeading(1, "Report"),
            new PdfFlowParagraph("An introduction to the table below."),
            new PdfFlowImage(image, "A red square", 0),
            new PdfFlowHeading(SecondLevel, "Data"),
            new PdfFlowTable(rows),
            new PdfFlowParagraph("A closing paragraph after the table."),
        ];

        var pages = PdfTextFlow.Add(creator, blocks, CreateOptions(font), cancellationToken);
        var report = creator.Finish();
        var pdf = creator.ToArray();
        CreatedFiles.Keep("tagged-table.pdf", pdf);

        using var document = PdfDocumentReader.Open(pdf, null);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(pages).IsGreaterThan(1);
        await Assert.That(CountType(root, "Table")).IsEqualTo(1);
        await Assert.That(CountType(root, "TR")).IsEqualTo(TaggedRows + 1);
        await Assert.That(CountType(root, "TH")).IsEqualTo(TaggedColumns);
        await Assert.That(CountType(root, "TD")).IsEqualTo(TaggedRows * TaggedColumns);
        await Assert.That(accessibility.Findings.Select(static finding => finding.Code).ToArray()).IsEmpty();
        await Assert.That(accessibility.Claim.Part).IsEqualTo(ClaimedPart);
        await Assert.That(report.ClaimsPdfUa).IsTrue();
        await CreationTestKit.CheckWithQpdfAsync("tagged-table", pdf, cancellationToken);
    }

    /// <summary>A tagged table whose row crosses pages has one cell element with content on each page and passes the checks.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedRowAcrossPagesStaysOneElement(CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: true);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var words = string.Join(' ', Enumerable.Range(0, TallWords).Select(static i => $"w{i:D3}"));
        var table = new PdfFlowTable([PdfTableRow.FromText("Head", "Body"), PdfTableRow.FromText(LabelCell, words)]) { ColumnWidths = [FirstWidth, SecondWidth] };

        var pages = PdfTextFlow.Add(creator, [new PdfFlowHeading(1, "Tall"), table], CreateOptions(font), cancellationToken);
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);

        await Assert.That(pages).IsGreaterThan(TaggedColumns);
        await Assert.That(CountType(root, "TR")).IsEqualTo(TaggedColumns);
        await Assert.That(CountType(root, "TD")).IsEqualTo(TaggedColumns);
        await Assert.That(accessibility.Findings.Select(static finding => finding.Code).ToArray()).IsEmpty();
    }

    /// <summary>A table with no rows adds nothing, and a header count beyond the rows is treated as all rows.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyAndHeaderOnlyTablesAreAccepted()
    {
        var empty = CreatePdfPages(new([]));
        var headerOnly = CreatePdfPages(new([PdfTableRow.FromText("A", "B")]) { HeaderRows = LongRows });

        await Assert.That(empty).IsEqualTo(0);
        await Assert.That(headerOnly).IsEqualTo(1);
    }

    /// <summary>A cancelled token stops the layout.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsTheLayout()
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var threw = false;
        try
        {
            _ = PdfTextFlow.Add(creator, [new PdfFlowTable([PdfTableRow.FromText("A")])], CreateOptions(font), source.Token);
        }
        catch (OperationCanceledException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>Makes the options of the test pages.</summary>
    /// <param name="font">The body font.</param>
    /// <returns>The options.</returns>
    private static PdfFlowOptions CreateOptions(PdfCanvasFont font) => new(font) { PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin, BodySize = BodySize };

    /// <summary>Starts a document for the tests.</summary>
    /// <param name="tagged">Whether it is tagged and claims PDF/UA-2.</param>
    /// <returns>The document.</returns>
    private static PdfDocumentCreator CreateCreator(bool tagged) =>
        tagged ? new(new() { Title = "Report", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 }) : new(PdfDocumentCreatorOptions.Default);

    /// <summary>Makes a flow with one table.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The blocks.</returns>
    private static PdfFlowBlock[] CreateFlow(PdfFlowTable table) => [table];

    /// <summary>Makes a small solid image.</summary>
    /// <returns>The pixels.</returns>
    private static PdfRasterImage CreateSquare()
    {
        var pixels = new byte[ImageSize * ImageSize * BgraBytes];
        Array.Fill(pixels, byte.MaxValue);
        return new(PdfRasterFormat.Bgra32, ImageSize, ImageSize, pixels);
    }

    /// <summary>Lays blocks out in a new untagged document and returns the file.</summary>
    /// <param name="table">The table.</param>
    /// <param name="pages">Receives the number of pages.</param>
    /// <returns>The file.</returns>
    private static byte[] CreatePdf(PdfFlowTable table, out int pages)
    {
        var creator = CreateCreator(tagged: false);
        pages = PdfTextFlow.Add(creator, CreateFlow(table), CreateOptions(creator.EmbedFont(CreationTestKit.OpenFont())), CancellationToken.None);
        var pdf = creator.ToArray();
        CreatedFiles.Keep("flow-table.pdf", pdf);
        return pdf;
    }

    /// <summary>Lays a table out and returns the number of pages.</summary>
    /// <param name="table">The table.</param>
    /// <returns>The number of pages.</returns>
    private static int CreatePdfPages(PdfFlowTable table)
    {
        _ = CreatePdf(table, out var pages);
        return pages;
    }

    /// <summary>Lays blocks out and renders the first page.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <param name="options">Makes the options from the body font.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderFirstPage(PdfFlowBlock[] blocks, Func<PdfCanvasFont, PdfFlowOptions> options)
    {
        var creator = CreateCreator(tagged: false);
        _ = PdfTextFlow.Add(creator, blocks, options(creator.EmbedFont(CreationTestKit.OpenFont())), CancellationToken.None);
        var pdf = creator.ToArray();
        CreatedFiles.Keep("flow-table-first.pdf", pdf);
        return RenderAt(pdf, 0);
    }

    /// <summary>Renders a page at one pixel per point.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="index">The zero-based page.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderAt(byte[] pdf, int index)
    {
        using var page = new RenderTestPage(pdf);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(page.Document, index), 0, 1F, out var width, out var height);
        return page.RenderTile(new(index, 1F, 0, 0, 0, PdfRenderFlags.None), width, height);
    }

    /// <summary>Reads the text of the first pages of a document.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="pages">The number of pages.</param>
    /// <returns>The text of each page.</returns>
    private static async Task<string[]> ReadPagesAsync(byte[] pdf, int pages)
    {
        var texts = new string[pages];
        for (var p = 0; p < pages; p++)
        {
            texts[p] = await CreatedFiles.TextOfAsync(pdf, p);
        }

        return texts;
    }

    /// <summary>Counts how many times a text occurs in another.</summary>
    /// <param name="text">The text searched.</param>
    /// <param name="word">The word looked for.</param>
    /// <returns>The count.</returns>
    private static int CountOf(string text, string word) => text.Split(word).Length - 1;

    /// <summary>Counts the elements of a type under an element, including itself.</summary>
    /// <param name="element">The element.</param>
    /// <param name="type">The raw structure type.</param>
    /// <returns>The count.</returns>
    private static int CountType(PdfStructureElement element, string type)
    {
        var count = element.RawType == type ? 1 : 0;
        foreach (var kid in element.Kids)
        {
            count += kid.Element is { } child ? CountType(child, type) : 0;
        }

        return count;
    }

    /// <summary>Determines whether a pixel is ink.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row from the top.</param>
    /// <returns><see langword="true"/> when every channel is low.</returns>
    private static bool IsDark(RenderedImage image, int x, int y)
    {
        var pixel = CreationTestKit.PixelAt(image, x, y);
        return pixel.Red < Dark && pixel.Green < Dark && pixel.Blue < Dark;
    }

    /// <summary>Finds the left edge of each run of ink along a row.</summary>
    /// <param name="image">The render.</param>
    /// <param name="y">The row from the top.</param>
    /// <returns>The first column of each run.</returns>
    private static List<int> FindEdges(RenderedImage image, int y)
    {
        var edges = new List<int>();
        var inside = false;
        for (var x = 0; x < image.Width; x++)
        {
            var dark = IsDark(image, x, y);
            if (dark && !inside)
            {
                edges.Add(x);
            }

            inside = dark;
        }

        return edges;
    }

    /// <summary>Finds the lowest row with ink in a column.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <returns>The row from the top.</returns>
    private static int LastDarkRow(RenderedImage image, int x)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            if (IsDark(image, x, y))
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>Finds the columns of the leftmost and rightmost ink in a region.</summary>
    /// <param name="image">The render.</param>
    /// <param name="left">The first column to look at.</param>
    /// <param name="right">The last column to look at.</param>
    /// <param name="top">The first row to look at.</param>
    /// <param name="bottom">The last row to look at.</param>
    /// <returns>The first and last columns with ink, or -1 for both when there is none.</returns>
    private static InkSpan FindInk(RenderedImage image, int left, int right, int top, int bottom)
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

    /// <summary>Determines whether a band of rows has no ink at all.</summary>
    /// <param name="image">The render.</param>
    /// <param name="top">The first row.</param>
    /// <param name="bottom">The index after the last row.</param>
    /// <returns><see langword="true"/> when the band is clear.</returns>
    private static bool IsBlank(RenderedImage image, int top, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Red < byte.MaxValue || pixel.Green < byte.MaxValue || pixel.Blue < byte.MaxValue)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The columns of the leftmost and rightmost ink in a region.</summary>
    /// <param name="First">The first column with ink.</param>
    /// <param name="Last">The last column with ink.</param>
    private readonly record struct InkSpan(int First, int Last);
}
