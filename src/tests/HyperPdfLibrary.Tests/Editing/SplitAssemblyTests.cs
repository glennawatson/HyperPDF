// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for splitting a document and building one from pages of several.</summary>
public sealed class SplitAssemblyTests
{
    /// <summary>The pages in the document that is split by count.</summary>
    private const int SevenPages = 7;

    /// <summary>The pages in each part when splitting by count.</summary>
    private const int ThreePages = 3;

    /// <summary>The index of the first page of the last part of the seven page document.</summary>
    private const int SeventhPageStart = 6;

    /// <summary>The index of the third page, or of the third document.</summary>
    private const int ThirdIndex = 2;

    /// <summary>The pages in the document that is split and read back.</summary>
    private const int FivePages = 5;

    /// <summary>The pages in each part when splitting the five page document.</summary>
    private const int TwoPages = 2;

    /// <summary>Plans parts by count so that every page is in exactly one part and the last part holds the rest.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlanByPageCountCoversEveryPage()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(SevenPages), null);
        var parts = PdfDocumentSplitting.PlanByPageCount(document, ThreePages);

        await Assert.That(parts.Select(static part => part.FirstPage).ToArray()).IsEquivalentTo([0, ThreePages, SeventhPageStart]);
        await Assert.That(parts.Select(static part => part.PageCount).ToArray()).IsEquivalentTo([ThreePages, ThreePages, 1]);
    }

    /// <summary>Plans parts at the pages the top-level outline entries go to, with a part for pages before the first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlanByOutlineStartsPartsAtTheEntries()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        var outline = PdfDocumentNavigation.GetOutline(document);
        var pages = outline.Select(static item => ((GoToAction)item.Action.Value!).Destination.PageIndex).ToArray();
        var parts = PdfDocumentSplitting.PlanByOutline(document);

        await Assert.That(parts.Sum(static part => part.PageCount)).IsEqualTo(document.PageCount);
        await Assert.That(parts.Select(static part => part.FirstPage)).Contains(pages[0]);
        await Assert.That(parts.Single(part => part.FirstPage == pages[0]).Title).IsEqualTo(outline[0].Title);
        await Assert.That(parts.Single(part => part.FirstPage == pages[1]).Title).IsEqualTo(outline[1].Title);
        for (var i = 1; i < parts.Length; i++)
        {
            await Assert.That(parts[i].FirstPage).IsEqualTo(parts[i - 1].FirstPage + parts[i - 1].PageCount);
        }
    }

    /// <summary>A document without an outline is one part.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlanByOutlineWithoutEntriesIsOnePart()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FivePages), null);
        var parts = PdfDocumentSplitting.PlanByOutline(document);

        await Assert.That(parts.Length).IsEqualTo(1);
        await Assert.That(parts[0].PageCount).IsEqualTo(FivePages);
    }

    /// <summary>Each part becomes a document with its pages in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitMakesOneDocumentPerPart()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FivePages), null);
        var outputs = PdfDocumentSplitting.Split(document, PdfDocumentSplitting.PlanByPageCount(document, TwoPages), CancellationToken.None).ToArray();

        await Assert.That(outputs.Length).IsEqualTo(ThreePages);
        using var second = PdfDocumentReader.Open(outputs[1].Bytes, null);
        await Assert.That(second.PageCount).IsEqualTo(TwoPages);
        await Assert.That(EditingTestDocuments.PageTexts(second)).IsEquivalentTo(EditingTestDocuments.Expected("3 4"));
        using var last = PdfDocumentReader.Open(outputs[^1].Bytes, null);
        await Assert.That(last.PageCount).IsEqualTo(1);
    }

    /// <summary>The asynchronous split gives the same documents and stops when cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SplitAsyncMatchesAndCancels()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FivePages), null);
        var parts = PdfDocumentSplitting.PlanByPageCount(document, TwoPages);
        var counts = new List<int>();
        await foreach (var output in PdfDocumentSplitting.SplitAsync(document, parts, CancellationToken.None))
        {
            using var opened = PdfDocumentReader.Open(output.Bytes, null);
            counts.Add(opened.PageCount);
        }

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(counts).IsEquivalentTo([TwoPages, TwoPages, 1]);
        await Assert.That(async () =>
        {
            await foreach (var unused in PdfDocumentSplitting.SplitAsync(document, parts, cancellation.Token))
            {
                _ = unused;
            }
        }).Throws<OperationCanceledException>();
    }

    /// <summary>A part outside the document is refused before any document is made.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PartsOutsideTheDocumentAreRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(ThreePages), null);

        await Assert.That(() => PdfDocumentSplitting.Split(document, [new(null, ThirdIndex, ThreePages)], CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentSplitting.PlanByPageCount(document, 0)).Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Interleaving takes a group from each document in turn and skips a document that has run out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InterleaveTakesGroupsInTurn()
    {
        var references = PdfDocumentCollation.Interleave([ThreePages, 1, TwoPages], TwoPages);

        await Assert.That(references).IsEquivalentTo([new PdfPageReference(0, 0), new(0, 1), new(1, 0), new(ThirdIndex, 0), new(ThirdIndex, 1), new(0, ThirdIndex)]);
        await Assert.That(PdfDocumentCollation.Concatenate([TwoPages, 1])).IsEquivalentTo([new PdfPageReference(0, 0), new(0, 1), new(1, 0)]);
    }

    /// <summary>A document built from interleaved pages of two documents has those pages in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AssembleBuildsADocumentFromSeveralSources()
    {
        using var first = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(ThreePages), null);
        using var second = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TwoPages), null);
        var references = PdfDocumentCollation.Interleave([ThreePages, TwoPages], 1);
        var bytes = PdfDocumentAssembly.Assemble([first, second], references, CancellationToken.None);
        var asyncBytes = await PdfDocumentAssembly.AssembleAsync([first, second], references, CancellationToken.None);
        using var result = PdfDocumentReader.Open(bytes, null);
        using var asyncResult = PdfDocumentReader.Open(asyncBytes, null);

        await Assert.That(EditingTestDocuments.PageTexts(result)).IsEquivalentTo(EditingTestDocuments.Expected("1 1 2 2 3"));
        await Assert.That(EditingTestDocuments.PageTexts(asyncResult)).IsEquivalentTo(EditingTestDocuments.Expected("1 1 2 2 3"));
    }

    /// <summary>A page listed twice in a row is copied twice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AssembleCopiesARepeatedPageTwice()
    {
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(TwoPages), null);
        var bytes = PdfDocumentAssembly.Assemble([source], [new(0, 1), new(0, 1), new(0, 0)], CancellationToken.None);
        using var result = PdfDocumentReader.Open(bytes, null);

        await Assert.That(EditingTestDocuments.PageTexts(result)).IsEquivalentTo(EditingTestDocuments.Expected("2 2 1"));
    }

    /// <summary>References to documents or pages that do not exist are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadReferencesAreRefused()
    {
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        await Assert.That(() => PdfDocumentAssembly.Assemble([source], [new(1, 0)], CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentAssembly.Assemble([source], [new(0, 1)], CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfDocumentAssembly.Assemble([source], [], CancellationToken.None)).Throws<ArgumentException>();
    }
}
