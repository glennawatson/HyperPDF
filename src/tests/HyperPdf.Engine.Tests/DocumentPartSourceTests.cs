// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the document parts and article threads a file lists for the structure panel (ISO 32000-2 §12.4.3, §14.12).</summary>
public sealed class DocumentPartSourceTests
{
    /// <summary>The pages in the test documents.</summary>
    private const int Pages = 3;

    /// <summary>The page the last bead and the last part are on.</summary>
    private const int LastPage = 2;

    /// <summary>The page the second part starts on.</summary>
    private const int MiddlePage = 1;

    /// <summary>An ordinary blank page.</summary>
    private const string BlankPage = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>";

    /// <summary>The most parts a looping tree may list.</summary>
    private const int LoopLimit = 100;

    /// <summary>Gets a mailing of two parts, the first holding two sheets, and a three-bead article that reads across all three pages.</summary>
    private static byte[] Mailing => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /DPartRoot 11 0 R /Threads [12 0 R] >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
        BlankPage,
        BlankPage,
        BlankPage,
        "<< /Type /DPart /Parent 11 0 R /DParts [[7 0 R] [8 0 R]] /DPM << /Title (Mailing) >> >>",
        "<< /Type /DPart /Parent 6 0 R /DParts [[9 0 R 10 0 R]] /DPM << /Recipient (Ann) /Title (Letter A) >> >>",
        "<< /Type /DPart /Parent 6 0 R /Start 5 0 R /DPM << /Recipient (Bob) >> >>",
        "<< /Type /DPart /Parent 7 0 R /Start 3 0 R >>",
        "<< /Type /DPart /Parent 7 0 R /Start 4 0 R /End 5 0 R >>",
        "<< /Type /DPartRoot /DPartRootNode 6 0 R /RecordLevel 1 /NodeNameList [/Job /Recipient] >>",
        "<< /Type /Thread /F 13 0 R /I << /Title (Story) >> >>",
        "<< /T 12 0 R /N 14 0 R /V 15 0 R /P 3 0 R /R [72 500 300 700] >>",
        "<< /T 12 0 R /N 15 0 R /V 13 0 R /P 4 0 R /R [72 200 300 400] >>",
        "<< /T 12 0 R /N 13 0 R /V 14 0 R /P 5 0 R /R [320 500 540 700] >>");

    /// <summary>Gets a document whose part tree points back at itself, and whose thread ring never ends.</summary>
    private static byte[] Loops => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /DPartRoot 8 0 R /Threads [9 0 R] >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 3 >>",
        BlankPage,
        BlankPage,
        BlankPage,
        "<< /DParts [[7 0 R 6 0 R]] >>",
        "<< /DParts [[6 0 R]] /Start 3 0 R >>",
        "<< /DPartRootNode 6 0 R >>",
        "<< /F 10 0 R >>",
        "<< /N 11 0 R /P 3 0 R /R [0 0 100 100] >>",
        "<< /N 10 0 R /P 4 0 R /R [0 0 100 100] >>");

    /// <summary>The parts keep their order, names, details and page ranges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsPartsWithNamesDetailsAndPages()
    {
        using var test = new HyperPdfTestDocument(Mailing);
        var parts = DocumentFeature<IDocumentPartSource>.Cast(test.Document)!.GetParts();

        await Assert.That(parts.Count).IsEqualTo(Pages - 1);
        await Assert.That(parts[0].Name).IsEqualTo("Letter A");
        await Assert.That(parts[0].Details).IsEqualTo("Recipient: Ann");
        await Assert.That(parts[0].StartPage).IsNull();
        await Assert.That(parts[0].Children.Count).IsEqualTo(Pages - 1);
        await Assert.That(parts[0].Children[0].Name).IsEqualTo("Part 1");
        await Assert.That(parts[0].Children[0].StartPage).IsEqualTo(0);
        await Assert.That(parts[0].Children[0].EndPage).IsEqualTo(0);
        await Assert.That(parts[0].Children[1].StartPage).IsEqualTo(MiddlePage);
        await Assert.That(parts[0].Children[1].EndPage).IsEqualTo(LastPage);
        await Assert.That(parts[1].Name).IsEqualTo("Part 2");
        await Assert.That(parts[1].Details).IsEqualTo("Recipient: Bob");
        await Assert.That(parts[1].StartPage).IsEqualTo(LastPage);
    }

    /// <summary>A three-bead article gives its title and the beads in reading order, each with its page and rectangle.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsArticleBeadsInReadingOrder()
    {
        using var test = new HyperPdfTestDocument(Mailing);
        var threads = DocumentFeature<IDocumentPartSource>.Cast(test.Document)!.GetThreads();

        await Assert.That(threads.Count).IsEqualTo(1);
        await Assert.That(threads[0].Title).IsEqualTo("Story");
        await Assert.That(threads[0].Beads.Select(static bead => bead.PageIndex)).IsEquivalentTo([0, MiddlePage, LastPage]);
        await Assert.That(threads[0].Beads.All(static bead => bead.Bounds is { Width: > 0, Height: > 0 })).IsTrue();
    }

    /// <summary>A document with neither lists nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsNothingWhenThereAreNeither()
    {
        using var test = new HyperPdfTestDocument(1);
        var source = DocumentFeature<IDocumentPartSource>.Cast(test.Document)!;

        await Assert.That(source.GetParts()).IsEmpty();
        await Assert.That(source.GetThreads()).IsEmpty();
    }

    /// <summary>Loops in the part tree and the bead ring stop, and the document's ordinary pages are still there.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoopsStayWithinLimits()
    {
        using var test = new HyperPdfTestDocument(Loops);
        var source = DocumentFeature<IDocumentPartSource>.Cast(test.Document)!;

        var parts = source.GetParts();
        var threads = source.GetThreads();

        await Assert.That(parts.Count).IsLessThan(LoopLimit);
        await Assert.That(threads.Count).IsEqualTo(1);
        await Assert.That(threads[0].Beads.Count).IsEqualTo(Pages - 1);
        await Assert.That(test.Document.PageCount).IsEqualTo(Pages);
    }
}
