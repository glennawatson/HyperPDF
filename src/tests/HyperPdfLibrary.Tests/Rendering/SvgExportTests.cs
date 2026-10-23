// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks writing a page as a scalable vector graphics document.</summary>
public sealed class SvgExportTests
{
    /// <summary>The page width and height in points.</summary>
    private const int PageSize = 200;

    /// <summary>The font size of the test text.</summary>
    private const float FontSize = 24;

    /// <summary>Where the text sits on the page.</summary>
    private const float Baseline = 100;

    /// <summary>The left margin of the text.</summary>
    private const float Margin = 10;

    /// <summary>A page of text is written as an SVG document of the page's size, with the letters as outlines.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageBecomesAnSvgDocumentWithVectorText()
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        await using var output = new MemoryStream();

        var written = renderer.WriteSvg(0, PdfRenderFlags.None, output);

        var svg = Encoding.UTF8.GetString(output.ToArray());
        await Assert.That(written).IsTrue();
        await Assert.That(svg).Contains("<svg");
        await Assert.That(svg).Contains("width=\"200\"");
        await Assert.That(svg).Contains("<path");
        await Assert.That(svg).DoesNotContain("<text");
    }

    /// <summary>The asynchronous form writes a document with the same shapes as the synchronous one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncWritesTheSameShapes()
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        await using var sync = new MemoryStream();
        await using var asynchronous = new MemoryStream();

        _ = renderer.WriteSvg(0, PdfRenderFlags.None, sync);
        var written = await renderer.WriteSvgAsync(0, PdfRenderFlags.None, asynchronous, CancellationToken.None);

        await Assert.That(written).IsTrue();
        await Assert.That(CountPaths(asynchronous)).IsEqualTo(CountPaths(sync));
        await Assert.That(CountPaths(sync)).IsGreaterThan(0);
    }

    /// <summary>A page that does not exist writes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingPageWritesNothing()
    {
        using var document = SkiaTestRegistration.Open(CreateDocument(), null);
        using var renderer = new PdfPageRenderer(document);
        await using var output = new MemoryStream();

        var written = renderer.WriteSvg(PageSize, PdfRenderFlags.None, output);

        await Assert.That(written).IsFalse();
        await Assert.That(output.Length).IsEqualTo(0);
    }

    /// <summary>Counts the path elements of an SVG document.</summary>
    /// <param name="svg">The document.</param>
    /// <returns>The count.</returns>
    private static int CountPaths(MemoryStream svg) => Encoding.UTF8.GetString(svg.ToArray()).Split("<path").Length - 1;

    /// <summary>Creates a one page document with a line of embedded-font text.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreateDocument()
    {
        var creator = new PdfDocumentCreator(new());
        var font = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, false, false).Source!);
        var page = creator.BeginPage(PageSize, PageSize);
        page.DrawText(font, FontSize, Margin, Baseline, "Vector");
        _ = page.Finish();
        page.Dispose();
        return creator.ToArray();
    }
}
