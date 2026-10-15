// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Creation;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.Engine.Tests.RenderingComparison;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Creation;

namespace HyperPdf.Engine.Tests;

/// <summary>Documents created through the engine: text, images, copied pages, accessibility, and how PDFium reads and draws them.</summary>
[NotInParallel(nameof(DocumentCreationTests))]
public sealed class DocumentCreationTests
{
    /// <summary>The width of the images in pixels.</summary>
    private const int ImageSize = 8;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The offset of the red sample in a BGRA pixel.</summary>
    private const int RedOffset = 2;

    /// <summary>The offset of the alpha sample in a BGRA pixel.</summary>
    private const int AlphaOffset = 3;

    /// <summary>The page width of the small pages the tests render.</summary>
    private const float PageWidth = 240F;

    /// <summary>The page height of the small pages the tests render.</summary>
    private const float PageHeight = 200F;

    /// <summary>Half the width of the small pages.</summary>
    private const float HalfWidth = PageWidth / 2;

    /// <summary>Half the height of the small pages.</summary>
    private const float HalfHeight = PageHeight / 2;

    /// <summary>The margin of the small pages.</summary>
    private const float Margin = 20F;

    /// <summary>The largest mean difference per channel accepted between two renderers; they rasterise glyph edges differently.</summary>
    private const double RenderThreshold = 1.5;

    /// <summary>The number of pages of the mixed request: a text page, a break, a text page and a copied page.</summary>
    private const int MixedPages = 3;

    /// <summary>The level of the section heading.</summary>
    private const int SectionLevel = 2;

    /// <summary>The pixel width an image is drawn at.</summary>
    private const float ImageWidth = 48F;

    /// <summary>The words in a paragraph long enough to need more pages.</summary>
    private const int LongWords = 600;

    /// <summary>The part of PDF/UA the accessible requests claim.</summary>
    private const int ClaimedPart = 2;

    /// <summary>The text of the first heading.</summary>
    private const string Heading = "Quarterly Report";

    /// <summary>The text of the first paragraph.</summary>
    private const string Body = "Hello World from a created page.";

    /// <summary>The description of the image in the accessible request.</summary>
    private const string ImageDescription = "A red square";

    /// <summary>Gets the types of the elements the accessible request makes below the document element.</summary>
    private static string[] ExpectedChildTypes { get; } = ["H1", "P", "Figure"];

    /// <summary>Text, an image and a copied page become pages of one document that reads back as written.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedRequestMakesPages(CancellationToken cancellationToken)
    {
        var source = Path.Combine(Path.GetTempPath(), $"created-source-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(source, TestPdf.Create(1), cancellationToken);
            var request = SmallPages(
            [
                new CreationHeading(1, Heading),
                new CreationParagraph(Body),
                CreateImageBlock(),
                new CreationPageBreak(),
                new CreationHeading(SectionLevel, "Second page"),
                new CreationSourcePage(source, 0, null),
            ]) with { Title = "Mixed" };
            await using var output = new MemoryStream();

            var result = await new HyperPdfDocumentCreator().CreateAsync(request, output, cancellationToken);
            var pdf = output.ToArray();
            CreatedFiles.Keep("engine-mixed.pdf", pdf);

            using var document = PdfDocumentReader.Open(pdf, null);
            await Assert.That(result.PageCount).IsEqualTo(MixedPages);
            await Assert.That(document.PageCount).IsEqualTo(MixedPages);
            await Assert.That(await CreatedFiles.TextOfAsync(pdf, 0)).Contains(Heading).And.Contains(Body);
            await Assert.That(await CreatedFiles.TextOfAsync(pdf, 1)).Contains("Second page");
            await Assert.That(result.IsTagged).IsFalse();
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>An accessible request is tagged, passes the accessibility checks and claims PDF/UA-2.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AccessibleRequestPassesTheChecks(CancellationToken cancellationToken)
    {
        var request = CreateAccessibleRequest();
        await using var output = new MemoryStream();

        var result = await new HyperPdfDocumentCreator().CreateAsync(request, output, cancellationToken);
        var pdf = output.ToArray();
        CreatedFiles.Keep("engine-accessible.pdf", pdf);

        using var document = PdfDocumentReader.Open(pdf, null);
        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(result.IsTagged).IsTrue();
        await Assert.That(result.ClaimsPdfUa).IsTrue();
        await Assert.That(result.Notes).IsEmpty();
        await Assert.That(report.TotalCount).IsEqualTo(0);
        await Assert.That(report.Claim.Part).IsEqualTo(ClaimedPart);
        await CheckWithQpdfAsync(pdf, cancellationToken);
    }

    /// <summary>Without a font file the text is drawn in a built-in font, so the claim is left out and the user is told why.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AccessibleRequestWithoutAFontLeavesOutTheClaim(CancellationToken cancellationToken)
    {
        var request = CreateAccessibleRequest() with { Font = null };
        await using var output = new MemoryStream();

        var result = await new HyperPdfDocumentCreator().CreateAsync(request, output, cancellationToken);

        await Assert.That(result.IsTagged).IsTrue();
        await Assert.That(result.ClaimsPdfUa).IsFalse();
        await Assert.That(result.Notes[0]).Contains("built-in font");
    }

    /// <summary>A long paragraph runs onto more pages.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongTextRunsOntoMorePages(CancellationToken cancellationToken)
    {
        var request = new DocumentCreationRequest([new CreationParagraph(string.Join(' ', Enumerable.Repeat("word", LongWords)))])
        {
            Font = new(TestFont.Create(), 0),
            PageWidth = PageWidth,
            PageHeight = PageHeight,
            Margin = Margin,
            PageNumbers = true,
        };
        await using var output = new MemoryStream();

        var result = await new HyperPdfDocumentCreator().CreateAsync(request, output, cancellationToken);

        await Assert.That(result.PageCount).IsGreaterThan(1);
    }

    /// <summary>The pages made can be inserted into an open document through the page manager.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreatedPagesInsertIntoAnOpenDocument(CancellationToken cancellationToken)
    {
        var created = Path.Combine(Path.GetTempPath(), $"created-insert-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var file = File.Create(created))
            {
                _ = await new HyperPdfDocumentCreator().CreateAsync(CreateAccessibleRequest(), file, cancellationToken);
            }

            using var target = new HyperPdfTestDocument(TestPdf.Create(1));
            var manager = HyperPdfDocumentPageManagement.GetPageManager(target.Document);

            await manager.InsertAsync(1, [created], cancellationToken);

            await Assert.That(target.Document.PageCount).IsGreaterThan(1);
        }
        finally
        {
            File.Delete(created);
        }
    }

    /// <summary>A font file that is not a font, an image whose pixels are short, and a missing source file are refused with clear errors.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadInputIsRefused(CancellationToken cancellationToken)
    {
        var creator = new HyperPdfDocumentCreator();
        var badFont = new DocumentCreationRequest([]) { Font = new(new byte[BgraBytes], 0) };
        var shortImage = new DocumentCreationRequest([new CreationImage(new byte[BgraBytes], CreationImageFormat.Bgra32, ImageSize, ImageSize, "x", 0)]);
        var missing = new DocumentCreationRequest([new CreationSourcePage(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf"), 0, null)]);

        await Assert.That(async () => await creator.CreateAsync(badFont, new MemoryStream(), cancellationToken)).Throws<ArgumentException>();
        await Assert.That(async () => await creator.CreateAsync(shortImage, new MemoryStream(), cancellationToken)).Throws<ArgumentException>();
        await Assert.That(async () => await creator.CreateAsync(missing, new MemoryStream(), cancellationToken)).Throws<DocumentOpenException>();
    }

    /// <summary>A cancelled token stops the work.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsTheWork()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(async () => await new HyperPdfDocumentCreator().CreateAsync(CreateAccessibleRequest(), new MemoryStream(), cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>PDFium extracts the same text from created pages as HyperPDF does.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextMatchesPdfium(CancellationToken cancellationToken)
    {
        var pdf = await CreateAsync(CreateAccessibleRequest(), cancellationToken);
        using var pair = new EnginePair(pdf);

        var expected = PdfiumReferenceText.GetTextPage(pair.Pdfium, 0).Text;
        var actual = PdfDocumentText.GetTextPage(pair.Library, 0).Text;

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(expected).Contains(Heading);
    }

    /// <summary>PDFium draws created text and image pages like HyperPDF draws them.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextAndImagePagesRenderLikePdfium(CancellationToken cancellationToken)
    {
        var pdf = await CreateAsync(CreateAccessibleRequest(), cancellationToken);

        var difference = await CompareRendersAsync(pdf, nameof(TextAndImagePagesRenderLikePdfium));

        await Assert.That(difference).IsLessThan(RenderThreshold);
    }

    /// <summary>PDFium draws a created shading and pattern page like HyperPDF draws it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingPageRendersLikePdfium()
    {
        var pdf = CreateShadingPage();

        var difference = await CompareRendersAsync(pdf, nameof(ShadingPageRendersLikePdfium));

        await Assert.That(difference).IsLessThan(RenderThreshold);
    }

    /// <summary>PDFium reads the structure of a created tagged document: the document element, the heading, the paragraph and the figure with its description.</summary>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureMatchesWhatPdfiumReads(CancellationToken cancellationToken)
    {
        var pdf = await CreateAsync(CreateAccessibleRequest(), cancellationToken);
        using var pair = new EnginePair(pdf);

        var tagged = PdfiumReferenceStructure.IsTagged(pair.Pdfium);
        var tree = PdfiumReferenceStructure.GetPageTree(pair.Pdfium, 0);
        var children = tree[0].Children;

        await Assert.That(tagged).IsTrue();
        await Assert.That(tree[0].Type).IsEqualTo("Document");
        await Assert.That(children.Select(static child => child.Type).ToArray()).IsEquivalentTo(ExpectedChildTypes);
        await Assert.That(children[^1].AlternateText).IsEqualTo(ImageDescription);
    }

    /// <summary>Runs <c>qpdf --check</c> when it is installed.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    private static async Task CheckWithQpdfAsync(byte[] pdf, CancellationToken cancellationToken)
    {
        var output = await QpdfCheck.RunAsync(pdf, cancellationToken);
        if (output is null)
        {
            return;
        }

        await Assert.That(output).IsEmpty().Because(output);
    }

    /// <summary>Creates a document through the engine.</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The file bytes.</returns>
    private static async Task<byte[]> CreateAsync(DocumentCreationRequest request, CancellationToken cancellationToken)
    {
        await using var output = new MemoryStream();
        _ = await new HyperPdfDocumentCreator().CreateAsync(request, output, cancellationToken);
        return output.ToArray();
    }

    /// <summary>Renders the first page with PDFium and with HyperPDF.</summary>
    /// <param name="pdf">The document.</param>
    /// <param name="name">The name the renders are saved under when they differ.</param>
    /// <returns>The mean absolute difference per channel.</returns>
    /// <exception cref="InvalidOperationException">HyperPDF could not render the page.</exception>
    private static async Task<double> CompareRendersAsync(byte[] pdf, string name)
    {
        using var input = new HyperPdfTestDocument(pdf);
        await input.Document.PreparePageAsync(0, CancellationToken.None);
        var info = new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.None);
        var reference = PdfiumReferenceRendering.Render(pdf, info);
        var expected = reference.Pixels.ToArray();
        var actual = new byte[expected.Length];
        if (!input.Document.Render(info, new(actual, reference.Width, reference.Height, reference.Width * BgraBytes)))
        {
            throw new InvalidOperationException("The page could not be rendered.");
        }

        long total = 0;
        for (var i = 0; i < expected.Length; i++)
        {
            total += Math.Abs(expected[i] - actual[i]);
        }

        var difference = (double)total / expected.Length;
        if (difference >= RenderThreshold)
        {
            new ParityImages(expected, actual, reference.Width, reference.Height).Save(name);
        }

        return difference;
    }

    /// <summary>Makes a request for a tagged document with a heading, a paragraph and a described image.</summary>
    /// <returns>The request.</returns>
    private static DocumentCreationRequest CreateAccessibleRequest() =>
        SmallPages([new CreationHeading(1, Heading), new CreationParagraph(Body), CreateImageBlock()]) with { Title = "Accessible", Language = "en", Accessible = true };

    /// <summary>Makes a request for small pages set in the test font.</summary>
    /// <param name="blocks">The content.</param>
    /// <returns>The request.</returns>
    private static DocumentCreationRequest SmallPages(IReadOnlyList<CreationBlock> blocks) =>
        new(blocks) { Font = new(TestFont.Create(), 0), PageWidth = PageWidth, PageHeight = PageHeight, Margin = Margin };

    /// <summary>Makes the described red square image block.</summary>
    /// <returns>The block.</returns>
    private static CreationImage CreateImageBlock() => new(CreateRedSquare(), CreationImageFormat.Bgra32, ImageSize, ImageSize, ImageDescription, ImageWidth);

    /// <summary>Makes a page painted with a radial shading and a tiling pattern.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] CreateShadingPage()
    {
        var creator = new HyperPdfLibrary.Creation.PdfDocumentCreator(HyperPdfLibrary.Creation.PdfDocumentCreatorOptions.Default);
        var shading = HyperPdfLibrary.Creation.PdfShadingWriter.AddRadial(
            creator.Sink,
            new(HalfWidth, HalfHeight, 0),
            new(HalfWidth, HalfHeight, HalfHeight),
            [new(0, 1, 1, 0), new(1, 0, 0, 1)],
            false,
            true);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.FillRectangleWithShading(shading, 0, 0, PageWidth, PageHeight);
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        return creator.ToArray();
    }

    /// <summary>Makes a solid red image.</summary>
    /// <returns>The pixels.</returns>
    private static byte[] CreateRedSquare()
    {
        var pixels = new byte[ImageSize * ImageSize * BgraBytes];
        for (var i = 0; i < pixels.Length; i += BgraBytes)
        {
            pixels[i + RedOffset] = byte.MaxValue;
            pixels[i + AlphaOffset] = byte.MaxValue;
        }

        return pixels;
    }
}
