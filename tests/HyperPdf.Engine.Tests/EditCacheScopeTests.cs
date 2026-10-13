// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Ocr;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Text;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Edits made through the engine drop the library's cached text page, marked content and structure tree only when they can
/// change what those hold. A cached value that survives an edit is the same instance as before it.
/// </summary>
public sealed class EditCacheScopeTests
{
    /// <summary>The note's colour.</summary>
    private const uint NoteColor = 0xFFEE88;

    /// <summary>The note's position.</summary>
    private const float NoteAt = 40;

    /// <summary>The recogniser's confidence in the test word.</summary>
    private const float Confidence = 90;

    /// <summary>The left edge of a new widget.</summary>
    private const float Left = 72;

    /// <summary>The top edge of a new widget.</summary>
    private const float Top = 300;

    /// <summary>The width of a new widget.</summary>
    private const float Width = 200;

    /// <summary>The height of a new widget.</summary>
    private const float Height = 24;

    /// <summary>The value the form data file gives the field.</summary>
    private const string ImportedName = "imported";

    /// <summary>The name of the field the sample form has.</summary>
    private const string FieldName = "Name";

    /// <summary>The name of a field the tests add.</summary>
    private const string AddedFieldName = "Added";

    /// <summary>The name a renamed field gets.</summary>
    private const string RenamedFieldName = "Renamed";

    /// <summary>The second page, as a selection.</summary>
    private static readonly int[] SecondPage = [1];

    /// <summary>The bytes of an attached file.</summary>
    private static readonly byte[] AttachedBytes = [1, 2, 3];

    /// <summary>The word a text layer adds.</summary>
    private static readonly OcrWord[] Words = [new("Zebra", new(40, 30, 80, 20), Confidence)];

    /// <summary>Gets an XFDF file that fills the sample form's field.</summary>
    private static ReadOnlySpan<byte> Answers => """
        <?xml version="1.0" encoding="UTF-8"?>
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="Name"><value>imported</value></field></fields></xfdf>
        """u8;

    /// <summary>Adding an annotation keeps the cached page data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationAddKeepsTheCachedPageData()
    {
        using var test = new HyperPdfTestDocument(Sample());
        var before = Read(test);

        var index = HyperPdfDocumentAnnotationEditing.AddNote(test.Document, 0, new(NoteAt, NoteAt), "A note", NoteColor);

        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await AssertKept(before, Read(test));
    }

    /// <summary>Changing an annotation keeps the cached page data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationChangeKeepsTheCachedPageData()
    {
        using var test = new HyperPdfTestDocument(Sample());
        var index = HyperPdfDocumentAnnotationEditing.AddNote(test.Document, 0, new(NoteAt, NoteAt), "A note", NoteColor);
        var before = Read(test);

        var changed = HyperPdfDocumentAnnotationEditing.SetContents(test.Document, 0, index, "Edited note");

        await Assert.That(changed).IsTrue();
        await AssertKept(before, Read(test));
    }

    /// <summary>Changing a form field's value keeps the cached page data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFieldValueChangeKeepsTheCachedPageData()
    {
        using var test = new HyperPdfTestDocument(Sample());
        var before = Read(test);

        var changed = HyperPdfDocumentFormFilling.SetText(test.Document, 0, 0, ImportedName);

        await Assert.That(changed).IsTrue();
        await AssertKept(before, Read(test));
    }

    /// <summary>Importing form data keeps the cached page data, and is still one undoable edit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormDataImportKeepsTheCachedPageData()
    {
        using var test = new HyperPdfTestDocument(Sample());
        var before = Read(test);

        var result = new HyperPdfFormDataImporter(test.Document).Import(Answers.ToArray());

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.FieldsApplied).IsEqualTo(1);
        await AssertKept(before, Read(test));
        await Assert.That(PdfDocumentEditing.Undo(test.Document.Document)).IsTrue();
        await AssertKept(before, Read(test));
    }

    /// <summary>Adding a bookmark keeps the cached page data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineEditKeepsTheCachedPageData(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(Sample());
        var editor = DocumentFeature<IOutlineEditor>.Cast(test.Document)!;
        var before = Read(test);

        var result = await editor.AddBookmarkAsync(0, -1, "Start", 0, cancellationToken);

        await Assert.That(result.Succeeded).IsTrue();
        await AssertKept(before, Read(test));
    }

    /// <summary>Adding an attached file keeps the cached page data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentEditKeepsTheCachedPageData(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(Sample());
        var editor = DocumentFeature<IAttachmentEditor>.Cast(test.Document)!;
        var before = Read(test);

        var result = await editor.AddAttachmentAsync("note.txt", AttachedBytes, cancellationToken);

        await Assert.That(result.Succeeded).IsTrue();
        await AssertKept(before, Read(test));
    }

    /// <summary>Changing and renaming a form field through the designer keeps the cached page data.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldChangeAndRenameKeepTheCachedPageData(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(Sample());
        var designer = DocumentFeature<IFormDesigner>.Cast(test.Document)!;
        var before = Read(test);

        var changed = await designer.ChangeFieldAsync(FieldName, new() { Value = ImportedName }, cancellationToken);
        var renamed = await designer.RenameFieldAsync(FieldName, RenamedFieldName, cancellationToken);

        await Assert.That(changed.Succeeded).IsTrue();
        await Assert.That(renamed.Succeeded).IsTrue();
        await AssertKept(before, Read(test));
    }

    /// <summary>Adding a form field drops the structure tree and keeps the text page and marked content.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldCreateDropsTheStructureTreeOnly(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(Sample());
        var designer = DocumentFeature<IFormDesigner>.Cast(test.Document)!;
        var before = Read(test);

        var created = await designer.CreateFieldAsync(new(FormFieldKind.Text, AddedFieldName, [new(0, new PageRect(Left, Top, Width, Height))]), cancellationToken);
        var after = Read(test);

        await Assert.That(created.Succeeded).IsTrue();
        await Assert.That(after.Text).IsSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsNotSameReferenceAs(before.Tree);
    }

    /// <summary>Removing a form field drops the structure tree and keeps the text page and marked content.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldRemoveDropsTheStructureTreeOnly(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(Sample());
        var designer = DocumentFeature<IFormDesigner>.Cast(test.Document)!;
        var before = Read(test);

        var removed = await designer.RemoveFieldAsync(FieldName, cancellationToken);
        var after = Read(test);

        await Assert.That(removed.Succeeded).IsTrue();
        await Assert.That(after.Text).IsSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsNotSameReferenceAs(before.Tree);
    }

    /// <summary>Reordering the pages drops the text page, marked content and structure tree.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageReorderDropsTheCachedPageData(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(Sample());
        var before = Read(test);

        await HyperPdfDocumentPageManagement.GetPageManager(test.Document).ApplyAsync(new(PageEditKind.Move, SecondPage, 0), cancellationToken);
        var after = Read(test);

        await Assert.That(after.Text).IsNotSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsNotSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsNotSameReferenceAs(before.Tree);
        await Assert.That(after.Text.Text).Contains("Second");
    }

    /// <summary>Adding a text layer drops the text page and marked content, and keeps the structure tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextLayerAdditionDropsTheTextAndMarkedContent()
    {
        using var test = new HyperPdfTestDocument(Sample());
        var writer = DocumentFeature<ITextLayerWriter>.Cast(test.Document)!;
        var before = Read(test);

        var written = writer.AddTextLayer(0, Words);
        var after = Read(test);

        await Assert.That(written).IsEqualTo(1);
        await Assert.That(after.Text).IsNotSameReferenceAs(before.Text);
        await Assert.That(after.Marked).IsNotSameReferenceAs(before.Marked);
        await Assert.That(after.Tree).IsSameReferenceAs(before.Tree);
        await Assert.That(after.Text.Text).Contains("Zebra");
    }

    /// <summary>Gets the cached page data of the first page.</summary>
    /// <param name="test">The document.</param>
    /// <returns>The text page, marked content and structure tree.</returns>
    private static CachedPageData Read(HyperPdfTestDocument test)
    {
        var library = test.Document.Document;
        return new(PdfDocumentText.GetTextPage(library, 0), PdfDocumentTagged.GetMarkedContent(library, 0), PdfDocumentTagged.GetStructureTree(library));
    }

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

    /// <summary>Builds a tagged file of two pages that draw "First" and "Second", with a text field on the first.</summary>
    /// <returns>The file bytes.</returns>
    private static byte[] Sample() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 8 0 R /MarkInfo << /Marked true >> /AcroForm << /Fields [9 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 7 0 R >> >> >> >>",
        "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 7 0 R >> >> /Contents 4 0 R /Annots [9 0 R] >>",
        MiniPdf.Stream(string.Empty, "BT /F1 12 Tf 20 100 Td (First) Tj ET"),
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 400] /Resources << /Font << /F1 7 0 R >> >> /Contents 6 0 R >>",
        MiniPdf.Stream(string.Empty, "BT /F1 12 Tf 20 100 Td (Second) Tj ET"),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Type /StructTreeRoot /K [] >>",
        "<< /Type /Annot /Subtype /Widget /Rect [20 200 120 220] /P 3 0 R /F 4 /FT /Tx /T (Name) /V (old) >>");

    /// <summary>The cached page data of one page.</summary>
    /// <param name="Text">The text page.</param>
    /// <param name="Marked">The marked content.</param>
    /// <param name="Tree">The structure tree.</param>
    private readonly record struct CachedPageData(PdfTextPage Text, PdfMarkedContentPage Marked, PdfStructureTree? Tree);
}
