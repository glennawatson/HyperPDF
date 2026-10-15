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

/// <summary>Flow code blocks (<see cref="PdfFlowCodeBlock"/>): whitespace preservation, tab expansion, box styling, overflow modes, pagination, table cells and tagging.</summary>
public sealed class FlowCodeBlockTests
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

    /// <summary>The font size of the code block.</summary>
    private const float CodeFontSize = 10F;

    /// <summary>The padding inside the code box.</summary>
    private const float BoxPadding = 6F;

    /// <summary>The left indent of the code box.</summary>
    private const float BoxIndent = 16F;

    /// <summary>The width of the border around the code box.</summary>
    private const float BorderWidth = 2F;

    /// <summary>The left edge of the indented code box in pixels.</summary>
    private const int IndentedLeftEdge = 36;

    /// <summary>A column inside the left margin before the indented box.</summary>
    private const int BeforeIndentedBox = 26;

    /// <summary>A row inside the top padding of the code box.</summary>
    private const int BoxSampleRow = 23;

    /// <summary>A column inside the code box.</summary>
    private const int BoxSampleCol = 42;

    /// <summary>The first pixel row of the first code line.</summary>
    private const int LineOneTop = 24;

    /// <summary>The last pixel row of the first code line.</summary>
    private const int LineOneBottom = 36;

    /// <summary>The first pixel row of the third code line (after a blank second line).</summary>
    private const int LineThreeTop = 48;

    /// <summary>The last pixel row of the third code line.</summary>
    private const int LineThreeBottom = 62;

    /// <summary>The right edge of the content area in pixels.</summary>
    private const int ContentRight = 280;

    /// <summary>The level below which a pixel channel counts as dark ink.</summary>
    private const int Dark = 128;

    /// <summary>The red channel of the code block background.</summary>
    private const float FillRed = 0.92F;

    /// <summary>The green channel of the code block background.</summary>
    private const float FillGreen = 0.94F;

    /// <summary>The blue channel of the code block background.</summary>
    private const float FillBlue = 0.98F;

    /// <summary>The number of spaces per tab in the custom tab test.</summary>
    private const int CustomTabSpaces = 4;

    /// <summary>The number of lines in the multi-page code block.</summary>
    private const int LongCodeLines = 60;

    /// <summary>The number of repeated tokens on a long code line.</summary>
    private const int LongTokenRepeats = 16;

    /// <summary>The pixels at the edge of the page checked for clipping.</summary>
    private const int EdgeGuard = 3;

    /// <summary>The width of the first table column.</summary>
    private const float FirstColWidth = 80F;

    /// <summary>The width of the second table column.</summary>
    private const float SecondColWidth = 180F;

    /// <summary>An invalid negative padding value.</summary>
    private const float InvalidNegativeValue = -2F;

    /// <summary>Leading spaces, tabs and blank lines are preserved in a <see cref="PdfFlowCodeBlock"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodeBlockPreservesIndentationTabsAndBlankLines()
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var code = new PdfFlowCodeBlock("Alpha\n\n\tBeta") { Font = font, FontSize = CodeFontSize, Padding = BoxPadding, Indent = 0F, TabSpaces = CustomTabSpaces };

        _ = PdfTextFlow.Add(creator, [code], CreateOptions(font), CancellationToken.None);
        var pdf = creator.ToArray();
        var render = RenderAt(pdf, 0);
        var text = await CreatedFiles.TextOfAsync(pdf, 0);

        var lineOneFirstX = FirstDarkColumn(render, (int)Margin, ContentRight, LineOneTop, LineOneBottom);
        var lineThreeFirstX = FirstDarkColumn(render, (int)Margin, ContentRight, LineThreeTop, LineThreeBottom);

        await Assert.That(lineOneFirstX).IsGreaterThan(0);
        await Assert.That(lineThreeFirstX).IsGreaterThan(lineOneFirstX);
        await Assert.That(text.Contains("Alpha", StringComparison.Ordinal)).IsTrue();
        await Assert.That(text.Contains("Beta", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Background fill, border, padding and left indent place the code box and its border at the expected coordinates.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodeBlockRendersBackgroundBorderPaddingAndIndent()
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var fill = new PdfRgbColor(FillRed, FillGreen, FillBlue);
        var code = new PdfFlowCodeBlock("int x = 1;") { Font = font, FontSize = CodeFontSize, Padding = BoxPadding, Indent = BoxIndent, BackgroundColor = fill, Border = PdfStroke.Black(BorderWidth) };

        _ = PdfTextFlow.Add(creator, [code], CreateOptions(font), CancellationToken.None);
        var render = RenderAt(creator.ToArray(), 0);
        var fillPixel = CreationTestKit.PixelAt(render, BoxSampleCol, BoxSampleRow);

        await Assert.That(IsDark(render, IndentedLeftEdge, BoxSampleRow)).IsTrue();
        await Assert.That(IsDark(render, BeforeIndentedBox, BoxSampleRow)).IsFalse();
        await Assert.That(fillPixel.Red < byte.MaxValue && fillPixel.Red > Dark).IsTrue();
    }

    /// <summary><see cref="PdfCodeOverflow.Wrap"/>, <see cref="PdfCodeOverflow.WrapCharacters"/> and <see cref="PdfCodeOverflow.Clip"/> handle overlong lines as configured.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodeBlockOverflowModesWrapBreakCharactersAndClip()
    {
        var longLine = string.Concat(Enumerable.Repeat("ABCD1234", LongTokenRepeats));

        var wrappedHeight = MeasureRenderedBottom(longLine, PdfCodeOverflow.Wrap);
        var charWrappedHeight = MeasureRenderedBottom(longLine, PdfCodeOverflow.WrapCharacters);
        var clippedRender = RenderCode(longLine, PdfCodeOverflow.Clip, out var clippedBottom);

        await Assert.That(wrappedHeight).IsGreaterThan(clippedBottom);
        await Assert.That(charWrappedHeight).IsGreaterThan(clippedBottom);
        await Assert.That(FirstDarkColumn(clippedRender, ContentRight + EdgeGuard, (int)PageWidth - 1, 0, PageRows - 1)).IsEqualTo(-1);
    }

    /// <summary>A long code block paginates across multiple pages and a code block inside a table cell is tagged as <c>Code</c> in PDF/UA-2.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodeBlockPaginatesAndWorksInsideTaggedTableCell(CancellationToken cancellationToken)
    {
        var creator = CreateCreator(tagged: true);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var manyLines = string.Join('\n', Enumerable.Range(0, LongCodeLines).Select(static i => $"line_{i:D3} = {i};"));
        var standaloneCode = new PdfFlowCodeBlock(manyLines) { Font = font, FontSize = CodeFontSize, Border = PdfStroke.Black(1F) };
        var cellCode = new PdfFlowCodeBlock("return 0;") { Font = font, FontSize = CodeFontSize };
        var table = new PdfFlowTable([PdfTableRow.FromText("Key", "Code"), new([new("Main"), new(cellCode)])]) { ColumnWidths = [FirstColWidth, SecondColWidth], Border = PdfStroke.Black(1F) };

        var pages = PdfTextFlow.Add(creator, [new PdfFlowHeading(1, "Code Report"), table, standaloneCode], CreateOptions(font), cancellationToken);
        var report = creator.Finish();
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var firstText = await CreatedFiles.TextOfAsync(pdf, 0);
        var lastText = await CreatedFiles.TextOfAsync(pdf, pages - 1);

        await Assert.That(pages).IsGreaterThan(1);
        await Assert.That(CountType(root, "Code")).IsGreaterThanOrEqualTo(1);
        await Assert.That(firstText.Contains("return 0;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(lastText.Contains($"line_{LongCodeLines - 1:D3}", StringComparison.Ordinal)).IsTrue();
        await Assert.That(accessibility.Findings.Select(static f => f.Code).ToArray()).IsEmpty();
        await Assert.That(report.ClaimsPdfUa).IsTrue();
    }

    /// <summary>Invalid code-block options throw <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidCodeBlockOptionsThrow()
    {
        var badPadding = new PdfFlowCodeBlock("x") { Padding = InvalidNegativeValue };
        var badIndent = new PdfFlowCodeBlock("x") { Indent = InvalidNegativeValue };
        var badTabs = new PdfFlowCodeBlock("x") { TabSpaces = -1 };
        var badFontSize = new PdfFlowCodeBlock("x") { FontSize = InvalidNegativeValue };

        await Assert.That(() => LayOut(badPadding)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => LayOut(badIndent)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => LayOut(badTabs)).ThrowsExactly<ArgumentOutOfRangeException>();
        await Assert.That(() => LayOut(badFontSize)).ThrowsExactly<ArgumentOutOfRangeException>();
    }

    /// <summary>Renders a single code block with the given overflow mode and returns the lowest dark pixel row.</summary>
    /// <param name="text">The code text.</param>
    /// <param name="overflow">The overflow mode.</param>
    /// <returns>The lowest dark row.</returns>
    private static int MeasureRenderedBottom(string text, PdfCodeOverflow overflow)
    {
        _ = RenderCode(text, overflow, out var bottom);
        return bottom;
    }

    /// <summary>Renders a single code block with the given overflow mode.</summary>
    /// <param name="text">The code text.</param>
    /// <param name="overflow">The overflow mode.</param>
    /// <param name="bottom">Receives the lowest dark row.</param>
    /// <returns>The rendered image.</returns>
    private static RenderedImage RenderCode(string text, PdfCodeOverflow overflow, out int bottom)
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var block = new PdfFlowCodeBlock(text) { Font = font, FontSize = CodeFontSize, Overflow = overflow, Border = PdfStroke.Black(BorderWidth) };
        _ = PdfTextFlow.Add(creator, [block], CreateOptions(font), CancellationToken.None);
        var render = RenderAt(creator.ToArray(), 0);
        bottom = LastDarkRow(render, (int)Margin);
        return render;
    }

    /// <summary>Lays out a code block on a test document.</summary>
    /// <param name="block">The code block.</param>
    private static void LayOut(PdfFlowCodeBlock block)
    {
        var creator = CreateCreator(tagged: false);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        _ = PdfTextFlow.Add(creator, [block], CreateOptions(font), CancellationToken.None);
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
        tagged ? new(new() { Title = "Code", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 }) : new(PdfDocumentCreatorOptions.Default);

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

    /// <summary>Determines whether a pixel is dark ink.</summary>
    /// <param name="image">The render.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row from the top.</param>
    /// <returns><see langword="true"/> when every channel is below <see cref="Dark"/>.</returns>
    private static bool IsDark(RenderedImage image, int x, int y)
    {
        var pixel = CreationTestKit.PixelAt(image, x, y);
        return pixel.Red < Dark && pixel.Green < Dark && pixel.Blue < Dark;
    }

    /// <summary>Finds the first column with dark ink in a rectangle.</summary>
    /// <param name="image">The render.</param>
    /// <param name="left">The left column.</param>
    /// <param name="right">The right column.</param>
    /// <param name="top">The top row.</param>
    /// <param name="bottom">The bottom row.</param>
    /// <returns>The first dark column, or -1 when none is found.</returns>
    private static int FirstDarkColumn(RenderedImage image, int left, int right, int top, int bottom)
    {
        for (var x = left; x <= right; x++)
        {
            for (var y = top; y <= bottom; y++)
            {
                if (IsDark(image, x, y))
                {
                    return x;
                }
            }
        }

        return -1;
    }

    /// <summary>Finds the lowest row with dark ink in a column.</summary>
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
}
