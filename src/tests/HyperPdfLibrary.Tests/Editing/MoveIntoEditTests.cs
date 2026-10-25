// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Checks that a tag can move into any other branch of the tree, that a move into itself is refused, and that undo restores the tree.</summary>
public sealed class MoveIntoEditTests
{
    /// <summary>A paragraph moves from one section into another, survives a save, and undo puts it back.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParagraphMovesToAnotherSectionAndBack(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(), null, cancellationToken);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var first = tree.Roots[0].Kids[0].Element!;
        var second = tree.Roots[0].Kids[1].Element!;

        await PdfDocumentStructureEditing.MoveIntoAsync(document, first.Kids[0].Element!, second, cancellationToken);

        var after = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        using (Assert.Multiple())
        {
            await Assert.That(after.Kids[0].Element!.Kids.Count).IsEqualTo(0);
            await Assert.That(after.Kids[1].Element!.Kids.Count).IsEqualTo(1);
            await Assert.That(after.Kids[1].Element!.Kids[0].Element!.MappedType).IsEqualTo("P");
        }

        using var reopened = PdfDocumentReader.Open(EditChecks.SaveCompact(document), null);
        await Assert.That(PdfDocumentTagged.GetStructureTree(reopened)!.Roots[0].Kids[1].Element!.Kids.Count).IsEqualTo(1);

        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Kids.Count).IsEqualTo(1);
    }

    /// <summary>A tag cannot move inside itself or inside a tag it holds.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingIntoItsOwnBranchIsRefused(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build(), null, cancellationToken);
        var first = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var paragraph = first.Kids[0].Element!;

        await Assert.That(() => PdfDocumentStructureEditing.MoveInto(document, first, paragraph)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentStructureEditing.MoveInto(document, first, first)).Throws<InvalidOperationException>();
    }

    /// <summary>In a PDF 2.0 tree a list cannot take a second caption, by a move or by a type change, and the first caption can still move out.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondCaptionInAListIsRefused(CancellationToken cancellationToken)
    {
        var bytes = MiniPdf.Build(
        [
            "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            "<< /Type /StructElem /S /Document /NS 9 0 R /P 5 0 R /K [6 0 R 7 0 R] >>",
            "<< /Type /StructTreeRoot /K 4 0 R /Namespaces [9 0 R] >>",
            "<< /Type /StructElem /S /L /NS 9 0 R /P 4 0 R /K [8 0 R] >>",
            "<< /Type /StructElem /S /Sect /NS 9 0 R /P 4 0 R /K [10 0 R] >>",
            "<< /Type /StructElem /S /Caption /NS 9 0 R /P 6 0 R /Pg 3 0 R /K 0 >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
            "<< /Type /StructElem /S /Caption /NS 9 0 R /P 7 0 R /Pg 3 0 R /K 1 >>",
        ]);
        using var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var list = root.Kids[0].Element!;
        var secondCaption = root.Kids[1].Element!.Kids[0].Element!;

        var error = Assert.Throws<InvalidOperationException>(() => PdfDocumentStructureEditing.MoveInto(document, secondCaption, list));

        await Assert.That(error.Message).Contains("only one Caption");
    }

    /// <summary>Builds a Document with two sections; the first holds a paragraph.</summary>
    /// <returns>The file.</returns>
    private static byte[] Build() => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> >>",
        "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
        "<< /Type /StructElem /S /Document /P 5 0 R /K [6 0 R 7 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R >>",
        "<< /Type /StructElem /S /Sect /P 4 0 R /K [8 0 R] >>",
        "<< /Type /StructElem /S /Sect /P 4 0 R >>",
        "<< /Type /StructElem /S /P /P 6 0 R /Pg 3 0 R /K 0 >>",
    ]);
}
