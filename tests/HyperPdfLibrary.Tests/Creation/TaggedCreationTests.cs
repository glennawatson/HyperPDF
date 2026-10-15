// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Tagged documents: structure elements, marked content ids, the parent tree and the PDF/UA checks.</summary>
public sealed class TaggedCreationTests
{
    /// <summary>The width of the test pages.</summary>
    private const float PageWidth = 300F;

    /// <summary>The height of the test pages.</summary>
    private const float PageHeight = 400F;

    /// <summary>The size of an image in pixels.</summary>
    private const int ImageSize = 4;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int BgraBytes = 4;

    /// <summary>The number of pages in the long document, enough for the parent tree to need leaves.</summary>
    private const int LongPageCount = 70;

    /// <summary>The size of body text.</summary>
    private const float BodySize = 11F;

    /// <summary>The part of PDF/UA claimed.</summary>
    private const int ClaimedPart = 2;

    /// <summary>The level of the second heading.</summary>
    private const int SecondLevel = 2;

    /// <summary>The index of the figure among the document's children.</summary>
    private const int FigureIndex = 2;

    /// <summary>The words in the paragraph that runs across pages.</summary>
    private const int LongParagraphWords = 400;

    /// <summary>The left edge of the first column.</summary>
    private const float FirstColumn = 10F;

    /// <summary>The left edge of the second column.</summary>
    private const float SecondColumn = 100F;

    /// <summary>The baseline of the heading.</summary>
    private const float HeadingRow = 380F;

    /// <summary>The baseline of the table's header row.</summary>
    private const float HeaderRow = 350F;

    /// <summary>The baseline of the table's data row.</summary>
    private const float DataRow = 330F;

    /// <summary>The baseline of the list item.</summary>
    private const float ListRow = 300F;

    /// <summary>The size an image is drawn at.</summary>
    private const float ImageExtent = 20F;

    /// <summary>Gets the types of the elements the structure test expects, in order.</summary>
    private static string[] ExpectedTypes { get; } = ["H1", "P", "Figure"];

    /// <summary>A flowed tagged document with headings, paragraphs and an image has no accessibility findings and claims PDF/UA-2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FlowedDocumentPassesTheAccessibilityChecks()
    {
        var creator = CreateAccessible();
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var image = creator.AddRaster(CreateSquare(), PdfImageOptions.Default, CancellationToken.None);
        PdfFlowBlock[] blocks =
        [
            new PdfFlowHeading(1, "A Title"),
            new PdfFlowParagraph("First paragraph of text that is long enough to wrap onto more than one line of the page."),
            new PdfFlowHeading(SecondLevel, "A Section"),
            new PdfFlowParagraph("Second paragraph."),
            new PdfFlowImage(image, "A red square", 0),
            new PdfFlowImage(image, string.Empty, 0),
        ];

        _ = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight, PageNumbers = true }, CancellationToken.None);
        var report = creator.Finish();
        var pdf = creator.ToArray();
        CreatedFiles.Keep("tagged-flow.pdf", pdf);

        using var document = PdfDocumentReader.Open(pdf, null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None);
        await Assert.That(accessibility.Findings.Select(static finding => finding.Code).ToArray()).IsEmpty();
        await Assert.That(accessibility.IsMarked).IsTrue();
        await Assert.That(accessibility.Claim.Part).IsEqualTo(ClaimedPart);
        await Assert.That(report.ClaimsPdfUa).IsTrue();
        await Assert.That(report.Notes).IsEmpty();
    }

    /// <summary>The structure tree lists the elements in reading order with the right types and alternative text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureTreeKeepsReadingOrder()
    {
        var creator = CreateAccessible();
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var image = creator.AddRaster(CreateSquare(), PdfImageOptions.Default, CancellationToken.None);
        PdfFlowBlock[] blocks = [new PdfFlowHeading(1, "Top"), new PdfFlowParagraph("Body text."), new PdfFlowImage(image, "Alt words", 0)];
        _ = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight }, CancellationToken.None);

        using var document = PdfDocumentReader.Open(creator.ToArray(), null);

        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var children = ChildrenOf(tree.Roots[0]);
        await Assert.That(tree.Roots[0].RawType).IsEqualTo("Document");
        await Assert.That(children.Select(static child => child.RawType).ToArray()).IsEquivalentTo(ExpectedTypes);
        await Assert.That(children[FigureIndex].AlternateText).IsEqualTo("Alt words");
    }

    /// <summary>Every page of a long document has marked content mapped through a parent tree with leaves, and the checks find nothing.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongDocumentHasAParentTreeWithLeaves(CancellationToken cancellationToken)
    {
        var creator = CreateAccessible();
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var blocks = new List<PdfFlowBlock> { new PdfFlowHeading(1, "Long") };
        for (var page = 0; page < LongPageCount; page++)
        {
            blocks.Add(new PdfFlowParagraph($"Paragraph {page}."));
            blocks.Add(new PdfFlowPageBreak());
        }

        var added = PdfTextFlow.Add(creator, blocks, new(font) { PageWidth = PageWidth, PageHeight = PageHeight, BodySize = BodySize }, cancellationToken);
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, cancellationToken);
        await Assert.That(added).IsEqualTo(LongPageCount);
        await Assert.That(document.PageCount).IsEqualTo(LongPageCount);
        await Assert.That(accessibility.TotalCount).IsEqualTo(0);
        await CreationTestKit.CheckWithQpdfAsync("tagged-long", pdf, cancellationToken);
    }

    /// <summary>A paragraph that runs across pages stays one element whose content is on both pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParagraphAcrossPagesIsOneElement()
    {
        var creator = CreateAccessible();
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var text = string.Join(' ', Enumerable.Repeat("word", LongParagraphWords));

        _ = PdfTextFlow.Add(creator, [new PdfFlowHeading(1, "T"), new PdfFlowParagraph(text)], new(font) { PageWidth = PageWidth, PageHeight = PageHeight }, CancellationToken.None);
        var pdf = creator.ToArray();

        using var document = PdfDocumentReader.Open(pdf, null);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var paragraphs = ChildrenOf(tree.Roots[0]).Where(static element => element.RawType == "P").ToArray();
        await Assert.That(document.PageCount).IsGreaterThan(1);
        await Assert.That(paragraphs.Length).IsEqualTo(1);
        await Assert.That((await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None)).TotalCount).IsEqualTo(0);
    }

    /// <summary>A figure with no alternative text withholds the claim and says why, and the checks report it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FigureWithoutAlternativeTextWithholdsTheClaim()
    {
        var creator = CreateAccessible();
        var image = creator.AddRaster(CreateSquare(), PdfImageOptions.Default, CancellationToken.None);
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            var figure = creator.Tags!.AddElement(creator.Tags.Root, "Figure");
            canvas.BeginContent(figure);
            canvas.DrawImage(image, FirstColumn, FirstColumn, ImageExtent, ImageExtent);
            canvas.EndContent();
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        var report = creator.Finish();
        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None);

        await Assert.That(report.ClaimsPdfUa).IsFalse();
        await Assert.That(report.Notes[0]).Contains("figure");
        await Assert.That(accessibility.GetCount(PdfAccessibilityCode.FigureNoDescription)).IsEqualTo(1);
        await Assert.That(accessibility.Claim.IsClaimed).IsFalse();
    }

    /// <summary>A built-in font withholds the claim because PDF/UA needs every font embedded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BuiltInFontWithholdsTheClaim()
    {
        var creator = CreateAccessible();
        var font = creator.AddBuiltInFont(AppearanceFont.Helvetica);
        _ = PdfTextFlow.Add(creator, [new PdfFlowHeading(1, "T"), new PdfFlowParagraph("Body.")], new(font) { PageWidth = PageWidth, PageHeight = PageHeight }, CancellationToken.None);

        var report = creator.Finish();

        await Assert.That(report.ClaimsPdfUa).IsFalse();
        await Assert.That(report.Notes[0]).Contains("built-in font");
    }

    /// <summary>A copied page is not tagged here, so the claim is withheld.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiedPageWithholdsTheClaim()
    {
        var source = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
        var canvas = source.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(source.AddBuiltInFont(AppearanceFont.Helvetica), BodySize, FirstColumn, FirstColumn, "x");
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        using var original = PdfDocumentReader.Open(source.ToArray(), null);
        var creator = CreateAccessible();
        _ = creator.AddPage(original, 0);

        var report = creator.Finish();

        await Assert.That(report.ClaimsPdfUa).IsFalse();
        await Assert.That(report.Notes[0]).Contains("copied");
    }

    /// <summary>Drawing outside marked content in a tagged document is refused, so nothing is left untagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDrawingIsRefused()
    {
        var creator = CreateAccessible();
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var threw = false;
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            canvas.DrawText(font, BodySize, FirstColumn, FirstColumn, "x");
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        finally
        {
            canvas.Dispose();
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>A structure type that is not standard is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownStructureTypeIsRefused()
    {
        var creator = CreateAccessible();
        var threw = false;
        try
        {
            _ = creator.Tags!.AddElement(creator.Tags.Root, "Frobnicate");
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }

    /// <summary>A table with header cells and a list pass the checks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TablesAndListsPassTheChecks()
    {
        var creator = CreateAccessible();
        var font = creator.EmbedFont(CreationTestKit.OpenFont());
        var tags = creator.Tags!;
        var canvas = creator.BeginPage(PageWidth, PageHeight);
        try
        {
            DrawIn(ref canvas, font, tags.AddElement(tags.Root, "H1"), FirstColumn, HeadingRow, "Report");
            var table = tags.AddElement(tags.Root, "Table");
            var head = tags.AddElement(table, "TR");
            var columnHeader = new PdfTagOptions { TableScope = PdfTableScope.Column };
            DrawIn(ref canvas, font, tags.AddElement(head, "TH", columnHeader), FirstColumn, HeaderRow, "Name");
            DrawIn(ref canvas, font, tags.AddElement(head, "TH", columnHeader), SecondColumn, HeaderRow, "Value");
            var row = tags.AddElement(table, "TR");
            DrawIn(ref canvas, font, tags.AddElement(row, "TD"), FirstColumn, DataRow, "Alpha");
            DrawIn(ref canvas, font, tags.AddElement(row, "TD"), SecondColumn, DataRow, "One");
            var item = tags.AddElement(tags.AddElement(tags.Root, "L"), "LI");
            DrawIn(ref canvas, font, tags.AddElement(item, "LBody"), FirstColumn, ListRow, "Item");
            _ = canvas.Finish();
        }
        finally
        {
            canvas.Dispose();
        }

        using var document = PdfDocumentReader.Open(creator.ToArray(), null);
        var accessibility = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None);

        await Assert.That(accessibility.Findings.Select(static finding => finding.Code).ToArray()).IsEmpty();
    }

    /// <summary>Lists the child elements of an element.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The children that are elements, in order.</returns>
    private static PdfStructureElement[] ChildrenOf(PdfStructureElement element) =>
        [.. element.Kids.Where(static kid => kid.Element is not null).Select(static kid => kid.Element!)];

    /// <summary>Draws one line of text as marked content of an element.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="font">The font.</param>
    /// <param name="element">The element.</param>
    /// <param name="x">The baseline start x.</param>
    /// <param name="y">The baseline y.</param>
    /// <param name="text">The text.</param>
    private static void DrawIn(ref PdfPageCanvas canvas, PdfCanvasFont font, PdfTagElement element, float x, float y, string text)
    {
        canvas.BeginContent(element);
        canvas.DrawText(font, BodySize, x, y, text);
        canvas.EndContent();
    }

    /// <summary>Starts a tagged document that claims PDF/UA-2.</summary>
    /// <returns>The document.</returns>
    private static PdfDocumentCreator CreateAccessible() => new(new() { Title = "Accessible", Language = "en", Tagged = true, UaClaim = PdfUaConformance.Part2 });

    /// <summary>Makes a small solid image.</summary>
    /// <returns>The pixels.</returns>
    private static PdfRasterImage CreateSquare()
    {
        var pixels = new byte[ImageSize * ImageSize * BgraBytes];
        Array.Fill(pixels, byte.MaxValue);
        return new(PdfRasterFormat.Bgra32, ImageSize, ImageSize, pixels);
    }
}
