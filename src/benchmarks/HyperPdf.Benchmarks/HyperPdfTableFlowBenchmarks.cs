// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures laying a table of wrapping text out over pages. The baseline draws the same grid the way a caller had to before
/// the flow had tables: measure each cell, break pages by hand and draw borders as thin filled rectangles. The change lays it out with
/// <see cref="PdfFlowTable"/>, and the paragraph flow of the same text shows the cost of the text flow itself.
/// </summary>
public class HyperPdfTableFlowBenchmarks
{
    /// <summary>The body rows.</summary>
    private const int Rows = 150;

    /// <summary>The columns.</summary>
    private const int Columns = 4;

    /// <summary>The lines the hand-written grid keeps room for, enough for its longest cell.</summary>
    private const int MaxLines = 8;

    /// <summary>The cell padding.</summary>
    private const float Padding = 4F;

    /// <summary>The padding on both sides of a cell together.</summary>
    private const float Inset = Padding + Padding;

    /// <summary>The rows between status changes.</summary>
    private const int StatusEvery = 2;

    /// <summary>The border width.</summary>
    private const float BorderWidth = 0.5F;

    /// <summary>The width of the first column.</summary>
    private const float FirstColumn = 70F;

    /// <summary>The width of the description column.</summary>
    private const float DescriptionColumn = 190F;

    /// <summary>The width of the number column.</summary>
    private const float NumberColumn = 90F;

    /// <summary>The width of the status column.</summary>
    private const float StatusColumn = 90F;

    /// <summary>One half, for centring.</summary>
    private const float Half = 0.5F;

    /// <summary>The multiplier that makes a number from a row.</summary>
    private const double NumberStep = 3.5;

    /// <summary>The rows between the longer descriptions.</summary>
    private const int LongEvery = 3;

    /// <summary>The cell text that wraps in the description column.</summary>
    private const string LongDescription = "A description of the item that is long enough to wrap onto a second and a third line in its column";

    /// <summary>The cell text that fits on one line.</summary>
    private const string ShortDescription = "A short description";

    /// <summary>The proportional weight of the description column.</summary>
    private const float DescriptionWeight = 3F;

    /// <summary>The text of the header cells.</summary>
    private static readonly string[] HeaderCells = ["Item", "Description", "Amount", "Status"];

    /// <summary>The column widths.</summary>
    private static readonly float[] Widths = [FirstColumn, DescriptionColumn, NumberColumn, StatusColumn];

    /// <summary>The mixed column sizing specification.</summary>
    private static readonly PdfTableColumnWidth[] MixedSizing =
    [
        PdfTableColumnWidth.Fixed(FirstColumn),
        PdfTableColumnWidth.Proportional(DescriptionWeight),
        PdfTableColumnWidth.Auto,
        PdfTableColumnWidth.Fixed(StatusColumn),
    ];

    /// <summary>The text of each row, header first.</summary>
    private string[][] _cells = [];

    /// <summary>The rows of the table block.</summary>
    private PdfTableRow[] _tableRows = [];

    /// <summary>The table block measured by the flow benchmark.</summary>
    private PdfFlowBlock[] _tableBlocks = [];

    /// <summary>The flow blocks with headings, rules, keep-with-next labels, and proportional/available-width table sizing.</summary>
    private PdfFlowBlock[] _sizedFlowBlocks = [];

    /// <summary>The paragraphs of the same text.</summary>
    private PdfFlowBlock[] _paragraphs = [];

    /// <summary>Builds the rows once.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _cells = new string[Rows + 1][];
        _cells[0] = HeaderCells;
        _tableRows = new PdfTableRow[Rows + 1];
        _tableRows[0] = PdfTableRow.FromText(HeaderCells);
        _paragraphs = new PdfFlowBlock[Rows + 1];
        _paragraphs[0] = new PdfFlowParagraph(string.Join(' ', HeaderCells));
        for (var i = 0; i < Rows; i++)
        {
            string[] row =
            [
                $"Item {i}",
                i % LongEvery == 0 ? LongDescription : ShortDescription,
                (i * NumberStep).ToString("F2", CultureInfo.InvariantCulture),
                i % StatusEvery == 0 ? "Open" : "Closed",
            ];
            _cells[i + 1] = row;
            _tableRows[i + 1] = PdfTableRow.FromText(row);
            _paragraphs[i + 1] = new PdfFlowParagraph(string.Join(' ', row));
        }

        _tableBlocks = [new PdfFlowTable(_tableRows) { ColumnWidths = Widths, CellPadding = Padding, Border = PdfStroke.Black(BorderWidth) }];
        _sizedFlowBlocks =
        [
            new PdfFlowHeading(1, "Section Summary"),
            new PdfFlowRule(),
            new PdfFlowParagraph("Lead-in label before table:") { KeepWithNext = true },
            new PdfFlowTable(_tableRows) { FillAvailableWidth = true, ColumnSizing = MixedSizing, CellPadding = Padding, Border = PdfStroke.Black(BorderWidth) },
        ];
    }

    /// <summary>Draws the grid by hand: each cell is measured and drawn by the caller, pages are broken by the caller and borders are thin filled rectangles.</summary>
    /// <returns>The number of pages.</returns>
    [Benchmark(Baseline = true)]
    public int ManualGrid()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.AddBuiltInFont(AppearanceFont.Helvetica);
        var options = new PdfFlowOptions(font);
        var leading = options.BodySize * options.LineSpacing;
        var lines = new List<PdfTextLine>(MaxLines);
        var canvas = default(PdfPageCanvas);
        var pages = 0;
        var top = 0F;
        try
        {
            foreach (var row in _cells)
            {
                var most = MeasureRow(font, options.BodySize, row, lines);
                var height = Inset + (most * leading);
                if (pages == 0 || top - height < options.Margin)
                {
                    if (pages > 0)
                    {
                        _ = canvas.Finish();
                    }

                    canvas.Dispose();
                    canvas = creator.BeginPage(options.PageWidth, options.PageHeight);
                    pages++;
                    top = options.PageHeight - options.Margin;
                }

                DrawRow(ref canvas, font, options, row, top, leading, lines);
                top -= height;
            }

            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        return pages;
    }

    /// <summary>Lays the table out with <see cref="PdfFlowTable"/>.</summary>
    /// <returns>The number of pages.</returns>
    [Benchmark]
    public int TableFlow()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var options = new PdfFlowOptions(creator.AddBuiltInFont(AppearanceFont.Helvetica));
        return PdfTextFlow.Add(creator, _tableBlocks, options, CancellationToken.None);
    }

    /// <summary>Lays out a heading, rule, keep-with-next label, and table with mixed column sizing and available-width fill.</summary>
    /// <returns>The number of pages.</returns>
    [Benchmark]
    public int SizedTableAndRuleFlow()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var options = new PdfFlowOptions(creator.AddBuiltInFont(AppearanceFont.Helvetica));
        return PdfTextFlow.Add(creator, _sizedFlowBlocks, options, CancellationToken.None);
    }

    /// <summary>Lays the same text out as paragraphs, to show what the text flow costs without the table.</summary>
    /// <returns>The number of pages.</returns>
    [Benchmark]
    public int ParagraphFlow()
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var options = new PdfFlowOptions(creator.AddBuiltInFont(AppearanceFont.Helvetica));
        return PdfTextFlow.Add(creator, _paragraphs, options, CancellationToken.None);
    }

    /// <summary>Wraps each cell of a row and finds the most lines.</summary>
    /// <param name="font">The font.</param>
    /// <param name="size">The font size.</param>
    /// <param name="row">The cell text.</param>
    /// <param name="lines">Receives the lines of the last cell.</param>
    /// <returns>The most lines in any cell.</returns>
    private static int MeasureRow(PdfCanvasFont font, float size, string[] row, List<PdfTextLine> lines)
    {
        var most = 1;
        for (var c = 0; c < row.Length; c++)
        {
            lines.Clear();
            PdfTextWrapper.Wrap(font, size, row[c], Widths[c] - Inset, lines);
            most = Math.Max(most, lines.Count);
        }

        return most;
    }

    /// <summary>Draws the text and borders of one row.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="font">The font.</param>
    /// <param name="options">The options.</param>
    /// <param name="row">The cell text.</param>
    /// <param name="top">The top of the row.</param>
    /// <param name="leading">The line pitch.</param>
    /// <param name="lines">Receives the wrapped lines.</param>
    private static void DrawRow(ref PdfPageCanvas canvas, PdfCanvasFont font, PdfFlowOptions options, string[] row, float top, float leading, List<PdfTextLine> lines)
    {
        var left = options.Margin;
        var most = 1;
        for (var c = 0; c < row.Length; c++)
        {
            lines.Clear();
            PdfTextWrapper.Wrap(font, options.BodySize, row[c], Widths[c] - Inset, lines);
            most = Math.Max(most, lines.Count);
            var ascent = font.GetAscent(options.BodySize);
            var offset = ((leading - (ascent - font.GetDescent(options.BodySize))) * Half) + ascent;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Length > 0)
                {
                    canvas.DrawText(font, options.BodySize, left + Padding, top - Padding - (i * leading) - offset, row[c].AsSpan(line.Start, line.Length));
                }
            }

            canvas.FillRectangle(0F, 0F, 0F, left, top - Inset - (lines.Count * leading), BorderWidth, Inset + (lines.Count * leading));
            left += Widths[c];
        }

        var bottom = top - Inset - (most * leading);
        canvas.FillRectangle(0F, 0F, 0F, options.Margin, bottom, left - options.Margin, BorderWidth);
        canvas.FillRectangle(0F, 0F, 0F, left, bottom, BorderWidth, Inset + (most * leading));
    }
}
