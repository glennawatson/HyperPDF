// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Reading;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks HyperPDF's managed tagged-structure reader against PDFium. For the tagged blocks (kind, heading level, replacement
/// text, and the characters each block covers, in order) and for <c>/MarkInfo /Marked</c> the two must agree. The structure
/// elements agree on type, alternate text, actual text and marked content id. Two differences are deliberate: PDFium gives an
/// element's own <c>/Lang</c> only, while HyperPDF gives the language that applies, inherited from the nearest ancestor or the catalog;
/// and PDFium gives a marked content id only when <c>/K</c> is a bare number, while HyperPDF reads every form of <c>/K</c>.
/// </summary>
[NotInParallel]
public sealed class TaggedParityTests
{
    /// <summary>The PDFium test document is read the same way: kinds, levels, replacement text and characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesPdfiumOnTheTaggedTestDocument()
    {
        using var pair = new EnginePair(TestPdf.CreateTagged());

        await AssertSameBlocksAsync(pair, 0, "tagged test document");
    }

    /// <summary>Tables, lists, figures, replacement text, notes and unknown types are read as PDFium reads them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchesPdfiumOnMixedStructure()
    {
        using var pair = new EnginePair(TaggedParitySamples.Mixed());

        await AssertSameBlocksAsync(pair, 0, "mixed structure");
    }

    /// <summary>An untagged document gives no blocks on either engine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentsGiveNoBlocks()
    {
        using var pair = new EnginePair(TestPdf.Create(1));
        var blocks = new List<TaggedBlock>();

        await Assert.That(HyperPdfTagged.GetTaggedBlocksNative(pair.HyperPdf, 0, blocks)).IsFalse();
        await Assert.That(blocks.Count).IsEqualTo(0);
        await Assert.That(PdfiumReferenceTaggedBlocks.GetPage(pair.Pdfium, 0).IsTrusted).IsFalse();
    }

    /// <summary>The reading view, which the page's screen reader peer speaks, gets the tagged text in logical order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadingViewSpeaksTheTaggedTextInOrder()
    {
        using var pair = new EnginePair(TestPdf.CreateTagged());
        var page = new ReadingDocument(new NativeTaggedSource(pair.HyperPdf), pair.HyperPdf.GetPageSizes()).GetPage(0);

        await Assert.That(page.Blocks.All(static block => block.IsTagged)).IsTrue();
        await Assert.That(page.Blocks.Select(static block => block.Text)).IsEquivalentTo(
            [TestPdf.TaggedHeading, TestPdf.TaggedFirst, TestPdf.TaggedSecond, TestPdf.TaggedFigure],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    /// <summary>
    /// A tagged reference PDF gives the same blocks on every page as PDFium. Skips when the file is not fetched, and fails when
    /// <c>HYPERPDF_REQUIRE_REFERENCE_PDFS</c> is <c>1</c>.
    /// </summary>
    /// <param name="id">The manifest id.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("verapdf-ua-headings")]
    [Arguments("verapdf-ua-tables")]
    [Arguments("verapdf-ua-notes")]
    [Arguments("pyhanko-ua-and-a")]
    public async Task MatchesPdfiumOnTaggedReferencePdfs(string id)
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(id));
        var trusted = 0;
        for (var page = 0; page < pair.Pdfium.PageCount; page++)
        {
            trusted += await AssertSameBlocksAsync(pair, page, $"{id} page {page}") ? 1 : 0;
        }

        TestContext.Current?.Output.WriteLine($"{id}: {pair.Pdfium.PageCount} pages, {trusted} read from the tags.");
        await Assert.That(trusted).IsGreaterThan(0).Because($"{id} is tagged, so at least one page is read from its tags");
    }

    /// <summary>
    /// <c>/MarkInfo /Marked</c> agrees with <c>FPDFCatalog_IsTagged</c> for every reference PDF that is fetched. The tagged ones
    /// must be marked, so the check is not one-sided. Fails when no reference PDF is there.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MarkInfoMatchesPdfiumForEveryReferencePdf()
    {
        var files = ReferenceFolderFiles();
        var tagged = 0;
        var mismatches = new List<string>();
        foreach (var file in files)
        {
            using var pair = new EnginePair(await File.ReadAllBytesAsync(file));
            var expected = PdfiumReferenceStructure.IsTagged(pair.Pdfium);
            var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(pair.Library, CancellationToken.None);
            tagged += expected ? 1 : 0;
            if (report.IsMarked != expected)
            {
                mismatches.Add($"{Path.GetFileName(file)}: /MarkInfo /Marked {report.IsMarked}, PDFium {expected}");
            }

            if (PdfDocumentTagged.GetStructureTree(pair.Library) is { } tree && tree.IsMarked != expected)
            {
                mismatches.Add($"{Path.GetFileName(file)}: structure tree marked {tree.IsMarked}, PDFium {expected}");
            }
        }

        TestContext.Current?.Output.WriteLine($"Compared {files.Length} files; PDFium treats {tagged} as tagged.");
        await Assert.That(mismatches).IsEmpty();
        await Assert.That(tagged).IsGreaterThan(0).Because("the tagged reference PDFs must be among the files compared");
    }

    /// <summary>
    /// For the tagged reference PDFs, each page's structure elements match PDFium's page tree in order, type, alternate text,
    /// actual text and marked content id. Where PDFium gives an element's own language, HyperPDF gives the same one.
    /// </summary>
    /// <param name="id">The manifest id.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("verapdf-ua-headings")]
    [Arguments("verapdf-ua-tables")]
    [Arguments("verapdf-ua-notes")]
    [Arguments("pyhanko-ua-and-a")]
    public async Task StructureElementsMatchPdfiumOnTaggedReferencePdfs(string id)
    {
        using var pair = new EnginePair(ReferencePdfFile.Read(id));
        var compared = 0;
        for (var page = 0; page < pair.Pdfium.PageCount; page++)
        {
            var expected = new List<PdfiumStructElement>();
            foreach (var root in PdfiumReferenceStructure.GetPageTree(pair.Pdfium, page))
            {
                Flatten(root, expected);
            }

            var actual = PageElements(pair.Library, page);
            compared += expected.Count;
            await Assert.That(actual.Count).IsEqualTo(expected.Count).Because($"{id} page {page}");
            for (var i = 0; i < expected.Count; i++)
            {
                // PDFium reports a marked content id only when /K is a bare number, and -1 for an /MCR dictionary or an array.
                // HyperPDF reads every form, so the ids are compared where PDFium gives one.
                var mcid = expected[i].MarkedContentId >= 0 ? FirstMcid(actual[i]) : -1;
                await Assert.That(Describe(actual[i].MappedType, actual[i].AlternateText, actual[i].ActualText, mcid)).IsEqualTo(
                    Describe(expected[i].Type, expected[i].AlternateText, expected[i].ActualText, expected[i].MarkedContentId)).Because($"{id} page {page} element {i}");
                if (expected[i].Language is { } own)
                {
                    await Assert.That(actual[i].Language).IsEqualTo(own);
                }
            }
        }

        await Assert.That(compared).IsGreaterThan(0).Because($"{id} has structure elements on its pages");
    }

    /// <summary>Gets the PDFs in the reference folder, or skips the test, or fails it when the files are required.</summary>
    /// <returns>The files.</returns>
    /// <exception cref="DirectoryNotFoundException">The files are required but missing.</exception>
    private static string[] ReferenceFolderFiles()
    {
        var folder = Environment.GetEnvironmentVariable(ReferencePdfFile.FolderVariable);
        var files = folder is { Length: > 0 } && Directory.Exists(folder) ? Directory.GetFiles(folder, "*.pdf").Order(StringComparer.Ordinal).ToArray() : [];
        if (files.Length > 0)
        {
            return files;
        }

        const string message = $"No reference PDFs. Fetch the entries of tests/reference-pdfs/manifest.json with scripts/FetchReferencePdfs.cs and set {ReferencePdfFile.FolderVariable}.";
        if (Environment.GetEnvironmentVariable(ReferencePdfFile.RequireVariable) == "1")
        {
            throw new DirectoryNotFoundException(message);
        }

        Skip.Test(message);
        return [];
    }

    /// <summary>Describes an element in the form both engines are compared in.</summary>
    /// <param name="type">The type after role mapping.</param>
    /// <param name="alternateText">The alternate text.</param>
    /// <param name="actualText">The actual text.</param>
    /// <param name="markedContentId">The marked content id, or -1.</param>
    /// <returns>The description.</returns>
    private static string Describe(string type, string? alternateText, string? actualText, int markedContentId) =>
        $"{type}|{alternateText}|{actualText}|{markedContentId}";

    /// <summary>Adds an element and its descendants, depth first.</summary>
    /// <param name="element">The PDFium element.</param>
    /// <param name="output">The elements.</param>
    private static void Flatten(PdfiumStructElement element, List<PdfiumStructElement> output)
    {
        output.Add(element);
        foreach (var child in element.Children)
        {
            Flatten(child, output);
        }
    }

    /// <summary>
    /// Lists the elements of a page the way PDFium's page tree does: an element is there when it, or one of its descendants,
    /// holds marked content on the page. Depth first, in logical order.
    /// </summary>
    /// <param name="document">The managed document.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <returns>The elements.</returns>
    private static List<PdfStructureElement> PageElements(PdfDocument document, int pageIndex)
    {
        var output = new List<PdfStructureElement>();
        if (PdfDocumentTagged.GetStructureTree(document) is { } tree)
        {
            foreach (var root in tree.Roots)
            {
                _ = Collect(root, pageIndex, output);
            }
        }

        return output;
    }

    /// <summary>Adds an element and its descendants on a page when any of them holds the page's marked content.</summary>
    /// <param name="element">The element.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="output">The elements.</param>
    /// <returns><see langword="true"/> when the element or a descendant holds marked content on the page.</returns>
    private static bool Collect(PdfStructureElement element, int pageIndex, List<PdfStructureElement> output)
    {
        var slot = output.Count;
        output.Add(element);
        var holds = element.Kids.Any(kid => kid.Kind == PdfStructureKidKind.MarkedContent && kid.PageIndex == pageIndex);
        foreach (var kid in element.Kids)
        {
            if (kid.Element is { } child)
            {
                holds |= Collect(child, pageIndex, output);
            }
        }

        if (!holds)
        {
            output.RemoveRange(slot, output.Count - slot);
        }

        return holds;
    }

    /// <summary>Gets the first marked content id an element holds directly, as PDFium's single id.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The id, or -1.</returns>
    private static int FirstMcid(PdfStructureElement element)
    {
        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.MarkedContent)
            {
                return kid.Mcid;
            }
        }

        return -1;
    }

    /// <summary>Checks both engines give the same blocks for a page.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    /// <param name="label">What the page is, for failure messages.</param>
    /// <returns>Whether the tags were trusted on both engines.</returns>
    private static async Task<bool> AssertSameBlocksAsync(EnginePair pair, int pageIndex, string label)
    {
        var expected = PdfiumReferenceTaggedBlocks.GetPage(pair.Pdfium, pageIndex);
        var actual = new List<TaggedBlock>();
        var actualTagged = HyperPdfTagged.GetTaggedBlocksNative(pair.HyperPdf, pageIndex, actual);
        var characters = new List<PageCharacter>();
        HyperPdfDocumentCharacterLayout.GetCharacters(pair.HyperPdf, pageIndex, characters);

        await Assert.That(actualTagged).IsEqualTo(expected.IsTrusted).Because(label);
        await Assert.That(actual.Count).IsEqualTo(expected.Blocks.Length).Because(label);
        for (var i = 0; i < expected.Blocks.Length; i++)
        {
            var block = $"{label} block {i}";
            await Assert.That(actual[i].Kind).IsEqualTo(expected.Blocks[i].Kind).Because(block);
            await Assert.That(actual[i].Level).IsEqualTo(expected.Blocks[i].Level).Because(block);
            await Assert.That(actual[i].ReplacementText).IsEqualTo(expected.Blocks[i].ReplacementText).Because(block);
            await Assert.That(TextOf(actual[i].Characters, characters)).IsEqualTo(TextOf(expected.Blocks[i].Characters, expected.Unicodes)).Because($"{block} text");
        }

        return actualTagged;
    }

    /// <summary>Reads the text of HyperPDF characters.</summary>
    /// <param name="indices">The character indices.</param>
    /// <param name="characters">The page's characters.</param>
    /// <returns>The text.</returns>
    private static string TextOf(int[] indices, List<PageCharacter> characters)
    {
        var text = new StringBuilder(indices.Length);
        foreach (var index in indices)
        {
            _ = text.Append((uint)index < (uint)characters.Count ? characters[index].Value : '?');
        }

        return text.ToString();
    }

    /// <summary>Reads the text of PDFium characters.</summary>
    /// <param name="indices">The character indices.</param>
    /// <param name="unicodes">The page's characters.</param>
    /// <returns>The text.</returns>
    private static string TextOf(int[] indices, uint[] unicodes)
    {
        var text = new StringBuilder(indices.Length);
        foreach (var index in indices)
        {
            _ = text.Append((uint)index < (uint)unicodes.Length ? (char)unicodes[index] : '?');
        }

        return text.ToString();
    }
}
