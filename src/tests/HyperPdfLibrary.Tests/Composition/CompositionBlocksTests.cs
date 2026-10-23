// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Barcodes;
using HyperPdfLibrary.Charts;
using HyperPdfLibrary.Composition;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Composition;

/// <summary>Checks tables, frames, charts and barcodes as blocks of a composition.</summary>
public sealed class CompositionBlocksTests
{
    /// <summary>The height of a page that holds a probe, so nothing breaks.</summary>
    private const float TallPage = 2000F;

    /// <summary>The height of a small page that holds only a few table rows.</summary>
    private const float SmallPage = 160F;

    /// <summary>How far a measured position may be from the exact one.</summary>
    private const float Tolerance = 0.6F;

    /// <summary>The width of the fixed column in the column test.</summary>
    private const float FixedColumn = 100F;

    /// <summary>The weight of the narrow star column.</summary>
    private const float NarrowWeight = 1F;

    /// <summary>The weight of the wide star column.</summary>
    private const float WideWeight = 3F;

    /// <summary>The padding of the cells in the tests, on each side.</summary>
    private const float CellPadding = 5F;

    /// <summary>The number of rows of the long table.</summary>
    private const int LongRows = 20;

    /// <summary>The width of a frame.</summary>
    private const float FrameWidth = 100F;

    /// <summary>The left edge of the absolute frame.</summary>
    private const float AbsoluteLeft = 200F;

    /// <summary>The height of the chart block.</summary>
    private const float ChartHeight = 150F;

    /// <summary>The side of the barcode block.</summary>
    private const float BarcodeSide = 80F;

    /// <summary>The text of the header cell.</summary>
    private const string HeaderText = "Region";

    /// <summary>The rows a cell spans in the span test, and the figures in the figure test.</summary>
    private const int TwoRows = 2;

    /// <summary>The width the star columns share: the text area less the fixed column.</summary>
    private const float FreeWidth = CompositionKit.PageWidth - (2F * CompositionKit.Margin) - FixedColumn;

    /// <summary>The categories of the test chart.</summary>
    private static readonly string[] ChartCategories = ["A", "B"];

    /// <summary>The values of the test chart's series.</summary>
    private static readonly double[] ChartValues = [1D, 2D];

    /// <summary>Star columns share what the fixed columns leave in proportion to their weights.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColumnsShareTheWidth()
    {
        var grid = new PdfGrid([PdfGridColumn.Fixed(FixedColumn), PdfGridColumn.Star(NarrowWeight), PdfGridColumn.Star(WideWeight)], [PdfGridRow.Of("a", "b", "c")]);
        var texts = CompositionKit.TextOf(Layout(grid).Pages[0]);

        await Assert.That(texts[0].X).IsEqualTo(CompositionKit.Margin + CellPadding).Within(Tolerance);
        await Assert.That(texts[1].X).IsEqualTo(CompositionKit.Margin + FixedColumn + CellPadding).Within(Tolerance);
        await Assert.That(texts[2].X).IsEqualTo(CompositionKit.Margin + FixedColumn + (FreeWidth * NarrowWeight / (NarrowWeight + WideWeight)) + CellPadding).Within(Tolerance);
    }

    /// <summary>Header rows use the bold table header style and repeat as artifacts at the top of the next page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderRowsRepeat()
    {
        var rows = new List<PdfGridRow> { PdfGridRow.Of(HeaderText, "Value") };
        for (var i = 0; i < LongRows; i++)
        {
            rows.Add(PdfGridRow.Of($"row {i}", "x"));
        }

        var grid = new PdfGrid([PdfGridColumn.Star(1F), PdfGridColumn.Star(1F)], rows) { HeaderRows = 1 };
        var layout = CompositionKit.Layout(Compose(CompositionKit.CreateSetup(SmallPage), grid));
        var first = CompositionKit.TextOf(layout.Pages[0])[0];
        var second = CompositionKit.TextOf(layout.Pages[1])[0];

        await Assert.That(layout.Pages.Count).IsGreaterThan(1);
        await Assert.That(first.Text).IsEqualTo(HeaderText);
        await Assert.That(second.Text).IsEqualTo(HeaderText);
        await Assert.That(second.Node).IsNull();
        await Assert.That(second.Artifact).IsEqualTo(PdfArtifactKind.Pagination);
    }

    /// <summary>A cell that spans rows pushes the cells of the rows below to the next free column.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RowSpansCoverCellsBelow()
    {
        var tall = new PdfGridCell([PdfParagraph.Of("tall")]) { RowSpan = TwoRows };
        var rows = new PdfGridRow[] { new([tall, PdfGridCell.Of("top")]), PdfGridRow.Of("below") };
        var grid = new PdfGrid([PdfGridColumn.Fixed(FixedColumn), PdfGridColumn.Fixed(FixedColumn)], rows);

        var texts = CompositionKit.TextOf(Layout(grid).Pages[0]);
        var below = texts.First(static t => t.Text == "below");

        await Assert.That(below.X).IsEqualTo(CompositionKit.Margin + FixedColumn + CellPadding).Within(Tolerance);
    }

    /// <summary>Middle alignment centres the content of a cell between its top and bottom.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VerticalAlignmentMovesContent()
    {
        var tall = new PdfGridCell([PdfParagraph.Of("a\nb\nc\nd")]);
        var top = new PdfGridCell([PdfParagraph.Of("top")]);
        var middle = new PdfGridCell([PdfParagraph.Of("middle")]) { VerticalAlignment = PdfVerticalAlignment.Middle };
        var grid = new PdfGrid([PdfGridColumn.Fixed(FixedColumn), PdfGridColumn.Fixed(FixedColumn), PdfGridColumn.Fixed(FixedColumn)], [new PdfGridRow([tall, top, middle])]);

        var texts = CompositionKit.TextOf(Layout(grid).Pages[0]);

        await Assert.That(texts.First(static t => t.Text == "middle").Y).IsLessThan(texts.First(static t => t.Text == "top").Y);
    }

    /// <summary>A left frame makes the text that follows start to its right.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FloatingFramesMakeRoom()
    {
        var frame = new PdfFrame([PdfParagraph.Of("side")]) { Wrap = PdfFrameWrap.Left, Width = FrameWidth };

        var texts = CompositionKit.TextOf(Layout(frame, PdfParagraph.Of("body")).Pages[0]);
        var body = texts.First(static t => t.Text == "body");

        await Assert.That(body.X).IsGreaterThanOrEqualTo(CompositionKit.Margin + FrameWidth);
    }

    /// <summary>A frame positioned on the page is drawn at that place whatever comes before it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AbsoluteFramesIgnoreTheFlow()
    {
        var frame = new PdfFrame([PdfParagraph.Of("stamp")]) { Position = PdfFramePosition.Page, Left = AbsoluteLeft, Top = 0F, Wrap = PdfFrameWrap.None, Width = FrameWidth };

        var texts = CompositionKit.TextOf(Layout(PdfParagraph.Of("body"), frame).Pages[0]);

        await Assert.That(texts.First(static t => t.Text == "stamp").X).IsEqualTo(AbsoluteLeft).Within(Tolerance);
        await Assert.That(texts.First(static t => t.Text == "body").X).IsEqualTo(CompositionKit.Margin).Within(Tolerance);
    }

    /// <summary>Chart and barcode blocks are drawn at their requested size and described in a tagged document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiguresAreDrawnAndDescribed()
    {
        var chart = new PdfChart(PdfChartKind.Column, "Chart", ChartCategories, new PdfChartSeries[] { new("S", ChartValues) });
        var creator = CompositionKit.CreateCreator(true);
        var qr = new PdfBarcodeBlock(PdfBarcode.CreateQr("hello", PdfQrErrorCorrection.Low), BarcodeSide, BarcodeSide) { AlternativeText = "A greeting" };
        var section = new PdfSection([new PdfChartBlock(chart, ChartHeight), qr]) { Setup = CompositionKit.CreateSetup(TallPage) };
        var composition = new PdfComposition(CompositionKit.CreateStyles(creator), [section]);

        var layout = CompositionKit.Layout(composition);
        var charts = layout.Pages[0].Commands.OfType<ChartCommand>().ToList();
        var barcodes = layout.Pages[0].Commands.OfType<BarcodeCommand>().ToList();
        _ = await PdfComposer.ComposeAsync(creator, composition, null, CancellationToken.None);
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var figures = new List<PdfStructureElement>();
        foreach (var root in PdfDocumentTagged.GetStructureTree(document)!.Roots)
        {
            CollectFigures(root, figures);
        }

        await Assert.That(charts.Count).IsEqualTo(1);
        await Assert.That(charts[0].Box.Height).IsEqualTo(ChartHeight).Within(Tolerance);
        await Assert.That(barcodes.Count).IsEqualTo(1);
        await Assert.That(barcodes[0].Box.Width).IsEqualTo(BarcodeSide).Within(Tolerance);
        await Assert.That(figures.Count).IsEqualTo(TwoRows);
        await Assert.That(figures.Exists(static f => f.AlternateText == "A greeting")).IsTrue();
        await Assert.That(figures.Exists(static f => f.AlternateText is { } text && text.StartsWith("Column chart", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>A table in a tagged document has a table, rows, header cells and data cells.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TablesAreTagged()
    {
        var creator = CompositionKit.CreateCreator(true);
        var grid = new PdfGrid([PdfGridColumn.Star(1F), PdfGridColumn.Star(1F)], [PdfGridRow.Of(HeaderText, "Value"), PdfGridRow.Of("a", "b")]) { HeaderRows = 1, Summary = "A table" };
        var composition = new PdfComposition(CompositionKit.CreateStyles(creator), [new PdfSection([grid]) { Setup = CompositionKit.CreateSetup(TallPage) }]);

        _ = await PdfComposer.ComposeAsync(creator, composition, null, CancellationToken.None);
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var types = new HashSet<string>(StringComparer.Ordinal);
        foreach (var root in PdfDocumentTagged.GetStructureTree(document)!.Roots)
        {
            CollectTypes(root, types);
        }

        await Assert.That(types.IsSupersetOf(["Table", "TR", "TH", "TD"])).IsTrue();
    }

    /// <summary>Custom document information keys are written, and a standard key is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomInformationKeysAreWritten()
    {
        var options = new PdfDocumentCreatorOptions { CustomInfo = new Dictionary<string, string> { ["CaseNumber"] = "A-1234" } };
        var creator = new PdfDocumentCreator(options);
        _ = creator.BeginPage(1F, 1F).Finish();

        var text = Encoding.Latin1.GetString(creator.ToArray());

        await Assert.That(text).Contains("/CaseNumber");
        await Assert.That(static () => new PdfDocumentCreator(new() { CustomInfo = new Dictionary<string, string> { ["Title"] = "x" } }).ToArray()).Throws<ArgumentException>();
    }

    /// <summary>Lays out blocks on a tall page.</summary>
    /// <param name="blocks">The blocks.</param>
    /// <returns>The layout.</returns>
    private static DocumentLayout Layout(params PdfBlock[] blocks) => CompositionKit.Layout(Compose(CompositionKit.CreateSetup(TallPage), blocks));

    /// <summary>Makes a composition of one section.</summary>
    /// <param name="setup">The page setup.</param>
    /// <param name="blocks">The blocks.</param>
    /// <returns>The composition.</returns>
    private static PdfComposition Compose(PdfPageSetup setup, params PdfBlock[] blocks)
    {
        var creator = CompositionKit.CreateCreator(false);
        return new(CompositionKit.CreateStyles(creator), [new PdfSection(blocks) { Setup = setup }]);
    }

    /// <summary>Collects the figure elements below an element.</summary>
    /// <param name="element">The element.</param>
    /// <param name="figures">Receives the figures.</param>
    private static void CollectFigures(PdfStructureElement element, List<PdfStructureElement> figures)
    {
        if (element.RawType == "Figure")
        {
            figures.Add(element);
        }

        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                CollectFigures(child, figures);
            }
        }
    }

    /// <summary>Collects the structure types of an element and everything below it.</summary>
    /// <param name="element">The element.</param>
    /// <param name="types">Receives the types.</param>
    private static void CollectTypes(PdfStructureElement element, HashSet<string> types)
    {
        _ = types.Add(element.RawType);
        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                CollectTypes(child, types);
            }
        }
    }
}
