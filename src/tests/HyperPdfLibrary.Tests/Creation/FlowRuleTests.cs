// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests for <see cref="PdfFlowRule"/> horizontal rules in <see cref="PdfTextFlow"/> and <see cref="PdfTableCell"/>.</summary>
public sealed class FlowRuleTests
{
    /// <summary>The default test page width in points.</summary>
    private const float PageWidth = 300F;

    /// <summary>A wider test page width in points.</summary>
    private const float WidePageWidth = 420F;

    /// <summary>The default test page height in points.</summary>
    private const float PageHeight = 200F;

    /// <summary>A short page height used to force page breaks.</summary>
    private const float ShortPageHeight = 90F;

    /// <summary>The default page margin in points.</summary>
    private const float Margin = 20F;

    /// <summary>A wider page margin in points.</summary>
    private const float WideMargin = 45F;

    /// <summary>The body font size in points.</summary>
    private const float BodySize = 11F;

    /// <summary>The thickness of the rule in points so it covers multiple pixel rows.</summary>
    private const float RuleThickness = 4F;

    /// <summary>The custom width of a short rule in points.</summary>
    private const float CustomRuleWidth = 100F;

    /// <summary>An oversized custom rule width that exceeds the content width.</summary>
    private const float OversizedRuleWidth = 500F;

    /// <summary>The width of the table column containing a rule in points.</summary>
    private const float CellColumnWidth = 160F;

    /// <summary>The width of a short rule inside a table cell in points.</summary>
    private const float CellRuleWidth = 60F;

    /// <summary>The custom space before a rule in points.</summary>
    private const float CustomSpaceBefore = 24F;

    /// <summary>The custom space after a rule in points.</summary>
    private const float CustomSpaceAfter = 18F;

    /// <summary>The red channel of a red rule.</summary>
    private const float RedChannel = 0.9F;

    /// <summary>The threshold above which the red channel counts as bright.</summary>
    private const int BrightRed = 180;

    /// <summary>The threshold below which green and blue channels count as dark.</summary>
    private const int DarkOther = 80;

    /// <summary>The threshold below which all channels count as dark ink.</summary>
    private const int DarkInk = 128;

    /// <summary>The pixel tolerance for rendered line edges.</summary>
    private const int EdgeTolerance = 2;

    /// <summary>The minimum number of paragraphs before a rule that forces a page break.</summary>
    private const int FillerParagraphs = 2;

    /// <summary>A default <see cref="PdfFlowRule"/> spans the available content width across different page sizes and margins.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultRuleSpansAvailableContentWidthAcrossPageSizesAndMargins()
    {
        var narrowRender = RenderRule(new() { Thickness = RuleThickness }, PageWidth, PageHeight, Margin);
        var wideRender = RenderRule(new() { Thickness = RuleThickness }, WidePageWidth, PageHeight, WideMargin);

        var narrowSpan = FindDarkHorizontalSpan(narrowRender);
        var wideSpan = FindDarkHorizontalSpan(wideRender);

        await Assert.That(Math.Abs(narrowSpan.Left - (int)Margin)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(narrowSpan.Right - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(wideSpan.Left - (int)WideMargin)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(wideSpan.Right - (int)(WidePageWidth - WideMargin))).IsLessThanOrEqualTo(EdgeTolerance);
    }

    /// <summary>A rule with an explicit width respects left, centre, and right alignment and clamps widths larger than the content area.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomWidthAndAlignmentPlaceRuleWithinContentBounds()
    {
        var leftSpan = FindDarkHorizontalSpan(RenderRule(new() { Thickness = RuleThickness, Width = CustomRuleWidth, Alignment = PdfTableAlignment.Left }, PageWidth, PageHeight, Margin));
        var centerSpan = FindDarkHorizontalSpan(RenderRule(new() { Thickness = RuleThickness, Width = CustomRuleWidth, Alignment = PdfTableAlignment.Center }, PageWidth, PageHeight, Margin));
        var rightSpan = FindDarkHorizontalSpan(RenderRule(new() { Thickness = RuleThickness, Width = CustomRuleWidth, Alignment = PdfTableAlignment.Right }, PageWidth, PageHeight, Margin));
        var clampedSpan = FindDarkHorizontalSpan(RenderRule(new() { Thickness = RuleThickness, Width = OversizedRuleWidth }, PageWidth, PageHeight, Margin));

        await Assert.That(Math.Abs(leftSpan.Left - (int)Margin)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(centerSpan.Left).IsGreaterThan(leftSpan.Left);
        await Assert.That(rightSpan.Left).IsGreaterThan(centerSpan.Left);
        await Assert.That(Math.Abs(rightSpan.Right - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(clampedSpan.Left - (int)Margin)).IsLessThanOrEqualTo(EdgeTolerance);
        await Assert.That(Math.Abs(clampedSpan.Right - (int)(PageWidth - Margin))).IsLessThanOrEqualTo(EdgeTolerance);
    }

    /// <summary>Custom colour and spacing move and colour the rule, and a rule that does not fit at the bottom of a page breaks to the next page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomColorSpacingAndPageBreakWorkAsExpected()
    {
        var defaultRule = new PdfFlowRule { Thickness = RuleThickness, Color = new(RedChannel, 0F, 0F) };
        var spacedRule = new PdfFlowRule { Thickness = RuleThickness, Color = new(RedChannel, 0F, 0F), SpaceBefore = CustomSpaceBefore, SpaceAfter = CustomSpaceAfter };

        var defaultRender = RenderFlow([new PdfFlowParagraph("Header line"), defaultRule], PageWidth, PageHeight, Margin, out _);
        var spacedRender = RenderFlow([new PdfFlowParagraph("Header line"), spacedRule], PageWidth, PageHeight, Margin, out _);

        var defaultRedRow = FirstRedRow(defaultRender);
        var spacedRedRow = FirstRedRow(spacedRender);

        await Assert.That(defaultRedRow).IsGreaterThan(0);
        await Assert.That(spacedRedRow).IsGreaterThan(defaultRedRow);

        var blocks = new List<PdfFlowBlock>();
        for (var i = 0; i < FillerParagraphs; i++)
        {
            blocks.Add(new PdfFlowParagraph($"Paragraph {i} filling the short page."));
        }

        blocks.Add(new PdfFlowRule { Thickness = RuleThickness, Color = new(RedChannel, 0F, 0F), SpaceBefore = CustomSpaceBefore });
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var pages = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = ShortPageHeight, Margin = Margin, BodySize = BodySize }, CancellationToken.None);
        var pdf = creator.ToArray();

        await Assert.That(pages).IsEqualTo(FillerParagraphs);
        await Assert.That(FirstRedRow(RenderPage(pdf, pages - 1))).IsGreaterThan(0);
    }

    /// <summary>In a tagged document, <see cref="PdfFlowRule"/> is emitted as a layout artifact both at page level and inside a table cell.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentMarksRulesAsLayoutArtifactsInFlowAndCells(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Title = "Rule Tags", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 });
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var cellRule = new PdfFlowRule { Thickness = RuleThickness, Width = CellRuleWidth, Alignment = PdfTableAlignment.Center, Color = new(RedChannel, 0F, 0F) };
        var cell = new PdfTableCell([new PdfFlowParagraph("Above"), cellRule, new PdfFlowParagraph("Below")]);
        var table = new PdfFlowTable([PdfTableRow.FromText("Content"), new([cell])]) { HeaderRows = 1, ColumnWidths = [CellColumnWidth] };

        PdfFlowBlock[] blocks =
        [
            new PdfFlowHeading(1, "Section"),
            new PdfFlowRule { Thickness = RuleThickness },
            new PdfFlowParagraph("Body paragraph."),
            table,
        ];

        _ = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin, BodySize = BodySize }, cancellationToken);
        var report = creator.Finish();
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        var render = RenderPage(pdf, 0);

        await Assert.That(report.ClaimsPdfUa).IsTrue();
        await Assert.That(accessibility.Findings.Select(static f => f.Code).ToArray()).IsEmpty();
        await Assert.That(FirstRedRow(render)).IsGreaterThan(0);
        await CreationTestKit.CheckWithQpdfAsync("flow-rule-tagged", pdf, cancellationToken);
    }

    /// <summary>Non-positive thickness or width and negative spacing throw <see cref="ArgumentOutOfRangeException"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidRuleDimensionsThrowArgumentOutOfRangeException()
    {
        await Assert.That(static () => RenderRule(new() { Thickness = 0F }, PageWidth, PageHeight, Margin)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => RenderRule(new() { Width = 0F }, PageWidth, PageHeight, Margin)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => RenderRule(new() { SpaceBefore = -1F }, PageWidth, PageHeight, Margin)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => RenderRule(new() { SpaceAfter = -1F }, PageWidth, PageHeight, Margin)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Renders a single <see cref="PdfFlowRule"/> on a page.</summary>
    /// <param name="rule">The rule block.</param>
    /// <param name="pageWidth">The page width.</param>
    /// <param name="pageHeight">The page height.</param>
    /// <param name="margin">The page margin.</param>
    /// <returns>The rendered first page.</returns>
    private static RenderedImage RenderRule(PdfFlowRule rule, float pageWidth, float pageHeight, float margin) =>
        RenderFlow([rule], pageWidth, pageHeight, margin, out _);

    /// <summary>Renders flow blocks and returns the first page render.</summary>
    /// <param name="blocks">The flow blocks.</param>
    /// <param name="pageWidth">The page width.</param>
    /// <param name="pageHeight">The page height.</param>
    /// <param name="margin">The page margin.</param>
    /// <param name="pages">Receives the page count.</param>
    /// <returns>The rendered first page.</returns>
    private static RenderedImage RenderFlow(IReadOnlyList<PdfFlowBlock> blocks, float pageWidth, float pageHeight, float margin, out int pages)
    {
        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        pages = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = pageWidth, PageHeight = pageHeight, Margin = margin, BodySize = BodySize }, CancellationToken.None);
        return RenderPage(creator.ToArray(), 0);
    }

    /// <summary>Renders a page at 1 pixel per point.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <returns>The rendered image.</returns>
    private static RenderedImage RenderPage(byte[] pdf, int pageIndex)
    {
        using var page = new RenderTestPage(pdf);
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(page.Document, pageIndex), 0, 1F, out var width, out var height);
        return page.RenderTile(new(pageIndex, 1F, 0, 0, 0, PdfRenderFlags.None), width, height);
    }

    /// <summary>Finds the leftmost and rightmost dark pixels across the image.</summary>
    /// <param name="image">The rendered page.</param>
    /// <returns>The leftmost and rightmost columns containing dark ink.</returns>
    private static HorizontalSpan FindDarkHorizontalSpan(RenderedImage image)
    {
        var left = int.MaxValue;
        var right = -1;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Red >= DarkInk || pixel.Green >= DarkInk || pixel.Blue >= DarkInk)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        return new(left, right + 1);
    }

    /// <summary>Finds the first row containing a bright red pixel, or -1 if none is present.</summary>
    /// <param name="image">The rendered page.</param>
    /// <returns>The zero-based row index.</returns>
    private static int FirstRedRow(RenderedImage image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var pixel = CreationTestKit.PixelAt(image, x, y);
                if (pixel.Red > BrightRed && pixel.Green < DarkOther && pixel.Blue < DarkOther)
                {
                    return y;
                }
            }
        }

        return -1;
    }

    /// <summary>Represents a horizontal pixel span.</summary>
    /// <param name="Left">The inclusive left pixel column.</param>
    /// <param name="Right">The exclusive right pixel column.</param>
    private readonly record struct HorizontalSpan(int Left, int Right);
}
