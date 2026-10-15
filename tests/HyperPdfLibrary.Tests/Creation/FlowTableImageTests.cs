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

/// <summary>Inline images inside flow table cells (<see cref="PdfTableCell"/> with <see cref="PdfFlowImage"/>): scaling, aspect ratio, alignment, mixed text+image cells, pagination and tagging.</summary>
public sealed class FlowTableImageTests
{
    /// <summary>The width of the test pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test pages.</summary>
    private const float PageHeight = 400F;

    /// <summary>The page margin.</summary>
    private const float Margin = 20F;

    /// <summary>The body size of the test text.</summary>
    private const float BodySize = 11F;

    /// <summary>The width of the table border.</summary>
    private const float BorderWidth = 2F;

    /// <summary>The width of a narrow column that constrains a wider image.</summary>
    private const float NarrowColWidth = 80F;

    /// <summary>The width of a wide column used to test horizontal alignment.</summary>
    private const float WideColWidth = 160F;

    /// <summary>A requested image width larger than the narrow column.</summary>
    private const float OversizedImagePoints = 140F;

    /// <summary>A small requested image width inside the wide column.</summary>
    private const float SmallImagePoints = 24F;

    /// <summary>The pixel width of the test raster image.</summary>
    private const int RasterWidth = 16;

    /// <summary>The pixel height of the test raster image.</summary>
    private const int RasterHeight = 8;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the red channel in a BGRA pixel.</summary>
    private const int BgraRedOffset = 2;

    /// <summary>The offset of the alpha channel in a BGRA pixel.</summary>
    private const int BgraAlphaOffset = 3;

    /// <summary>The left edge of the table in pixels.</summary>
    private const int TableLeft = 20;

    /// <summary>The right edge of the narrow first column in pixels.</summary>
    private const int NarrowColRight = 100;

    /// <summary>The right edge of the wide first column in pixels.</summary>
    private const int WideColRight = 180;

    /// <summary>An inset inside the cell away from its borders.</summary>
    private const int CellInset = 3;

    /// <summary>The top row to scan for the image in the first row.</summary>
    private const int FirstRowTop = 23;

    /// <summary>The bottom row to scan for the image in the first row.</summary>
    private const int FirstRowBottom = 90;

    /// <summary>The threshold above which the red channel counts as bright.</summary>
    private const int BrightRed = 180;

    /// <summary>The threshold below which green and blue channels count as dark.</summary>
    private const int DarkOther = 80;

    /// <summary>The threshold below which all channels count as dark border ink.</summary>
    private const int DarkInk = 128;

    /// <summary>The number of image rows in the multi-page table.</summary>
    private const int MultiPageImageRows = 12;

    /// <summary>A wide image scales down to the cell's inner width while preserving aspect ratio and growing the row height.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImageScalesToCellWidthAndGrowsRowHeight(CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var createdImage = creator.AddRaster(CreateRedRectangle(), PdfImageOptions.Default, cancellationToken);
        var flowImage = new PdfFlowImage(createdImage, "Red rectangle", OversizedImagePoints);

        var table = new PdfFlowTable(
        [
            new PdfTableRow([new PdfTableCell(flowImage), new PdfTableCell("Short")]),
        ])
        {
            HeaderRows = 0,
            ColumnWidths = [NarrowColWidth, NarrowColWidth],
            Border = PdfStroke.Black(BorderWidth),
        };

        _ = PdfTextFlow.Add(creator, [table], CreateOptions(font), cancellationToken);
        var render = RenderAt(creator.ToArray(), 0);

        var redBounds = FindRedBounds(render, TableLeft + CellInset, NarrowColRight - CellInset, FirstRowTop, FirstRowBottom);
        var renderedWidth = redBounds.Right - redBounds.Left + 1;
        var renderedHeight = redBounds.Bottom - redBounds.Top + 1;

        await Assert.That(redBounds.Left).IsGreaterThanOrEqualTo(TableLeft + CellInset);
        await Assert.That(redBounds.Right).IsLessThan(NarrowColRight);
        await Assert.That(renderedWidth).IsGreaterThan(renderedHeight);
        await Assert.That(LastDarkRow(render, TableLeft)).IsGreaterThan(redBounds.Bottom);
    }

    /// <summary>Left, centre and right cell alignment position an inline image horizontally inside a wider column.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task InlineImageRespectsHorizontalCellAlignment(CancellationToken cancellationToken)
    {
        var leftX = RenderAlignedImageLeftEdge(PdfTableAlignment.Left, cancellationToken);
        var centerX = RenderAlignedImageLeftEdge(PdfTableAlignment.Center, cancellationToken);
        var rightX = RenderAlignedImageLeftEdge(PdfTableAlignment.Right, cancellationToken);

        await Assert.That(leftX).IsGreaterThan(TableLeft);
        await Assert.That(centerX).IsGreaterThan(leftX);
        await Assert.That(rightX).IsGreaterThan(centerX);
    }

    /// <summary>A cell with both an inline image and text renders both, paginates cleanly across pages and emits tagged <c>Figure</c> elements in PDF/UA-2.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedImageAndTextCellPaginatesAndTagsFigures(CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: true);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var createdImage = creator.AddRaster(CreateRedRectangle(), PdfImageOptions.Default, cancellationToken);

        var rows = new List<PdfTableRow> { PdfTableRow.FromText("Item", "Preview") };
        for (var i = 0; i < MultiPageImageRows; i++)
        {
            var img = new PdfFlowImage(createdImage, $"Diagram {i:D2}", SmallImagePoints);
            rows.Add(new([new PdfTableCell($"Row{i:D2}"), new PdfTableCell(img, $"Caption{i:D2}")]));
        }

        var table = new PdfFlowTable(rows)
        {
            HeaderRows = 1,
            ColumnWidths = [NarrowColWidth, WideColWidth],
            Border = PdfStroke.Black(1F),
        };

        var pages = PdfTextFlow.Add(creator, [new PdfFlowHeading(1, "Visual Table"), table], CreateOptions(font), cancellationToken);
        var report = creator.Finish();
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var firstPageText = await CreatedFiles.TextOfAsync(pdf, 0);
        var lastPageText = await CreatedFiles.TextOfAsync(pdf, pages - 1);

        await Assert.That(pages).IsGreaterThan(1);
        await Assert.That(CountType(root, "Figure")).IsEqualTo(MultiPageImageRows);
        await Assert.That(firstPageText.Contains("Caption00", StringComparison.Ordinal)).IsTrue();
        await Assert.That(lastPageText.Contains($"Caption{MultiPageImageRows - 1:D2}", StringComparison.Ordinal)).IsTrue();
        await Assert.That(accessibility.Findings.Select(static f => f.Code).ToArray()).IsEmpty();
        await Assert.That(report.ClaimsPdfUa).IsTrue();
    }

    /// <summary>Renders a single-cell table with an aligned inline image and returns the leftmost red pixel column.</summary>
    /// <param name="alignment">The cell alignment.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The first red column.</returns>
    private static int RenderAlignedImageLeftEdge(PdfTableAlignment alignment, CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var createdImage = creator.AddRaster(CreateRedRectangle(), PdfImageOptions.Default, cancellationToken);
        var flowImage = new PdfFlowImage(createdImage, "Icon", SmallImagePoints);
        var cell = new PdfTableCell(flowImage) { Alignment = alignment };
        var table = new PdfFlowTable([new PdfTableRow([cell])])
        {
            HeaderRows = 0,
            ColumnWidths = [WideColWidth],
            Border = PdfStroke.Black(BorderWidth),
        };

        _ = PdfTextFlow.Add(creator, [table], CreateOptions(font), cancellationToken);
        var render = RenderAt(creator.ToArray(), 0);
        return FindRedBounds(render, TableLeft + CellInset, WideColRight - CellInset, FirstRowTop, FirstRowBottom).Left;
    }

    /// <summary>Creates a solid red 16x8 raster image.</summary>
    /// <returns>The raster image.</returns>
    private static PdfRasterImage CreateRedRectangle()
    {
        var pixels = new byte[RasterWidth * RasterHeight * BgraBytes];
        for (var i = 0; i < pixels.Length; i += BgraBytes)
        {
            pixels[i + BgraRedOffset] = byte.MaxValue;
            pixels[i + BgraAlphaOffset] = byte.MaxValue;
        }

        return new(PdfRasterFormat.Bgra32, RasterWidth, RasterHeight, pixels);
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
        tagged ? new(new() { Title = "Images", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 }) : new(PdfDocumentCreatorOptions.Default);

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

    /// <summary>Finds the bounding box of bright red pixels in a region.</summary>
    /// <param name="image">The render.</param>
    /// <param name="left">The left column.</param>
    /// <param name="right">The right column.</param>
    /// <param name="top">The top row.</param>
    /// <param name="bottom">The bottom row.</param>
    /// <returns>The bounding box of red pixels.</returns>
    private static PixelBox FindRedBounds(RenderedImage image, int left, int right, int top, int bottom)
    {
        var minX = int.MaxValue;
        var maxX = -1;
        var minY = int.MaxValue;
        var maxY = -1;
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Red <= BrightRed || pixel.Green >= DarkOther || pixel.Blue >= DarkOther)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        return new(minX, maxX, minY, maxY);
    }

    /// <summary>Finds the lowest row with dark border ink in a column.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <returns>The row from the top.</returns>
    private static int LastDarkRow(RenderedImage image, int x)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            var pixel = CreationTestKit.PixelAt(image, x, y);
            if (pixel.Red < DarkInk && pixel.Green < DarkInk && pixel.Blue < DarkInk)
            {
                return y;
            }
        }

        return -1;
    }

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

    /// <summary>The pixel bounding box of a colour region.</summary>
    /// <param name="Left">The leftmost column.</param>
    /// <param name="Right">The rightmost column.</param>
    /// <param name="Top">The topmost row.</param>
    /// <param name="Bottom">The bottommost row.</param>
    private readonly record struct PixelBox(int Left, int Right, int Top, int Bottom);
}
