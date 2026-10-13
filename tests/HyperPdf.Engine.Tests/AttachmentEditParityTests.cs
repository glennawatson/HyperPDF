// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that PDFium reads edited embedded files the way HyperPDF does.</summary>
public sealed class AttachmentEditParityTests
{
    /// <summary>Pages in the sample.</summary>
    private const int Pages = 2;

    /// <summary>PDFium lists the same attachment names and bytes after files are added, renamed, changed and removed.</summary>
    /// <param name="compact">Whether the document is saved as a compact rewrite rather than an incremental update.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task PdfiumReadsTheEditedAttachments(bool compact)
    {
        using var edited = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        _ = await PdfDocumentAttachmentEditing.AddAttachmentAsync(edited, new("b-report.pdf", "report bytes"u8.ToArray()) { MimeType = "application/pdf" }, CancellationToken.None);
        _ = await PdfDocumentAttachmentEditing.AddAttachmentAsync(edited, new("a-notes.txt", "some notes"u8.ToArray()), CancellationToken.None);
        _ = await PdfDocumentAttachmentEditing.AddAttachmentAsync(edited, new("c-old.bin", "old"u8.ToArray()), CancellationToken.None);
        _ = await PdfDocumentAttachmentEditing.UpdateAttachmentAsync(edited, "a-notes.txt", new() { Name = "d-notes.txt", Data = "changed notes"u8.ToArray() }, CancellationToken.None);
        _ = await PdfDocumentAttachmentEditing.RemoveAttachmentAsync(edited, "c-old.bin", CancellationToken.None);
        var bytes = compact ? PdfCompactWriter.Save(edited.Objects, PdfCompactOptions.Default) : PdfIncrementalWriter.Save(edited.Objects);

        using var pair = new EnginePair(bytes);

        var pdfium = PdfiumReferenceAttachments.GetAttachments(pair.Pdfium);
        var library = PdfDocumentAttachments.GetAttachments(pair.Library);
        await Assert.That(pdfium.Count).IsEqualTo(library.Count);
        await Assert.That(pdfium.Count).IsEqualTo(Pages);
        for (var i = 0; i < library.Count; i++)
        {
            await Assert.That(pdfium[i].Name).IsEqualTo(library[i].Name);
            await Assert.That(pdfium[i].Data).IsEquivalentTo(library[i].Data!.DecodeToArray(CancellationToken.None));
        }

        await Assert.That(pdfium[0].Name).IsEqualTo("b-report.pdf");
        await Assert.That(pdfium[1].Name).IsEqualTo("d-notes.txt");
    }
}
