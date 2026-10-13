// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>An edit drops the text pages, marked content and structure tree only when it can change what they hold.</summary>
public sealed class PageCacheEditScopeTests
{
    /// <summary>The first icon coordinate.</summary>
    private const float IconLeft = 20;

    /// <summary>The second icon coordinate.</summary>
    private const float IconBottom = 20;

    /// <summary>The third icon coordinate.</summary>
    private const float IconRight = 40;

    /// <summary>The fourth icon coordinate.</summary>
    private const float IconTop = 40;

    /// <summary>The title an edit sets.</summary>
    private const string EditedTitle = "Edited";

    /// <summary>The name of the file the annotation holds.</summary>
    private const string FileName = "note.txt";

    /// <summary>The bytes of the file the annotation holds.</summary>
    private static readonly byte[] FileContent = "attached"u8.ToArray();

    /// <summary>Adding an annotation leaves the text page, marked content and structure tree as they were.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationAddKeepsTheCachedPageData()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        var before = Read(document);

        _ = PdfDocumentFileAttachmentAnnotations.AddFileAttachmentAnnotation(
            document,
            0,
            new(IconLeft, IconBottom, IconRight, IconTop),
            new(FileName, FileContent),
            PdfAttachmentIcon.Paperclip,
            null);

        await AssertKept(before, Read(document));
    }

    /// <summary>Editing the metadata leaves the text page, marked content and structure tree as they were.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MetadataEditKeepsTheCachedPageData()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        var before = Read(document);

        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = EditedTitle });

        await AssertKept(before, Read(document));
    }

    /// <summary>Adding a bookmark leaves the text page, marked content and structure tree as they were.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineEditKeepsTheCachedPageData()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        var before = Read(document);

        _ = PdfDocumentOutlineEditing.AddBookmark(document, default, new("Start", PdfBookmarkTarget.ToPage(PdfExplicitDestination.FitPage(0))));

        await AssertKept(before, Read(document));
    }

    /// <summary>Undoing and redoing an annotation edit leaves the cached page data as it was.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoOfAnAnnotationKeepsTheCachedPageData()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        _ = PdfDocumentFileAttachmentAnnotations.AddFileAttachmentAnnotation(
            document,
            0,
            new(IconLeft, IconBottom, IconRight, IconTop),
            new(FileName, FileContent),
            PdfAttachmentIcon.Paperclip,
            null);
        var before = Read(document);

        _ = PdfDocumentEditing.Undo(document);
        var undone = Read(document);
        _ = PdfDocumentEditing.Redo(document);

        await AssertKept(before, undone);
        await AssertKept(before, Read(document));
    }

    /// <summary>Writing a page's content again drops the text page and marked content, and keeps the structure tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContentEditDropsTheTextAndMarkedContentOnly()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        var before = Read(document);

        PdfPageContentApplication.Apply(PdfDocumentPageContent.GetPageContent(document, 0), PdfRegenerateMode.Rewrite);
        var after = Read(document);

        await Assert.That(after.Text).IsNotSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsNotSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsSameReferenceAs(before.Tree);
    }

    /// <summary>Reordering the pages drops the text page, marked content and structure tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderDropsAllThreeCaches()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        var before = Read(document);

        PdfDocumentPageOperations.ReorderPages(document, [1, 0]);
        var after = Read(document);

        await Assert.That(after.Text).IsNotSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsNotSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsNotSameReferenceAs(before.Tree);
        await Assert.That(after.Text.Text).Contains("Second");
    }

    /// <summary>Undoing a content edit drops the text page and marked content again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoOfAContentEditDropsTheTextAndMarkedContent()
    {
        using var document = PdfDocumentReader.Open(Tagged(), null);
        PdfPageContentApplication.Apply(PdfDocumentPageContent.GetPageContent(document, 0), PdfRegenerateMode.Rewrite);
        var before = Read(document);

        _ = PdfDocumentEditing.Undo(document);
        var after = Read(document);

        await Assert.That(after.Text).IsNotSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsNotSameReferenceAs(before.Marked);
    }

    /// <summary>Gets the cached page data of the first page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The text page, marked content and structure tree.</returns>
    private static CachedPageData Read(PdfDocument document) =>
        new(PdfDocumentText.GetTextPage(document, 0), PdfDocumentTagged.GetMarkedContent(document, 0), PdfDocumentTagged.GetStructureTree(document));

    /// <summary>Checks that every cached value is the same instance as before.</summary>
    /// <param name="before">The values before the edit.</param>
    /// <param name="after">The values after it.</param>
    /// <returns>A task.</returns>
    private static async Task AssertKept(CachedPageData before, CachedPageData after)
    {
        await Assert.That(before.Tree).IsNotNull();
        await Assert.That(after.Text).IsSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsSameReferenceAs(before.Tree);
    }

    /// <summary>Builds a tagged file of two pages that draw "First" and "Second".</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] Tagged() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 8 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 7 0 R >> >> /Contents 4 0 R >>",
        MiniPdf.Stream(string.Empty, "BT /F1 12 Tf 20 100 Td (First) Tj ET"),
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R >>",
        MiniPdf.Stream(string.Empty, "BT /F1 12 Tf 20 100 Td (Second) Tj ET"),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Type /StructTreeRoot /K [] >>");

    /// <summary>The cached page data of one page.</summary>
    /// <param name="Text">The text page.</param>
    /// <param name="Marked">The marked content.</param>
    /// <param name="Tree">The structure tree.</param>
    private readonly record struct CachedPageData(PdfTextPage Text, PdfMarkedContentPage Marked, PdfStructureTree? Tree);
}
