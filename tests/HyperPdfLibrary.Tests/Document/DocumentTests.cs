// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for <see cref="PdfDocument"/>.</summary>
public sealed class DocumentTests
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 3;

    /// <summary>The page the first page's internal link leads to (zero based).</summary>
    private const int LinkedPageIndex = 2;

    /// <summary>The year of the sample creation date.</summary>
    private const int CreatedYear = 2026;

    /// <summary>The time zone offset of the sample creation date, in hours.</summary>
    private const int CreatedOffsetHours = 10;

    /// <summary>The links on the first sample page.</summary>
    private const int FirstPageLinks = 2;

    /// <summary>A generated document opens with its pages, sizes and information.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensPagesAndInformation()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var info = PdfDocumentMetadata.GetInfo(document);
        var page = PdfDocumentPages.GetPage(document, 0);

        await Assert.That(document.PageCount).IsEqualTo(Pages);
        await Assert.That(page.Width).IsEqualTo(TestPdf.PortraitWidth);
        await Assert.That(page.Height).IsEqualTo(TestPdf.PortraitHeight);
        await Assert.That(info.Title).IsEqualTo(TestPdf.Title);
        await Assert.That(info.Author).IsEqualTo(TestPdf.Author);
        await Assert.That(info.Created!.Value.Year).IsEqualTo(CreatedYear);
        await Assert.That(info.Created!.Value.Offset).IsEqualTo(TimeSpan.FromHours(CreatedOffsetHours));
        await Assert.That(info.IsEncrypted).IsFalse();
    }

    /// <summary>The outline lists one entry per chapter leading to its page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsOutline()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var outline = PdfDocumentNavigation.GetOutline(document);

        await Assert.That(outline.Count).IsEqualTo(Pages);
        await Assert.That(outline[1].Title).IsEqualTo("Chapter 2");
        await Assert.That(outline[1].Action.Value is GoToAction { Destination.PageIndex: 1 }).IsTrue();
    }

    /// <summary>Links resolve to a page destination and a URI.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsLinks()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var links = PdfDocumentLinks.GetLinks(document, 0);

        await Assert.That(links.Count).IsEqualTo(FirstPageLinks);
        await Assert.That(links[0].Action.Value is GoToAction { Destination.PageIndex: LinkedPageIndex }).IsTrue();
        await Assert.That(links[1].Action.Value is UriAction { Uri: var uri } && uri == TestPdf.LinkUri).IsTrue();
        await Assert.That(PdfDocumentLinks.GetLinks(document, 1).Count).IsEqualTo(0);
    }

    /// <summary>The link cache follows the page set: after pages are deleted, links are read for the new pages and an old index is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkCacheFollowsEditedPages()
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(Pages), null);
        var before = PdfDocumentLinks.GetLinks(document, 0);

        PdfDocumentPageOperations.DeletePages(document, [1]);

        await Assert.That(PdfDocumentLinks.GetLinks(document, 0).Count).IsEqualTo(before.Count);
        await Assert.That(() => PdfDocumentLinks.GetLinks(document, Pages - 1)).Throws<ArgumentOutOfRangeException>();
        _ = PdfDocumentEditing.Undo(document);
        await Assert.That(PdfDocumentLinks.GetLinks(document, Pages - 1).Count).IsEqualTo(0);
    }

    /// <summary>Layers list in document order with their default visibility, and toggle without touching the file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAndTogglesLayers()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateWithLayers(), null);
        var content = PdfDocumentLayers.GetOptionalContent(document);
        var layers = content.Layers;
        var version = content.Version;

        await Assert.That(layers.Count).IsEqualTo(FirstPageLinks);
        await Assert.That(layers[0].Name).IsEqualTo(TestPdf.DrawingLayer);
        await Assert.That(layers[0].IsVisible).IsTrue();
        await Assert.That(layers[1].Name).IsEqualTo(TestPdf.NotesLayer);
        await Assert.That(layers[1].IsVisible).IsFalse();

        await Assert.That(content.SetVisible(layers[1].Id, true)).IsTrue();
        await Assert.That(content.Layers[1].IsVisible).IsTrue();
        await Assert.That(content.Version).IsNotEqualTo(version);
        await Assert.That(content.SetVisible(int.MaxValue, true)).IsFalse();
    }

    /// <summary>A document packed in object streams with a cross-reference stream opens.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensCompressedLayout()
    {
        using var document = PdfDocumentReader.Open(TestPdf.CreateCompressed(), null);

        await Assert.That(document.PageCount).IsEqualTo(1);
        await Assert.That(document.Objects.UsesXrefStreams).IsTrue();
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Resources).IsNotNull();
    }

    /// <summary>A file whose cross-reference offset is wrong is rebuilt by scanning.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepairsBrokenCrossReference()
    {
        var bytes = TestPdf.Create(Pages);
        var text = Encoding.Latin1.GetString(bytes);
        var marker = text.LastIndexOf("startxref", StringComparison.Ordinal);
        var damaged = Encoding.Latin1.GetBytes(string.Concat(text.AsSpan(0, marker), "startxref\n12\n%%EOF\n"));

        using var document = PdfDocumentReader.Open(damaged, null);

        await Assert.That(document.PageCount).IsEqualTo(Pages);
        await Assert.That(document.Objects.WasRepaired).IsTrue();
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(TestPdf.Title);
    }

    /// <summary>Bytes that are not a PDF report a format error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GarbageReportsFormatError()
    {
        var exception = await Assert.That(static () => PdfDocumentReader.Open("not a pdf"u8.ToArray(), null)).Throws<PdfException>();

        await Assert.That(exception!.Error).IsEqualTo(PdfError.Format);
    }
}
