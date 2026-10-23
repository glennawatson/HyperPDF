// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.AssociatedFiles;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests for portfolios and associated files.</summary>
public sealed class PortfolioTests
{
    /// <summary>The folder id of the first sample file.</summary>
    private const int FirstFolder = 2;

    /// <summary>The id of the nested folder in the nesting sample.</summary>
    private const int DeepFolder = 12;

    /// <summary>The first id of the sample free range.</summary>
    private const int FreeFirst = 3;

    /// <summary>The last id of the sample free range.</summary>
    private const int FreeLast = 5;

    /// <summary>The number of files in the sample portfolio.</summary>
    private const int FileCount = 2;

    /// <summary>The number of associated files in the sample document.</summary>
    private const int AssociatedCount = 3;

    /// <summary>A portfolio's schema, sort, colours, folder tree and items are read and mapped to the embedded files.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PortfolioIsMappedToEmbeddedFiles()
    {
        using var document = StructureDocuments.OpenPage(
            "/Names << /EmbeddedFiles << /Names [(<2>a.txt) 5 0 R (b.txt) 6 0 R] >> >> /Collection 4 0 R",
            string.Empty,
            "<< /Type /Collection /D (b.txt) /View /T /Colors << /Background [1 0 0] >> /Folders 7 0 R "
            + "/Schema << /Name << /Type /CollectionField /Subtype /S /N (Name) /O 1 >> "
            + "/Size << /Subtype /Size /N (Size) /O 0 /V false >> >> "
            + "/Sort << /S [/Name /Size] /A [true false] >> >>",
            "<< /Type /Filespec /F (a.txt) /CI 9 0 R /Folder 7 >>",
            "<< /Type /Filespec /F (b.txt) >>",
            "<< /Type /Folder /ID 0 /Name (Root) /Child 10 0 R /Free [3 5] >>",
            MiniPdf.Stream("/Type /EmbeddedFile", "x"),
            "<< /Type /CollectionItem /Name (Alpha) /Size 12 >>",
            "<< /Type /Folder /ID 1 /Name (Sub) /Parent 7 0 R /Next 11 0 R >>",
            "<< /Type /Folder /ID 2 /Name (Sub2) /Parent 7 0 R >>");

        var portfolio = HyperPdfLibrary.Document.PdfDocumentPortfolio.GetPortfolio(document)!;

        await Assert.That(HyperPdfLibrary.Document.PdfDocumentPortfolio.IsPortfolio(document)).IsTrue();
        await Assert.That(portfolio.Schema.Count).IsEqualTo(FileCount);
        await Assert.That(portfolio.Schema[0].Key).IsEqualTo("Size");
        await Assert.That(portfolio.Schema[0].Visible).IsFalse();
        await Assert.That(portfolio.Sort.Count).IsEqualTo(FileCount);
        await Assert.That(portfolio.Sort[1].Ascending).IsFalse();
        await Assert.That(portfolio.InitialDocument).IsEqualTo("b.txt");
        await Assert.That(portfolio.View).IsEqualTo("T");
        await Assert.That(portfolio.Colors!.Background!).IsEquivalentTo([1.0, 0.0, 0.0]);
        await Assert.That(portfolio.RootFolder!.Children.Length).IsEqualTo(FileCount);
        await Assert.That(portfolio.RootFolder.Children[1].Name).IsEqualTo("Sub2");
        await Assert.That(portfolio.RootFolder.FreeRanges[0].First).IsEqualTo(FreeFirst);
        await Assert.That(portfolio.RootFolder.FreeRanges[0].Last).IsEqualTo(FreeLast);
        await Assert.That(portfolio.Items.Count).IsEqualTo(FileCount);
        await Assert.That(portfolio.Items[0].FileName).IsEqualTo("a.txt");
        await Assert.That(portfolio.Items[0].FolderId).IsEqualTo(FirstFolder);
        await Assert.That(portfolio.Items[0].Fields["Name"]).IsEqualTo("Alpha");
        await Assert.That(portfolio.Items[0].Fields["Size"]).IsEqualTo("12");
        await Assert.That(portfolio.Items[1].FolderId).IsNull();
    }

    /// <summary>Files belong to the folder named by the <c>&lt;n&gt;</c> prefix of their name-tree key, through nested folders.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedFolderMembershipComesFromTheKeyPrefix()
    {
        using var document = StructureDocuments.OpenPage(
            "/Names << /EmbeddedFiles << /Names [(<12>deep.txt) 5 0 R (<1>mid.txt) 6 0 R (<x>odd.txt) 7 0 R (root.txt) 8 0 R] >> >> /Collection 4 0 R",
            string.Empty,
            "<< /Type /Collection /Folders 9 0 R >>",
            "<< /Type /Filespec /F (deep.txt) >>",
            "<< /Type /Filespec /F (mid.txt) >>",
            "<< /Type /Filespec /F (odd.txt) >>",
            "<< /Type /Filespec /F (root.txt) >>",
            "<< /Type /Folder /ID 0 /Name (Root) /Child 10 0 R >>",
            "<< /Type /Folder /ID 1 /Name (Mid) /Child 11 0 R >>",
            "<< /Type /Folder /ID 12 /Name (Deep) >>");

        var portfolio = HyperPdfLibrary.Document.PdfDocumentPortfolio.GetPortfolio(document)!;

        await Assert.That(portfolio.RootFolder!.Children[0].Children[0].Id).IsEqualTo(DeepFolder);
        await Assert.That(portfolio.Items[0].FolderId).IsEqualTo(DeepFolder);
        await Assert.That(portfolio.Items[1].FolderId).IsEqualTo(1);
        await Assert.That(portfolio.Items[2].FolderId).IsNull();
        await Assert.That(portfolio.Items[3].FolderId).IsNull();
    }

    /// <summary>A looping folder tree stops, and a document without a collection gives null.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FolderLoopsStop()
    {
        using var document = StructureDocuments.OpenPage(
            "/Collection 4 0 R",
            string.Empty,
            "<< /Type /Collection /Folders 5 0 R >>",
            "<< /Type /Folder /ID 0 /Child 5 0 R >>");
        using var plain = StructureDocuments.Open(string.Empty);

        await Assert.That(HyperPdfLibrary.Document.PdfDocumentPortfolio.GetPortfolio(document)!.RootFolder!.Children.Length).IsEqualTo(0);
        await Assert.That(HyperPdfLibrary.Document.PdfDocumentPortfolio.GetPortfolio(plain)).IsNull();
    }

    /// <summary>Associated files are read from the catalog, a page, an annotation and a structure element.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AssociatedFilesAreFoundOnEveryOwner()
    {
        using var document = StructureDocuments.OpenPage(
            "/AF [4 0 R]",
            "/AF 5 0 R /Annots [6 0 R]",
            "<< /Type /Filespec /F (doc.xml) /AFRelationship /Source /EF << /F 7 0 R >> /Desc (d) >>",
            "<< /Type /Filespec /F (p.csv) /AFRelationship /Data >>",
            "<< /Type /Annot /Subtype /Stamp /Rect [0 0 1 1] /AF [8 0 R] >>",
            MiniPdf.Stream("/Type /EmbeddedFile /Subtype /application#2Fxml", "<a/>"),
            "<< /Type /Filespec /F (a.bin) >>");

        var catalog = HyperPdfLibrary.Document.PdfDocumentAssociatedFiles.GetAssociatedFiles(document);
        var all = HyperPdfLibrary.Document.PdfDocumentAssociatedFiles.GetAllAssociatedFiles(document);

        await Assert.That(catalog.Count).IsEqualTo(1);
        await Assert.That(catalog[0].Relationship).IsEqualTo("Source");
        await Assert.That(catalog[0].MimeType).IsEqualTo("application/xml");
        await Assert.That(catalog[0].Description).IsEqualTo("d");
        await Assert.That(catalog[0].Data).IsNotNull();
        var pageFiles = HyperPdfLibrary.Document.PdfDocumentAssociatedFiles.GetAssociatedFiles(document, HyperPdfLibrary.Document.PdfDocumentPages.GetPage(document, 0));
        await Assert.That(pageFiles[0].Relationship).IsEqualTo("Data");
        await Assert.That(all.Count).IsEqualTo(AssociatedCount);
        await Assert.That(all[2].Owner).IsEqualTo(PdfAssociatedOwner.Annotation);
        await Assert.That(all[2].Relationship).IsEqualTo("Unspecified");
    }

    /// <summary>Structure elements and XObjects carry associated files too, and a shared specification is listed once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StructureAndXObjectFilesAreFound()
    {
        using var document = StructureDocuments.OpenPage(
            "/StructTreeRoot 4 0 R",
            "/Resources << /XObject << /Fm 7 0 R >> >>",
            "<< /Type /StructTreeRoot /K 5 0 R >>",
            "<< /Type /StructElem /S /Document /K [<< /Type /MCR /MCID 0 >>] /AF [6 0 R] >>",
            "<< /Type /Filespec /F (s.txt) /AFRelationship /Alternative >>",
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 1 1] /AF [6 0 R]", string.Empty));

        var all = HyperPdfLibrary.Document.PdfDocumentAssociatedFiles.GetAllAssociatedFiles(document);

        await Assert.That(all.Count).IsEqualTo(1);
        await Assert.That(all[0].Owner).IsEqualTo(PdfAssociatedOwner.XObject);
    }
}
