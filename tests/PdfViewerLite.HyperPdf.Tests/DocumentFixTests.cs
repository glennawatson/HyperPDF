// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks link targets, attachment listing, layer replay and damaged page trees.</summary>
public sealed class DocumentFixTests
{
    /// <summary>The size recorded in an attachment's /Params.</summary>
    private const long RecordedSize = 12_345;

    /// <summary>The decoded size of the attachment without a recorded size.</summary>
    private const long DecodedSize = 6;

    /// <summary>The attachments in the sample.</summary>
    private const int AttachmentCount = 3;

    /// <summary>The pages in the three page sample.</summary>
    private const int ThreePages = 3;

    /// <summary>The edge of the repeated small page.</summary>
    private const float SmallPageEdge = 100;

    /// <summary>The edge of the page following the missing kid.</summary>
    private const float LargePageEdge = 200;

    /// <summary>The middle page's index.</summary>
    private const int MiddlePage = 1;

    /// <summary>The last page's index, and the third attachment's.</summary>
    private const int FinalIndex = 2;

    /// <summary>The links on the middle page.</summary>
    private const int MiddleLinks = 4;

    /// <summary>The x coordinate of the sample destination in viewer space after the quarter turn and the crop.</summary>
    private const float ViewerX = 200;

    /// <summary>The y coordinate of the sample destination in viewer space after the quarter turn and the crop.</summary>
    private const float ViewerY = 100;

    /// <summary>The index of the layer that is hidden by default.</summary>
    private const int HiddenLayer = 1;

    /// <summary>The attachment's name.</summary>
    private const string AttachmentFile = "<< /Type /Filespec /F (a.txt) /EF << /F 8 0 R >> >>";

    /// <summary>The page links come from the named actions and destination in the sample.</summary>
    private const string PageDictionary = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>";

    /// <summary>The listed size uses /Params /Size, decodes otherwise and reports 0 for a stream that cannot be decoded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentSizesComeFromParamsThenDecodingAndSurviveBadStreams()
    {
        using var test = new HyperPdfTestDocument(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.txt) 5 0 R (b.txt) 6 0 R (c.txt) 7 0 R] >> >> >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                PageDictionary,
                "<< >>",
                AttachmentFile,
                "<< /Type /Filespec /F (b.txt) /EF << /F 9 0 R >> >>",
                "<< /Type /Filespec /F (c.txt) /EF << /F 10 0 R >> >>",
                MiniPdf.Stream("/Type /EmbeddedFile /Params << /Size 12345 >>", "hello"),
                MiniPdf.Stream("/Type /EmbeddedFile /Filter /FlateDecode", "not zlib at all"),
                MiniPdf.Stream("/Type /EmbeddedFile", "world!")));
        var attachments = DocumentFeature<IAttachmentSource>.Cast(test.Document)!.GetAttachments();

        await Assert.That(attachments.Count).IsEqualTo(AttachmentCount);
        await Assert.That(attachments[0].Size).IsEqualTo(RecordedSize);
        await Assert.That(attachments[1].Size).IsEqualTo(0);
        await Assert.That(attachments[FinalIndex].Size).IsEqualTo(DecodedSize);
    }

    /// <summary>Next page, previous page, first page and last page links lead to pages; outline entries ignore relative moves.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedActionsLeadToPages()
    {
        using var test = new HyperPdfTestDocument(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R /Outlines 9 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
                PageDictionary,
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Annots [6 0 R 7 0 R 8 0 R 11 0 R] >>",
                PageDictionary,
                Named("NextPage"),
                Named("PrevPage"),
                Named("FirstPage"),
                "<< /Type /Outlines /First 10 0 R /Last 12 0 R /Count 2 >>",
                "<< /Title (Next) /Parent 9 0 R /Next 12 0 R /A << /S /Named /N /NextPage >> >>",
                Named("LastPage"),
                "<< /Title (Last) /Parent 9 0 R /Prev 10 0 R /A << /S /Named /N /LastPage >> >>"));
        var links = test.Document.GetLinks(MiddlePage);
        var outline = test.Document.GetOutline();

        await Assert.That(links.Count).IsEqualTo(MiddleLinks);
        await Assert.That(links[0].Target.PageIndex).IsEqualTo(FinalIndex);
        await Assert.That(links[1].Target.PageIndex).IsEqualTo(0);
        await Assert.That(links[2].Target.PageIndex).IsEqualTo(0);
        await Assert.That(links[3].Target.PageIndex).IsEqualTo(FinalIndex);
        await Assert.That(outline[0].Target.Kind).IsEqualTo(LinkTargetKind.None);
        await Assert.That(outline[1].Target.PageIndex).IsEqualTo(FinalIndex);
    }

    /// <summary>A destination's location follows the crop box and rotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DestinationLocationHonoursCropBoxAndRotation()
    {
        using var test = new HyperPdfTestDocument(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /Rotate 90 /MediaBox [0 0 600 800] /CropBox [100 100 500 700] /Annots [4 0 R] >>",
                "<< /Type /Annot /Subtype /Link /Rect [10 10 50 50] /Dest [3 0 R /XYZ 200 300 0] >>"));
        var links = test.Document.GetLinks(0);

        await Assert.That(links.Count).IsEqualTo(1);
        await Assert.That(links[0].Target.Location).IsEqualTo(new PagePoint(ViewerX, ViewerY));
    }

    /// <summary>A hidden layer can be shown and reads back as visible.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HiddenLayerCanBeShown()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateWithLayers());
        var hyper = DocumentFeature<ILayerSource>.Cast(test.Document)!;
        var hidden = hyper.GetLayers()[HiddenLayer];

        await Assert.That(hidden.IsVisible).IsFalse();
        await Assert.That(hyper.SetLayerVisible(hidden.Id, true)).IsTrue();
        await Assert.That(hyper.GetLayers()[HiddenLayer].IsVisible).IsTrue();
    }

    /// <summary>A page tree skips a missing kid and keeps the following real page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedPageTreeSkipsMissingKid()
    {
        using var test = new HyperPdfTestDocument(
            MiniPdf.Build(
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 3 0 R 99 0 R 4 0 R] /Count 3 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>"));

        var sizes = test.Document.GetPageSizes();
        await Assert.That(test.Document.PageCount).IsEqualTo(ThreePages);
        PageSize small = new(SmallPageEdge, SmallPageEdge);
        PageSize large = new(LargePageEdge, LargePageEdge);
        await Assert.That(sizes[0]).IsEqualTo(small);
        await Assert.That(sizes[1]).IsEqualTo(small);
        await Assert.That(sizes[FinalIndex]).IsEqualTo(large);
    }

    /// <summary>Writes a link annotation that runs a named action.</summary>
    /// <param name="name">The action's name.</param>
    /// <returns>The object body.</returns>
    private static string Named(string name) =>
        $"<< /Type /Annot /Subtype /Link /Rect [10 10 50 50] /A << /S /Named /N /{name} >> >>";
}
