// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.AssociatedFiles;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Portfolio;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>
/// Tests that damaged portfolio dictionaries (ISO 32000-2 §7.11.6) and associated-file entries (§14.13) read as bounded data
/// without throwing, and that valid relationship names, owners and folders are kept.
/// </summary>
public sealed class PortfolioRobustnessTests
{
    /// <summary>The number of fields that survive in the damaged schema sample.</summary>
    private const int SurvivingFields = 2;

    /// <summary>The folder chain length, far beyond the nesting limit.</summary>
    private const int ChainLength = 300;

    /// <summary>The first object number after the catalog objects in the chain sample.</summary>
    private const int ChainStart = 5;

    /// <summary>The page index recorded for the page-owned sample.</summary>
    private const int SamplePage = 7;

    /// <summary>The object number of the font in the owner sample.</summary>
    private const int FontNumber = 4;

    /// <summary>The most levels a read folder tree may have.</summary>
    private const int NestingLimit = 64;

    /// <summary>The catalog entries that make object 4 the collection.</summary>
    private const string CollectionEntry = "/Collection 4 0 R";

    /// <summary>The embedded files name tree used by the item samples.</summary>
    private const string FilesTree = "/Names << /EmbeddedFiles << /Names [(a.txt) 5 0 R] >> >>";

    /// <summary>The relationship reported when none is named.</summary>
    private const string Unspecified = "Unspecified";

    /// <summary>Schema fields that are not dictionaries are skipped, and bad member types fall back to defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedSchemaFieldsFallBackToDefaults()
    {
        using var document = Open(
            "<< /Type /Collection /Schema << /Bad 42 /Worse (text) /Array [1 2] /NoOrder << /Subtype /S >> "
            + "/Odd << /Subtype 7 /N 9 /O (x) /V 3 /E (no) >> >> >>");

        var schema = PdfDocumentPortfolio.GetPortfolio(document)!.Schema;

        await Assert.That(schema.Count).IsEqualTo(SurvivingFields);
        var odd = schema.Single(static field => field.Key == "Odd");
        await Assert.That(odd.Order).IsEqualTo(0);
        await Assert.That(odd.Visible).IsTrue();
        await Assert.That(odd.Editable).IsFalse();
        await Assert.That(odd.Subtype).IsEmpty();
    }

    /// <summary>A schema, sort, colours, folders, initial document or view of the wrong type reads as absent.</summary>
    /// <param name="collection">The collection dictionary.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<< /Type /Collection /Schema 42 /Sort (x) /Colors [1] /Folders 7 /D 12 /View 9 >>")]
    [Arguments("<< /Type /Collection /Schema [] /Sort [] /Colors [] /Folders [] /D [] /View [] >>")]
    [Arguments("<< /Type /Collection /Sort << /S 5 /A (no) >> /Colors << /Background [1 2] /CardBorder (x) >> >>")]
    [Arguments("<< /Type /Collection /Sort << /S [/A 3 (x) /B] /A [true] >> >>")]
    public async Task DamagedCollectionMembersReadAsAbsent(string collection)
    {
        using var document = Open(collection);

        var portfolio = PdfDocumentPortfolio.GetPortfolio(document)!;

        await Assert.That(portfolio.Schema).IsEmpty();
        await Assert.That(portfolio.RootFolder).IsNull();
        await Assert.That(portfolio.View).IsEqualTo("D");
        await Assert.That(portfolio.Colors?.Background).IsNull();
    }

    /// <summary>A sort with fewer directions than keys sorts the rest ascending.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShortSortDirectionsDefaultToAscending()
    {
        using var document = Open("<< /Type /Collection /Sort << /S [/A /B] /A [false] >> >>");

        var sort = PdfDocumentPortfolio.GetPortfolio(document)!.Sort;

        await Assert.That(sort.Select(static key => key.Ascending)).IsEquivalentTo([false, true]);
        await Assert.That(sort.Select(static key => key.Key)).IsEquivalentTo(["A", "B"]);
    }

    /// <summary>A folder chain much longer than the nesting limit stops at the limit without overflowing the stack.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongFolderChainStopsAtTheNestingLimit()
    {
        var objects = new List<string>
        {
            $"<< /Type /Catalog /Pages 2 0 R {CollectionEntry} >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 10 10] >>",
            $"<< /Type /Collection /Folders {ChainStart} 0 R >>",
        };
        for (var i = 0; i < ChainLength; i++)
        {
            objects.Add($"<< /Type /Folder /ID {i} /Child {ChainStart + i + 1} 0 R >>");
        }

        using var document = PdfDocumentReader.Open(MiniPdf.Build([.. objects]), null);

        var depth = 0;
        for (var folder = PdfDocumentPortfolio.GetPortfolio(document)!.RootFolder; folder is not null; folder = folder.Children.Length > 0 ? folder.Children[0] : null)
        {
            depth++;
        }

        await Assert.That(depth).IsGreaterThan(1);
        await Assert.That(depth).IsLessThanOrEqualTo(NestingLimit);
    }

    /// <summary>Item values of the wrong type are skipped, nested value dictionaries give their <c>/D</c>, and bad folder prefixes mean the root.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedItemsAndFolderKeysReadAsData()
    {
        using var document = StructureDocuments.OpenPage(
            $"{FilesTree} /Collection 4 0 R",
            string.Empty,
            "<< /Type /Collection >>",
            "<< /Type /Filespec /F (a.txt) /CI << /Type /CollectionItem /Plain (one) /Number 3 /Nested << /Type /CollectionSubitem /D (two) /P (x) >> /List [1 2] >> >>");

        var item = PdfDocumentPortfolio.GetPortfolio(document)!.Items.Single();

        await Assert.That(item.Fields["Plain"]).IsEqualTo("one");
        await Assert.That(item.Fields["Number"]).IsEqualTo("3");
        await Assert.That(item.Fields["Nested"]).IsEqualTo("two");
        await Assert.That(item.Fields.ContainsKey("Type")).IsFalse();
        await Assert.That(item.Fields.ContainsKey("List")).IsFalse();
    }

    /// <summary>An item whose <c>/CI</c> is not a dictionary has no values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ItemWithoutADictionaryHasNoValues()
    {
        using var document = StructureDocuments.OpenPage(
            $"{FilesTree} /Collection 4 0 R",
            string.Empty,
            "<< /Type /Collection >>",
            "<< /Type /Filespec /F (a.txt) /CI (nope) >>");

        await Assert.That(PdfDocumentPortfolio.GetPortfolio(document)!.Items.Single().Fields).IsEmpty();
    }

    /// <summary>Folder prefixes in name-tree keys are read only when they are a complete non-negative number.</summary>
    /// <param name="key">The key text.</param>
    /// <param name="expected">The expected folder id, or -1 for none.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<3>file", 3)]
    [Arguments("<12>", 12)]
    [Arguments("<>file", -1)]
    [Arguments("<x>file", -1)]
    [Arguments("<-1>file", -1)]
    [Arguments("<99999999999999999999>file", -1)]
    [Arguments("<3file", -1)]
    [Arguments("plain", -1)]
    [Arguments("", -1)]
    public async Task FolderPrefixIsParsedStrictly(string key, int expected)
    {
        var id = PortfolioReader.ReadFolderId(Encoding.UTF8.GetBytes(key));

        await Assert.That(id ?? -1).IsEqualTo(expected);
    }

    /// <summary>Standard relationships, a second-class name, a missing name and wrongly typed values all read without throwing.</summary>
    /// <param name="entry">The <c>/AFRelationship</c> entry text.</param>
    /// <param name="expected">The relationship reported.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/AFRelationship /Source", "Source")]
    [Arguments("/AFRelationship /Data", "Data")]
    [Arguments("/AFRelationship /Alternative", "Alternative")]
    [Arguments("/AFRelationship /Supplement", "Supplement")]
    [Arguments("/AFRelationship /EncryptedPayload", "EncryptedPayload")]
    [Arguments("/AFRelationship /FormData", "FormData")]
    [Arguments("/AFRelationship /Schema", "Schema")]
    [Arguments("/AFRelationship /Unspecified", "Unspecified")]
    [Arguments("/AFRelationship /ABC_Custom", "ABC_Custom")]
    [Arguments("/AFRelationship /Two#20Words", "Two Words")]
    [Arguments("/AFRelationship /source", "source")]
    [Arguments("", Unspecified)]
    [Arguments("/AFRelationship (Source)", Unspecified)]
    [Arguments("/AFRelationship 5", Unspecified)]
    [Arguments("/AFRelationship [/Source]", Unspecified)]
    [Arguments("/AFRelationship null", Unspecified)]
    public async Task RelationshipNamesAreReadAsWritten(string entry, string expected)
    {
        using var document = StructureDocuments.Open("/AF [4 0 R]", $"<< /Type /Filespec /F (f.bin) {entry} >>");

        var files = PdfDocumentAssociatedFiles.GetAssociatedFiles(document);

        await Assert.That(files.Single().Relationship).IsEqualTo(expected);
    }

    /// <summary>The <c>/AF</c> value may be one dictionary or an array; other values and non-dictionary elements are skipped.</summary>
    /// <param name="entry">The <c>/AF</c> entry text.</param>
    /// <param name="count">The number of files expected.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/AF 4 0 R", 1)]
    [Arguments("/AF [4 0 R 4 0 R]", 2)]
    [Arguments("/AF [4 0 R 5 (x) null [4 0 R]]", 1)]
    [Arguments("/AF 42", 0)]
    [Arguments("/AF (text)", 0)]
    [Arguments("/AF []", 0)]
    [Arguments("", 0)]
    public async Task AfValueShapesAreHandled(string entry, int count)
    {
        using var document = StructureDocuments.Open(entry, "<< /Type /Filespec /F (f.bin) >>");

        await Assert.That(PdfDocumentAssociatedFiles.GetAssociatedFiles(document)).Count().IsEqualTo(count);
    }

    /// <summary>Any dictionary can own associated files; the kind and page given are recorded on each file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnerKindAndPageAreRecorded()
    {
        using var document = StructureDocuments.Open(
            string.Empty,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /AF 5 0 R >>",
            "<< /Type /Filespec /F (f.bin) /AFRelationship /Supplement >>");
        var font = StructureDocuments.Dictionary(document, FontNumber);

        var withPage = PdfDocumentAssociatedFiles.GetAssociatedFiles(document, font, PdfAssociatedOwner.Other, SamplePage);
        var withoutPage = PdfDocumentAssociatedFiles.GetAssociatedFiles(document, font, PdfAssociatedOwner.Document, null);

        await Assert.That(withPage.Single().Owner).IsEqualTo(PdfAssociatedOwner.Other);
        await Assert.That(withPage.Single().PageIndex).IsEqualTo(SamplePage);
        await Assert.That(withoutPage.Single().Owner).IsEqualTo(PdfAssociatedOwner.Document);
        await Assert.That(withoutPage.Single().PageIndex).IsNull();
        await Assert.That(PdfDocumentAssociatedFiles.GetAssociatedFiles(document, font, PdfAssociatedOwner.Other, null).Single().Name).IsEqualTo("f.bin");
        await Assert.That(() => PdfDocumentAssociatedFiles.GetAssociatedFiles(document, null!, PdfAssociatedOwner.Other, null)).Throws<ArgumentNullException>();
    }

    /// <summary>Embedded data prefers <c>/UF</c> over <c>/F</c>, and a specification without <c>/EF</c> has no data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmbeddedDataPrefersUnicodeStream()
    {
        using var document = StructureDocuments.Open(
            "/AF [4 0 R 5 0 R 8 0 R]",
            "<< /Type /Filespec /F (a) /UF (a-unicode) /EF << /F 6 0 R /UF 7 0 R >> >>",
            "<< /Type /Filespec /F (b) >>",
            MiniPdf.Stream("/Type /EmbeddedFile", "plain"),
            MiniPdf.Stream("/Type /EmbeddedFile", "unicode"),
            "<< /Type /Filespec /F (c) /EF 9 >>");

        var files = PdfDocumentAssociatedFiles.GetAssociatedFiles(document);

        await Assert.That(files[0].Name).IsEqualTo("a-unicode");
        await Assert.That(Encoding.ASCII.GetString(files[0].Data!.DecodeToArray())).IsEqualTo("unicode");
        await Assert.That(files[1].Data).IsNull();
        await Assert.That(files[2].Data).IsNull();
    }

    /// <summary>A structure tree that loops back on itself is walked once, and each specification is listed once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopingStructureTreeStops()
    {
        using var document = StructureDocuments.Open(
            "/StructTreeRoot 4 0 R",
            "<< /Type /StructTreeRoot /K 5 0 R >>",
            "<< /Type /StructElem /S /Document /K [5 0 R 6 0 R 5 0 R] /AF [7 0 R] >>",
            "<< /Type /StructElem /S /P /K [4 0 R 5 0 R] /AF 7 0 R >>",
            "<< /Type /Filespec /F (s.txt) /AFRelationship /Alternative >>");

        var all = PdfDocumentAssociatedFiles.GetAllAssociatedFiles(document);

        await Assert.That(all.Count).IsEqualTo(1);
        await Assert.That(all[0].Owner).IsEqualTo(PdfAssociatedOwner.StructureElement);
    }

    /// <summary>The catalog, page, annotation and XObject owners are all reported by the full walk, in that order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FullWalkReportsEveryOwnerKind()
    {
        using var document = StructureDocuments.OpenPage(
            "/AF 4 0 R",
            "/AF 5 0 R /Annots [6 0 R] /Resources << /XObject << /Fm 7 0 R >> >>",
            "<< /Type /Filespec /F (c.bin) >>",
            "<< /Type /Filespec /F (p.bin) >>",
            "<< /Type /Annot /Subtype /Stamp /Rect [0 0 1 1] /AF 8 0 R >>",
            MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 1 1] /AF 9 0 R", string.Empty),
            "<< /Type /Filespec /F (a.bin) >>",
            "<< /Type /Filespec /F (x.bin) >>");

        var all = PdfDocumentAssociatedFiles.GetAllAssociatedFiles(document);

        await Assert.That(all.Select(static file => file.Owner)).IsEquivalentTo(
            [PdfAssociatedOwner.Document, PdfAssociatedOwner.Page, PdfAssociatedOwner.Annotation, PdfAssociatedOwner.XObject]);
    }

    /// <summary>Opens a document whose object 4 is the collection and object 5 an embedded file specification.</summary>
    /// <param name="collection">The collection dictionary text.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string collection) =>
        StructureDocuments.OpenPage(CollectionEntry, string.Empty, collection, "<< /Type /Filespec /F (a.txt) >>");
}
