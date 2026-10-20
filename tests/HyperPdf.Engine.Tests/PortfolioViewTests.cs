// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using TUnit.Assertions.Enums;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the order, folders, field values and opening file a PDF portfolio asks the attachments panel to show (ISO 32000-2 §7.11.6).</summary>
public sealed class PortfolioViewTests
{
    /// <summary>The number of files in the synthetic portfolio.</summary>
    private const int Files = 3;

    /// <summary>The dictionary entries of each embedded file stream.</summary>
    private const string EmbeddedFileEntries = "/Type /EmbeddedFile";

    /// <summary>The name of the file the portfolio opens at.</summary>
    private const string InitialName = "a.txt";

    /// <summary>The file names in the order the portfolio's sort keys ask for.</summary>
    private static readonly string[] SortedNames = ["b.bin", InitialName, "folder.txt"];

    /// <summary>Gets a portfolio sorted by its Size field, descending, that opens at a.txt and shows the Size and Note fields.</summary>
    private static byte[] Portfolio => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(<1>folder.txt) 5 0 R (a.txt) 6 0 R (b.bin) 7 0 R] >> >> /Collection 4 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        "<< /Type /Collection /D (a.txt) /View /T /Schema << /Size << /Subtype /N /N (Size) /O 1 /V false >> /Note << /Subtype /S /N (Note) /O 2 /V true >> >> "
        + "/Sort << /S [/Size] /A [false] >> /Folders 11 0 R >>",
        "<< /Type /Filespec /F (folder.txt) /UF (folder.txt) /EF << /F 8 0 R >> /CI 10 0 R >>",
        "<< /Type /Filespec /F (a.txt) /EF << /F 9 0 R >> /CI 14 0 R >>",
        "<< /Type /Filespec /F (b.bin) /EF << /F 12 0 R >> /CI 15 0 R >>",
        MiniPdf.Stream(EmbeddedFileEntries, "folder file"),
        MiniPdf.Stream(EmbeddedFileEntries, "a"),
        "<< /Type /CollectionItem /Size 5 /Note (inside) >>",
        "<< /Type /Folder /ID 0 /Name (Root) /Child 13 0 R >>",
        MiniPdf.Stream(EmbeddedFileEntries, "bytes of the third file are longer"),
        "<< /Type /Folder /ID 1 /Name (Docs) /Parent 11 0 R >>",
        "<< /Type /CollectionItem /Size 10 >>",
        "<< /Type /CollectionItem /Size 100 /Note (biggest) >>");

    /// <summary>The files come back in the portfolio's sort order, largest value of the sort field first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FilesFollowThePortfolioSortKeys()
    {
        using var pair = new EnginePair(Portfolio);
        var attachments = DocumentFeature<IAttachmentSource>.Cast(pair.HyperPdf)!.GetAttachments();

        var inOrder = attachments.OrderBy(static attachment => attachment.PortfolioOrder).Select(static attachment => attachment.Name).ToArray();

        await Assert.That(attachments.Count).IsEqualTo(Files);
        await Assert.That(inOrder).IsEquivalentTo(SortedNames, CollectionOrdering.Matching);
    }

    /// <summary>A file shows its folder and the schema fields that are visible, and nothing for fields the portfolio hides.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DetailsShowFolderAndVisibleFields()
    {
        using var pair = new EnginePair(Portfolio);
        var attachments = DocumentFeature<IAttachmentSource>.Cast(pair.HyperPdf)!.GetAttachments();

        var folderFile = attachments.Single(static attachment => attachment.Name == "folder.txt");
        var plain = attachments.Single(static attachment => attachment.Name == InitialName);

        await Assert.That(folderFile.PortfolioDetails).IsEqualTo("Folder: Root/Docs; Note: inside");
        await Assert.That(plain.PortfolioDetails).IsNull();
    }

    /// <summary>The summary names the view, the file count and the file the portfolio opens at, and that file is marked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SummaryNamesTheInitialFile()
    {
        using var pair = new EnginePair(Portfolio);
        var source = DocumentFeature<IAttachmentSource>.Cast(pair.HyperPdf)!;
        var attachments = source.GetAttachments();

        var summary = source.GetPortfolioSummary();

        await Assert.That(summary).IsNotNull();
        await Assert.That(summary!.View).IsEqualTo("T");
        await Assert.That(summary.FileCount).IsEqualTo(Files);
        await Assert.That(summary.InitialFile).IsEqualTo(InitialName);
        await Assert.That(attachments.Single(static attachment => attachment.IsPortfolioInitial).Name).IsEqualTo(InitialName);
    }

    /// <summary>A document with embedded files but no collection is not a portfolio.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OrdinaryAttachmentsAreNotAPortfolio()
    {
        var bytes = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(a.txt) 4 0 R] >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
            "<< /Type /Filespec /F (a.txt) /EF << /F 5 0 R >> >>",
            MiniPdf.Stream(EmbeddedFileEntries, "a"));
        using var pair = new EnginePair(bytes);
        var source = DocumentFeature<IAttachmentSource>.Cast(pair.HyperPdf)!;

        await Assert.That(source.GetPortfolioSummary()).IsNull();
        await Assert.That(source.GetAttachments()[0].PortfolioOrder).IsEqualTo(-1);
    }
}
