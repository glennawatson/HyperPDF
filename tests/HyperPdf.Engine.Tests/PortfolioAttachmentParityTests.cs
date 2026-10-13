// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using TUnit.Assertions.Enums;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that embedded file names and sizes match PDFium for portfolios and for ordinary attachments (ISO 32000-2 §7.11.4, §7.11.6).</summary>
public sealed class PortfolioAttachmentParityTests
{
    /// <summary>The number of files in the synthetic portfolio.</summary>
    private const int PortfolioFiles = 3;

    /// <summary>The dictionary entries of each embedded file stream.</summary>
    private const string EmbeddedFileEntries = "/Type /EmbeddedFile";

    /// <summary>Gets the synthetic portfolio: a root file, a file in a folder and a file whose size differs, all with collection item values.</summary>
    private static byte[] Portfolio => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(<1>folder.txt) 5 0 R (a.txt) 6 0 R (b.bin) 7 0 R] >> >> /Collection 4 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        "<< /Type /Collection /D (a.txt) /View /D /Schema << /Name << /Subtype /S /N (Name) /O 0 >> >> /Sort << /S /Name >> /Folders 11 0 R >>",
        "<< /Type /Filespec /F (folder.txt) /UF (folder.txt) /EF << /F 8 0 R >> /CI 10 0 R >>",
        "<< /Type /Filespec /F (a.txt) /EF << /F 9 0 R >> >>",
        "<< /Type /Filespec /F (b.bin) /EF << /F 12 0 R >> >>",
        MiniPdf.Stream(EmbeddedFileEntries, "folder file"),
        MiniPdf.Stream(EmbeddedFileEntries, "a"),
        "<< /Type /CollectionItem /Name (Folder file) >>",
        "<< /Type /Folder /ID 0 /Name (Root) /Child 13 0 R >>",
        MiniPdf.Stream(EmbeddedFileEntries, "bytes of the third file are longer"),
        "<< /Type /Folder /ID 1 /Name (Docs) /Parent 11 0 R >>");

    /// <summary>The synthetic portfolio lists the same file names and sizes in HyperPDF as in PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SyntheticPortfolioAttachmentsMatch()
    {
        using var pair = new EnginePair(Portfolio);

        await AssertAttachmentsMatchAsync(pair);
        var portfolio = PdfDocumentPortfolio.GetPortfolio(pair.Library)!;
        var expected = PdfiumReferenceAttachments.GetAttachments(pair.Pdfium);

        await Assert.That(expected.Count).IsEqualTo(PortfolioFiles);
        await Assert.That(portfolio.Items.Select(static item => item.FileName)).IsEquivalentTo(expected.Select(static attachment => attachment.Name), CollectionOrdering.Matching);
        await Assert.That(portfolio.Items[0].FolderId).IsEqualTo(1);
    }

    /// <summary>The downloaded attachment samples list the same names and sizes in HyperPDF as in PDFium.</summary>
    /// <param name="id">The reference PDF id.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("pypdf-attachment")]
    [Arguments("pdfjs-annotation-attachment")]
    public async Task ReferenceAttachmentsMatch(string id)
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(id));

        await AssertAttachmentsMatchAsync(pair);
    }

    /// <summary>Compares the attachment names, sizes and bytes of both engines.</summary>
    /// <param name="pair">The documents.</param>
    /// <returns>A task.</returns>
    private static async Task AssertAttachmentsMatchAsync(EnginePair pair)
    {
        var expected = PdfiumReferenceAttachments.GetAttachments(pair.Pdfium);
        var actual = PdfDocumentAttachments.GetAttachments(pair.Library);

        await Assert.That(actual.Select(static attachment => attachment.Name)).IsEquivalentTo(expected.Select(static attachment => attachment.Name), CollectionOrdering.Matching);
        for (var i = 0; i < expected.Count; i++)
        {
            var bytes = actual[i].Data?.DecodeOrEmpty() ?? [];
            await Assert.That(bytes.Length).IsEqualTo(expected[i].Data?.Length ?? 0);
            await Assert.That(bytes).IsEquivalentTo(expected[i].Data ?? [], CollectionOrdering.Matching);
        }
    }
}
