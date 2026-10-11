// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Writing;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.PageObjects;

/// <summary>Reads pages into objects and writes them again with no edits: the page must draw and read the same.</summary>
[NotInParallel]
public sealed class PageContentRoundTripTests
{
    /// <summary>The page the documents of <see cref="TestPdf"/> draw text on.</summary>
    private const int TextPages = 3;

    /// <summary>Preserve mode writes unchanged objects with their original bytes, so the content is identical.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreserveGivesTheSameContentBytes()
    {
        using var document = SkiaTestRegistration.Open(PageObjectSamples.Mixed(), null);
        var content = PdfDocumentPageContent.GetPageContent(document, 0);

        await Assert.That(PdfPageContentWriter.Regenerate(content).AsSpan().SequenceEqual(content.Source)).IsTrue();
        await Assert.That(PdfPageContentEditing.IsModified(content)).IsFalse();
    }

    /// <summary>Both modes keep the mixed page drawing and reading the same.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRegenerateMode.Preserve)]
    [Arguments(PdfRegenerateMode.Rewrite)]
    public async Task MixedPageRoundTrips(PdfRegenerateMode mode)
    {
        var pdf = PageObjectSamples.Mixed();
        var rewritten = RoundTrip(pdf, mode);

        await Assert.That(PageObjectSamples.Render(rewritten).Pixels.AsSpan().SequenceEqual(PageObjectSamples.Render(pdf).Pixels)).IsTrue();
        await Assert.That(TextOf(rewritten)).IsEqualTo(TextOf(pdf));
    }

    /// <summary>Both modes keep the text pages of the sample document drawing and reading the same.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRegenerateMode.Preserve)]
    [Arguments(PdfRegenerateMode.Rewrite)]
    public async Task SamplePagesRoundTrip(PdfRegenerateMode mode)
    {
        var pdf = TestPdf.Create(TextPages);
        using var original = new RenderTestPage(pdf);
        for (var page = 0; page < TextPages; page++)
        {
            var before = Render(original.Document, original.Renderer, page);
            var text = PdfDocumentText.GetTextPage(original.Document, page).Text;
            var content = PdfDocumentPageContent.GetPageContent(original.Document, page);
            PdfPageContentApplication.Apply(content, mode);
            var after = Render(original.Document, original.Renderer, page);

            await Assert.That(after.Pixels.AsSpan().SequenceEqual(before.Pixels)).IsTrue();
            await Assert.That(PdfDocumentText.GetTextPage(original.Document, page).Text).IsEqualTo(text);
        }
    }

    /// <summary>Writes the first page's content again and saves the document.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>The saved bytes.</returns>
    private static byte[] RoundTrip(byte[] pdf, PdfRegenerateMode mode)
    {
        using var document = SkiaTestRegistration.Open(pdf, null);
        PdfPageContentApplication.Apply(PdfDocumentPageContent.GetPageContent(document, 0), mode);
        return PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
    }

    /// <summary>Gets the first page's text.</summary>
    /// <param name="pdf">The PDF bytes.</param>
    /// <returns>The text.</returns>
    private static string TextOf(byte[] pdf)
    {
        using var document = SkiaTestRegistration.Open(pdf, null);
        return PdfDocumentText.GetTextPage(document, 0).Text;
    }

    /// <summary>Renders a page at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="renderer">Its renderer.</param>
    /// <param name="pageIndex">The page index.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(PdfDocument document, PdfPageRenderer renderer, int pageIndex)
    {
        PdfPageRenderer.GetPixelSize(PdfDocumentPages.GetPage(document, pageIndex), 0, 1, out var width, out var height);
        var pixels = new byte[width * height * RenderedImage.BytesPerPixel];
        _ = renderer.Render(new(pageIndex, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, width, height, width * RenderedImage.BytesPerPixel));
        return new(pixels, width, height);
    }
}
