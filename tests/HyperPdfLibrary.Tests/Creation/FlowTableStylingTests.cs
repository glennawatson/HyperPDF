// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Per-cell fonts, font sizes, colours, backgrounds, table defaults and rich text runs (<see cref="PdfTextRun"/>) in flow tables.</summary>
public sealed class FlowTableStylingTests
{
    /// <summary>The width of the test pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test pages.</summary>
    private const float PageHeight = 400F;

    /// <summary>The page margin.</summary>
    private const float Margin = 20F;

    /// <summary>The body size of the test text.</summary>
    private const float BodySize = 11F;

    /// <summary>A larger font size for a styled cell.</summary>
    private const float LargeFontSize = 22F;

    /// <summary>The width of the borders.</summary>
    private const float BorderWidth = 2F;

    /// <summary>The width of the first column.</summary>
    private const float FirstWidth = 120F;

    /// <summary>The width of the second column.</summary>
    private const float SecondWidth = 120F;

    /// <summary>The left edge of the table in pixels.</summary>
    private const int TableLeft = 20;

    /// <summary>The dividing border between the two columns in pixels.</summary>
    private const int ColumnDivider = 140;

    /// <summary>The right edge of the two-column table in pixels.</summary>
    private const int TableRight = 260;

    /// <summary>An inset inside a cell away from its borders.</summary>
    private const int CellInset = 4;

    /// <summary>A sample row near the top of the first cell's background.</summary>
    private const int BackgroundSampleY = 23;

    /// <summary>A sample column inside the first cell.</summary>
    private const int FirstCellSampleX = 25;

    /// <summary>The first pixel row to scan for text in the first row.</summary>
    private const int FirstRowScanTop = 24;

    /// <summary>The last pixel row to scan for text in the first row.</summary>
    private const int FirstRowScanBottom = 60;

    /// <summary>The threshold above which a colour channel counts as bright.</summary>
    private const int BrightChannel = 180;

    /// <summary>The threshold below which a colour channel counts as dark.</summary>
    private const int DarkChannel = 80;

    /// <summary>The red channel of the light yellow cell background.</summary>
    private const float FillRed = 1F;

    /// <summary>The green channel of the light yellow cell background.</summary>
    private const float FillGreen = 0.95F;

    /// <summary>The blue channel of the light yellow cell background.</summary>
    private const float FillBlue = 0.6F;

    /// <summary>The maximum blue channel expected on the yellow background.</summary>
    private const int YellowMaxBlue = 180;

    /// <summary>The number of rows in the multi-page rich-text table.</summary>
    private const int MultiPageRows = 35;

    /// <summary>The number of runs repeated in the wrapping rich-text cell.</summary>
    private const int RepeatedRuns = 6;

    /// <summary>An invalid negative font size.</summary>
    private const float InvalidNegativeFontSize = -5F;

    /// <summary>An invalid colour channel above one.</summary>
    private const float InvalidColorChannel = 1.5F;

    /// <summary>The family name of the synthetic bold font.</summary>
    private const string BoldFamily = "PVL Test Bold";

    /// <summary>Table defaults and per-cell overrides for font, font size, text colour and background colour render with the expected height and colours.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PerCellFontSizeAndColoursRenderAndGrowRowHeight()
    {
        var smallCreator = CreateCreator(tagged: false);
        var smallFont = smallCreator.EmbedFont(CreationTestKit.OpenFont());
        var smallTable = new PdfFlowTable([PdfTableRow.FromText("Normal", "Row")]) { HeaderRows = 0, ColumnWidths = [FirstWidth, SecondWidth], Border = PdfStroke.Black(BorderWidth) };
        _ = PdfTextFlow.Add(smallCreator, [smallTable], CreateOptions(smallFont), CancellationToken.None);
        var smallRender = RenderAt(smallCreator.ToArray(), 0);
        var smallBottom = LastDarkRow(smallRender, TableLeft);

        var styledCreator = CreateCreator(tagged: false);
        var bodyFont = styledCreator.EmbedFont(CreationTestKit.OpenFont());
        var boldFont = styledCreator.EmbedFont(OpenCustomFont(BoldFamily, bold: true));
        var yellow = new PdfRgbColor(FillRed, FillGreen, FillBlue);
        var red = new PdfRgbColor(1F, 0F, 0F);

        var styledRow = new PdfTableRow(
        [
            new PdfTableCell("BigRed") { Font = boldFont, FontSize = LargeFontSize, TextColor = red, BackgroundColor = yellow },
            new("DefaultBody"),
        ]);
        var styledTable = new PdfFlowTable([styledRow]) { HeaderRows = 0, ColumnWidths = [FirstWidth, SecondWidth], Border = PdfStroke.Black(BorderWidth), BodyFont = bodyFont, FontSize = BodySize };
        _ = PdfTextFlow.Add(styledCreator, [styledTable], CreateOptions(bodyFont), CancellationToken.None);
        var styledPdf = styledCreator.ToArray();
        var styledRender = RenderAt(styledPdf, 0);
        var styledBottom = LastDarkRow(styledRender, TableLeft);

        var fillPixel = CreationTestKit.PixelAt(styledRender, FirstCellSampleX, BackgroundSampleY);
        var text = await CreatedFiles.TextOfAsync(styledPdf, 0);

        await Assert.That(styledBottom).IsGreaterThan(smallBottom);
        await Assert.That((int)fillPixel.Red).IsGreaterThan(BrightChannel);
        await Assert.That((int)fillPixel.Green).IsGreaterThan(BrightChannel);
        await Assert.That((int)fillPixel.Blue).IsLessThan(YellowMaxBlue);
        await Assert.That(HasDominantRedPixel(styledRender, TableLeft + CellInset, ColumnDivider - CellInset, FirstRowScanTop, FirstRowScanBottom)).IsTrue();
        await Assert.That(text.Contains("BigRed", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("DefaultBody", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>A cell with multiple <see cref="PdfTextRun"/> segments renders mixed fonts, sizes and colours on a shared line.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichTextRunsRenderMixedFontsSizesAndColors()
    {
        var creator = CreateCreator(tagged: false);
        var regularFont = creator.EmbedFont(CreationTestKit.OpenFont());
        var boldFont = creator.EmbedFont(OpenCustomFont(BoldFamily, bold: true));
        var red = new PdfRgbColor(1F, 0F, 0F);
        var blue = new PdfRgbColor(0F, 0F, 1F);

        var richCell = PdfTableCell.FromRuns(
            new PdfTextRun("Red ") { Font = regularFont, Color = red },
            new PdfTextRun("Blue") { Font = boldFont, FontSize = LargeFontSize, Color = blue });

        var table = new PdfFlowTable([new PdfTableRow([richCell, new("Side")])]) { HeaderRows = 0, ColumnWidths = [FirstWidth, SecondWidth], Border = PdfStroke.Black(BorderWidth) };

        _ = PdfTextFlow.Add(creator, [table], CreateOptions(regularFont), CancellationToken.None);
        var pdf = creator.ToArray();
        var render = RenderAt(pdf, 0);
        var text = await CreatedFiles.TextOfAsync(pdf, 0);

        var redX = FirstDominantRedColumn(render, TableLeft + CellInset, ColumnDivider - CellInset, FirstRowScanTop, FirstRowScanBottom);
        var blueX = FirstDominantBlueColumn(render, TableLeft + CellInset, ColumnDivider - CellInset, FirstRowScanTop, FirstRowScanBottom);

        await Assert.That(redX).IsGreaterThan(0);
        await Assert.That(blueX).IsGreaterThan(redX);
        await Assert.That(text.Contains("Red", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("Blue", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Multi-run rich text wraps within a narrow column and paginates cleanly across pages in a tagged PDF/UA-2 document.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichTextRunsWrapAndPaginateInTaggedTable(CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: true);
        var regularFont = creator.EmbedFont(CreationTestKit.OpenFont());
        var boldFont = creator.EmbedFont(OpenCustomFont(BoldFamily, bold: true));

        var rows = new List<PdfTableRow> { new([new PdfTableCell("HeaderA") { Font = boldFont }, new PdfTableCell("HeaderB") { Font = boldFont }]) };

        for (var r = 0; r < MultiPageRows; r++)
        {
            var runs = new List<PdfTextRun>(RepeatedRuns);
            for (var i = 0; i < RepeatedRuns; i++)
            {
                runs.Add(new($"R{r:D2}W{i} ") { Font = (i & 1) == 0 ? regularFont : boldFont });
            }

            rows.Add(new([new($"Key{r:D2}"), new(runs)]));
        }

        var table = new PdfFlowTable(rows) { HeaderRows = 1, ColumnWidths = [FirstWidth, SecondWidth], Border = PdfStroke.Black(BorderWidth), HeaderFont = boldFont, BodyFont = regularFont };

        var pages = PdfTextFlow.Add(creator, [new PdfFlowHeading(1, "Styled Table"), table], CreateOptions(regularFont), cancellationToken);
        var report = creator.Finish();
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var firstPageText = await CreatedFiles.TextOfAsync(pdf, 0);
        var lastPageText = await CreatedFiles.TextOfAsync(pdf, pages - 1);

        await Assert.That(pages).IsGreaterThan(1);
        await Assert.That(firstPageText.Contains("R00W0", StringComparison.Ordinal)).IsTrue();
        await Assert.That(lastPageText.Contains($"R{MultiPageRows - 1:D2}W0", StringComparison.Ordinal)).IsTrue();
        await Assert.That(accessibility.Findings.Select(static f => f.Code).ToArray()).IsEmpty();
        await Assert.That(report.ClaimsPdfUa).IsTrue();
    }

    /// <summary>Invalid font sizes, colour channels or null text runs throw the documented exceptions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidStylingParametersThrow()
    {
        var badFontSize = new PdfFlowTable([new PdfTableRow([new PdfTableCell("A") { FontSize = InvalidNegativeFontSize }])]);
        var badColor = new PdfFlowTable([new PdfTableRow([new PdfTableCell("A") { TextColor = new PdfRgbColor(InvalidColorChannel, 0F, 0F) }])]);
        var badRun = new PdfFlowTable([new PdfTableRow([PdfTableCell.FromRuns(new PdfTextRun(null!))])]);

        await Assert.That(() => LayOut(badFontSize)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => LayOut(badColor)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => LayOut(badRun)).ThrowsExactly<ArgumentNullException>();
    }

    /// <summary>Opens a synthetic font face with the given family name and bold flag.</summary>
    /// <param name="family">The family name.</param>
    /// <param name="bold">Whether the face is marked bold.</param>
    /// <returns>The font source.</returns>
    /// <exception cref="InvalidOperationException">The synthetic font is refused.</exception>
    private static PdfFontSource OpenCustomFont(string family, bool bold)
    {
        var result = PdfFontSource.Open(TestFont.Create(0, family, bold), 0);
        return result.Source ?? throw new InvalidOperationException(result.Refusal);
    }

    /// <summary>Lays a table out on a test document.</summary>
    /// <param name="table">The table.</param>
    private static void LayOut(PdfFlowTable table)
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        _ = PdfTextFlow.Add(creator, [table], CreateOptions(font), CancellationToken.None);
    }

    /// <summary>Makes the options of the test pages.</summary>
    /// <param name="font">The body font.</param>
    /// <returns>The options.</returns>
    private static PdfFlowOptions CreateOptions(PdfCanvasFont font) =>
        new(font) { PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin, BodySize = BodySize };

    /// <summary>Starts a document for the tests.</summary>
    /// <param name="tagged">Whether it is tagged and claims PDF/UA-2.</param>
    /// <returns>The document.</returns>
    private static PdfDocumentCreator CreateCreator(bool tagged) =>
        tagged ? new(new() { Title = "Styled", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 }) : new(PdfDocumentCreatorOptions.Default);

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

    /// <summary>Finds the lowest row with dark ink in a column.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <returns>The row from the top.</returns>
    private static int LastDarkRow(RenderedImage image, int x)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            var pixel = CreationTestKit.PixelAt(image, x, y);
            if (pixel.Red < DarkChannel && pixel.Green < DarkChannel && pixel.Blue < DarkChannel)
            {
                return y;
            }
        }

        return -1;
    }

    /// <summary>Determines whether any pixel in a rectangle has bright red and dark green/blue channels.</summary>
    /// <param name="image">The render.</param>
    /// <param name="left">The left column.</param>
    /// <param name="right">The right column.</param>
    /// <param name="top">The top row.</param>
    /// <param name="bottom">The bottom row.</param>
    /// <returns><see langword="true"/> when a red pixel is found.</returns>
    private static bool HasDominantRedPixel(RenderedImage image, int left, int right, int top, int bottom) =>
        FirstDominantRedColumn(image, left, right, top, bottom) >= 0;

    /// <summary>Finds the first column in a rectangle that has a pixel with bright red and dark green/blue channels.</summary>
    /// <param name="image">The render.</param>
    /// <param name="left">The left column.</param>
    /// <param name="right">The right column.</param>
    /// <param name="top">The top row.</param>
    /// <param name="bottom">The bottom row.</param>
    /// <returns>The column index, or -1 when none is found.</returns>
    private static int FirstDominantRedColumn(RenderedImage image, int left, int right, int top, int bottom)
    {
        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Red > BrightChannel && pixel.Green < DarkChannel && pixel.Blue < DarkChannel)
                {
                    return x;
                }
            }
        }

        return -1;
    }

    /// <summary>Finds the first column in a rectangle that has a pixel with bright blue and dark red/green channels.</summary>
    /// <param name="image">The render.</param>
    /// <param name="left">The left column.</param>
    /// <param name="right">The right column.</param>
    /// <param name="top">The top row.</param>
    /// <param name="bottom">The bottom row.</param>
    /// <returns>The column index, or -1 when none is found.</returns>
    private static int FirstDominantBlueColumn(RenderedImage image, int left, int right, int top, int bottom)
    {
        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Blue > BrightChannel && pixel.Red < DarkChannel && pixel.Green < DarkChannel)
                {
                    return x;
                }
            }
        }

        return -1;
    }
}
