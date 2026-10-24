// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>Tests for <see cref="PdfDocumentBuilder"/>, <see cref="PdfObjectImporter"/> and <see cref="PdfPageImporter"/>.</summary>
public sealed class PageImporterTests
{
    /// <summary>The pages in the generated source.</summary>
    private const int SourcePages = 3;

    /// <summary>The last page of the generated source.</summary>
    private const int LastPage = 2;

    /// <summary>Gets a source whose resource dictionary refers to itself, to its page and to its page tree.</summary>
    private static ReadOnlySpan<byte> CyclicSource => """
        %PDF-1.4
        1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj
        2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj
        3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Resources << /Marker 4 0 R >> /Contents 5 0 R >> endobj
        4 0 obj << /Name (loop) /Self 4 0 R /Parent 3 0 R /Back 3 0 R /Tree 2 0 R >> endobj
        5 0 obj << /Length 9 >>
        stream
        0 0 10 10
        endstream
        endobj
        trailer << /Root 1 0 R /Size 6 >>
        """u8;

    /// <summary>Copied pages keep their content, and a font shared by two pages stays one object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiedPagesKeepContentAndShareObjects()
    {
        using var source = PdfDocumentReader.Open(TestPdf.Create(SourcePages), null);
        var builder = new PdfDocumentBuilder();
        var importer = new PdfPageImporter(builder, source);
        _ = importer.ImportPage(PdfDocumentPages.GetPage(source, 0), PdfAnnotationFilter.All);
        _ = importer.ImportPage(PdfDocumentPages.GetPage(source, LastPage), PdfAnnotationFilter.All);
        _ = importer.ImportPage(PdfDocumentPages.GetPage(source, 0), PdfAnnotationFilter.All);

        using var copy = PdfDocumentReader.Open(builder.ToArray(), null);
        var fonts = new List<int>();
        for (var i = 0; i < copy.PageCount; i++)
        {
            fonts.Add(FontNumber(copy, i));
        }

        await Assert.That(copy.PageCount).IsEqualTo(SourcePages);
        await Assert.That(PdfDocumentPages.GetPage(copy, 0).Width).IsEqualTo(PdfDocumentPages.GetPage(source, 0).Width);
        await Assert.That(PageContent(copy, 0)).IsEqualTo(PageContent(source, 0));
        await Assert.That(PageContent(copy, 1)).IsEqualTo(PageContent(source, LastPage));
        await Assert.That(fonts.Distinct().Count()).IsEqualTo(1);
    }

    /// <summary>
    /// Reference cycles end, /Parent entries are dropped, a reference to the copied page points at its copy, and a
    /// reference to a page tree becomes null.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclesEndAndPageTreesAreNotCopied()
    {
        using var source = PdfDocumentReader.Open(CyclicSource.ToArray(), null);
        var builder = new PdfDocumentBuilder();
        _ = new PdfPageImporter(builder, source).ImportPage(PdfDocumentPages.GetPage(source, 0), PdfAnnotationFilter.All);

        using var copy = PdfDocumentReader.Open(builder.ToArray(), null);
        var names = copy.Objects.Names;
        var marker = PdfDocumentPages.GetPage(copy, 0).Resources!.GetDictionary(names.Intern("Marker"));

        await Assert.That(marker).IsNotNull();
        await Assert.That(marker!.GetRaw(names.Intern("Self")).IsReference).IsTrue();
        await Assert.That(marker.GetDictionary(names.Intern("Self"))).IsSameReferenceAs(marker);
        await Assert.That(marker.ContainsKey(KnownName.Parent)).IsFalse();
        await Assert.That(marker.GetDictionary(names.Intern("Back"))).IsSameReferenceAs(PdfDocumentPages.GetPage(copy, 0).Dictionary);
        await Assert.That(marker.Get(names.Intern("Tree")).IsNull).IsTrue();
        await Assert.That(copy.PageCount).IsEqualTo(1);
    }

    /// <summary>Gets the object number of the first page's font.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The font's object number.</returns>
    private static int FontNumber(PdfDocument document, int page)
    {
        var fonts = PdfDocumentPages.GetPage(document, page).Resources!.GetDictionary(KnownName.Font)!;
        return fonts.GetRaw(fonts.GetKeyAt(0)).AsReference().Number;
    }

    /// <summary>Gets a page's decoded content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The content as text.</returns>
    private static string PageContent(PdfDocument document, int page) =>
        Encoding.Latin1.GetString(PdfDocumentPages.GetPage(document, page).Dictionary.GetStream(KnownName.Contents)!.DecodeToArray());
}
