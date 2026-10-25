// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tests for <see cref="PdfFlowBlock.KeepWithNext"/> pagination across headings, paragraphs, code blocks, images, and tables.</summary>
public sealed class FlowKeepWithNextTests
{
    /// <summary>The test page width in points.</summary>
    private const float PageWidth = 300F;

    /// <summary>The test page height in points (content height = 100 pt with 20 pt margins).</summary>
    private const float PageHeight = 140F;

    /// <summary>The page height in points for the multi-heading keep-with-next chain test (content height = 140 pt).</summary>
    private const float ChainPageHeight = 180F;

    /// <summary>The page margin in points.</summary>
    private const float Margin = 20F;

    /// <summary>The body font size in points.</summary>
    private const float BodySize = 11F;

    /// <summary>The requested width of the test image in points.</summary>
    private const float ImagePoints = 40F;

    /// <summary>The pixel size of the test raster image.</summary>
    private const int RasterPixels = 16;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The second heading level.</summary>
    private const int SecondLevel = 2;

    /// <summary>The expected number of pages when a block breaks to page 2.</summary>
    private const int TwoPages = 2;

    /// <summary>The number of words in an oversized paragraph.</summary>
    private const int OversizedWordCount = 120;

    /// <summary>The section label text used in keep-with-next paragraph tests.</summary>
    private const string SectionLabel = "SectionLabel";

    /// <summary>The target paragraph text used in keep-with-next paragraph tests.</summary>
    private const string TargetParagraph = "TargetParagraph";

    /// <summary>The heading text before a code block.</summary>
    private const string CodeHeading = "CodeHeading";

    /// <summary>The heading text before a table.</summary>
    private const string TableHeading = "TableHeading";

    /// <summary>The heading text before an image.</summary>
    private const string ImageHeading = "ImageHeading";

    /// <summary>The main heading text in a keep-with-next chain.</summary>
    private const string MainHeading = "MainHeading";

    /// <summary><see cref="PdfFlowHeading"/> defaults to <see cref="PdfFlowBlock.KeepWithNext"/> = <see langword="true"/>, while other blocks default to <see langword="false"/>.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultKeepWithNextValuesMatchSpecification()
    {
        var heading = new PdfFlowHeading(1, "Heading");
        var optedOutHeading = new PdfFlowHeading(1, "Heading") { KeepWithNext = false };
        var paragraph = new PdfFlowParagraph("Paragraph");
        var labelParagraph = new PdfFlowParagraph("Label:") { KeepWithNext = true };
        var rule = new PdfFlowRule();

        await Assert.That(heading.KeepWithNext).IsTrue();
        await Assert.That(optedOutHeading.KeepWithNext).IsFalse();
        await Assert.That(paragraph.KeepWithNext).IsFalse();
        await Assert.That(labelParagraph.KeepWithNext).IsTrue();
        await Assert.That(rule.KeepWithNext).IsFalse();
    }

    /// <summary>A heading or <see cref="PdfFlowBlock.KeepWithNext"/> label near the bottom of a page moves to the next page to stay with a following block.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeadingAndKeepWithNextLabelStayWithFollowingParagraphCodeImageAndTable()
    {
        var filler = new PdfFlowParagraph("Line one on page one.\nLine two on page one.\nLine three on page one.\nLine four on page one.");

        var paraPages = await LayoutPagesAsync(
        [
            filler,
            new PdfFlowParagraph(SectionLabel) { KeepWithNext = true },
            new PdfFlowParagraph(TargetParagraph),
        ]);

        await Assert.That(paraPages.Length).IsEqualTo(TwoPages);
        await Assert.That(paraPages[0]).DoesNotContain(SectionLabel);
        await Assert.That(paraPages[1]).Contains(SectionLabel);
        await Assert.That(paraPages[1]).Contains(TargetParagraph);

        var unkeptPages = await LayoutPagesAsync(
        [
            filler,
            new PdfFlowParagraph(SectionLabel) { KeepWithNext = false },
            new PdfFlowParagraph(TargetParagraph),
        ]);

        await Assert.That(unkeptPages[0]).Contains(SectionLabel);
        await Assert.That(unkeptPages[1]).Contains(TargetParagraph);

        var codePages = await LayoutPagesAsync(
        [
            filler,
            new PdfFlowHeading(SecondLevel, CodeHeading),
            new PdfFlowCodeBlock("var x = 1;\nvar y = 2;\nvar z = 3;"),
        ]);

        await Assert.That(codePages.Length).IsEqualTo(TwoPages);
        await Assert.That(codePages[0]).DoesNotContain(CodeHeading);
        await Assert.That(codePages[1]).Contains(CodeHeading);
        await Assert.That(codePages[1]).Contains("var x = 1;");

        var tablePages = await LayoutPagesAsync(
        [
            filler,
            new PdfFlowHeading(SecondLevel, TableHeading),
            new PdfFlowTable([PdfTableRow.FromText("ColA", "ColB"), PdfTableRow.FromText("ValA", "ValB")]),
        ]);

        await Assert.That(tablePages.Length).IsEqualTo(TwoPages);
        await Assert.That(tablePages[0]).DoesNotContain(TableHeading);
        await Assert.That(tablePages[1]).Contains(TableHeading);
        await Assert.That(tablePages[1]).Contains("ColA");

        var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var image = creator.AddRaster(new(PdfRasterFormat.Bgra32, RasterPixels, RasterPixels, new byte[RasterPixels * RasterPixels * BgraBytes]), PdfImageOptions.Default, CancellationToken.None);
        var imagePages = await LayoutPagesWithCreatorAsync(
            creator,
            [
                filler,
                new PdfFlowHeading(SecondLevel, ImageHeading),
                new PdfFlowImage(image, "Chart", ImagePoints),
            ],
            PageHeight);

        await Assert.That(imagePages.Length).IsEqualTo(TwoPages);
        await Assert.That(imagePages[0]).DoesNotContain(ImageHeading);
        await Assert.That(imagePages[1]).Contains(ImageHeading);
    }

    /// <summary>A chain of consecutive <see cref="PdfFlowBlock.KeepWithNext"/> blocks moves together when the following block does not fit on the current page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConsecutiveKeepWithNextChainMovesTogetherToNextPage()
    {
        var filler = new PdfFlowParagraph("Opening line one.\nOpening line two.\nOpening line three.");
        var pages = await LayoutPagesWithCreatorAsync(
            new(PdfDocumentCreatorOptions.Default),
            [
                filler,
                new PdfFlowHeading(1, MainHeading),
                new PdfFlowHeading(SecondLevel, "SubHeading"),
                new PdfFlowParagraph("LeadInLabel") { KeepWithNext = true },
                new PdfFlowParagraph("BodyAfterChain"),
            ],
            ChainPageHeight);

        await Assert.That(pages.Length).IsEqualTo(TwoPages);
        await Assert.That(pages[0]).Contains("Opening line one.");
        await Assert.That(pages[0]).DoesNotContain(MainHeading);
        await Assert.That(pages[1]).Contains(MainHeading);
        await Assert.That(pages[1]).Contains("SubHeading");
        await Assert.That(pages[1]).Contains("LeadInLabel");
        await Assert.That(pages[1]).Contains("BodyAfterChain");
    }

    /// <summary>An oversized <see cref="PdfFlowBlock.KeepWithNext"/> block that exceeds a single page does not create a blank page or loop infinitely.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedKeepWithNextBlockDoesNotCreateBlankPageOrInfiniteLoop()
    {
        var longText = string.Join(' ', Enumerable.Range(0, OversizedWordCount).Select(static i => $"word{i:D3}"));
        var pages = await LayoutPagesAsync(
        [
            new PdfFlowParagraph(longText) { KeepWithNext = true },
            new PdfFlowParagraph("FinalLine"),
        ]);

        await Assert.That(pages.Length).IsGreaterThan(1);
        await Assert.That(pages[0]).Contains("word000");
        await Assert.That(pages[^1]).Contains("FinalLine");
    }

    /// <summary>Lays out blocks and returns the extracted text of each page.</summary>
    /// <param name="blocks">The flow blocks.</param>
    /// <returns>The extracted text per page.</returns>
    private static Task<string[]> LayoutPagesAsync(IReadOnlyList<PdfFlowBlock> blocks) =>
        LayoutPagesWithCreatorAsync(new(PdfDocumentCreatorOptions.Default), blocks, PageHeight);

    /// <summary>Lays out blocks in an existing <see cref="PdfDocumentCreator"/> and returns the extracted text of each page.</summary>
    /// <param name="creator">The document creator.</param>
    /// <param name="blocks">The flow blocks.</param>
    /// <param name="pageHeight">The page height in points.</param>
    /// <returns>The extracted text per page.</returns>
    private static async Task<string[]> LayoutPagesWithCreatorAsync(PdfDocumentCreator creator, IReadOnlyList<PdfFlowBlock> blocks, float pageHeight)
    {
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var options = new PdfFlowOptions(font) { PageWidth = PageWidth, PageHeight = pageHeight, Margin = Margin, BodySize = BodySize, HeadingFont = font };
        var pageCount = PdfTextFlow.Add(creator, blocks, options, CancellationToken.None);
        var pdf = creator.ToArray();
        var texts = new string[pageCount];
        for (var i = 0; i < pageCount; i++)
        {
            texts[i] = await CreatedFiles.TextOfAsync(pdf, i);
        }

        return texts;
    }
}
