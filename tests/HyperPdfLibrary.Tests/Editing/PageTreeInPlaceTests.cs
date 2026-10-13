// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that inserting and moving pages change the page tree in place and leave it sound.</summary>
public sealed class PageTreeInPlaceTests
{
    /// <summary>The pages the target starts with.</summary>
    private const int StartPages = 3;

    /// <summary>The pages the source holds.</summary>
    private const int SourcePages = 300;

    /// <summary>The pages inserted one at a time.</summary>
    private const int Inserted = 300;

    /// <summary>The step between insertion positions, which spreads the inserts through the tree.</summary>
    private const int Stride = 7;

    /// <summary>A page count that fills a root node.</summary>
    private const int FullRoot = 64;

    /// <summary>The kids of a full root after a page is added and its kids move down.</summary>
    private const int RootKidsAfterGrowth = 2;

    /// <summary>Pages added to a tree that is already two levels deep.</summary>
    private const int ExtraPages = 70;

    /// <summary>The first page moved.</summary>
    private const int MovedFirst = 100;

    /// <summary>The second page moved.</summary>
    private const int MovedSecond = 50;

    /// <summary>Where the first moved page lands.</summary>
    private const int MoveDestination = 10;

    /// <summary>Pages in the document that checks pages are read when first used.</summary>
    private const int LazyPages = 5;

    /// <summary>Many one-page inserts keep the order, the counts, the parents and the fan-out of the tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ManyInsertsKeepTheTreeSoundAndInOrder()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(StartPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(SourcePages), null);
        var expected = new List<string>(EditingTestDocuments.PageTexts(target));
        for (var i = 0; i < Inserted; i++)
        {
            var index = (i * Stride) % (target.PageCount + 1);
            PdfDocumentPageOperations.InsertPages(target, index, source, [i]);
            expected.Insert(index, $"Page {i + 1}");
        }

        await Assert.That(string.Join('|', EditingTestDocuments.PageTexts(target))).IsEqualTo(string.Join('|', expected));
        await Assert.That(PageTreeVerifier.Verify(target.Objects, target.PageCount, out _)).IsTrue();
        await Assert.That(MaxFanOut(target)).IsLessThanOrEqualTo(PdfPageTreeWriter.FanOut);
        await Assert.That(CountPagesWithWrongParent(target)).IsEqualTo(0);
        using var reopened = PdfDocumentReader.Open(PdfIncrementalWriter.Save(target.Objects), null);
        await Assert.That(string.Join('|', EditingTestDocuments.PageTexts(reopened))).IsEqualTo(string.Join('|', expected));
    }

    /// <summary>A root that is full gives its kids to new nodes below it when another page is added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFullRootGrowsDownwardWhenAPageIsAdded()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FullRoot), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);

        PdfDocumentPageOperations.InsertPages(target, target.PageCount, source, [0]);

        var root = target.Catalog.GetDictionary(KnownName.Pages)!;
        await Assert.That(root.GetArray(KnownName.Kids)!.Count).IsEqualTo(RootKidsAfterGrowth);
        await Assert.That(root.GetInt32(KnownName.Count)).IsEqualTo(FullRoot + 1);
        await Assert.That(PageTreeVerifier.Verify(target.Objects, target.PageCount, out _)).IsTrue();
        await Assert.That(CountPagesWithWrongParent(target)).IsEqualTo(0);
        await Assert.That(EditingTestDocuments.PageTexts(target)[FullRoot]).IsEqualTo("Page 1");
    }

    /// <summary>Moving pages in a tree of several levels keeps every page once, in the order asked for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovingPagesKeepsTheTreeSoundAndInOrder()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FullRoot), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(ExtraPages), null);
        for (var i = 0; i < ExtraPages; i++)
        {
            PdfDocumentPageOperations.InsertPages(target, target.PageCount, source, [i]);
        }

        var before = EditingTestDocuments.PageTexts(target);
        int[] moved = [0, MovedFirst, MovedSecond];
        PdfDocumentPageOperations.MovePages(target, moved, MoveDestination);

        var expected = new List<string>(before);
        var pages = new[] { before[0], before[MovedFirst], before[MovedSecond] };
        foreach (var index in moved.OrderDescending())
        {
            expected.RemoveAt(index);
        }

        expected.InsertRange(MoveDestination, pages);
        await Assert.That(string.Join('|', EditingTestDocuments.PageTexts(target))).IsEqualTo(string.Join('|', expected));
        await Assert.That(PageTreeVerifier.Verify(target.Objects, target.PageCount, out _)).IsTrue();
        await Assert.That(CountPagesWithWrongParent(target)).IsEqualTo(0);
    }

    /// <summary>Undoing an insertion that changed the tree in place puts back the earlier tree and pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoPutsBackTheTreeAfterAnInPlaceInsert()
    {
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(StartPages), null);
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var before = string.Join('|', EditingTestDocuments.PageTexts(target));
        PdfDocumentPageOperations.InsertPages(target, 1, source, [0]);

        var undone = PdfDocumentEditing.Undo(target);

        await Assert.That(undone).IsTrue();
        await Assert.That(string.Join('|', EditingTestDocuments.PageTexts(target))).IsEqualTo(before);
        await Assert.That(PageTreeVerifier.Verify(target.Objects, target.PageCount, out _)).IsTrue();
    }

    /// <summary>A plain page is parsed when it is first used, not when the page tree is read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APlainPageIsParsedWhenFirstUsed()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(LazyPages), null);
        var number = PdfDocumentPages.GetPageSet(document).Entries[0].Id.Number;

        await Assert.That(document.PageCount).IsEqualTo(LazyPages);
        await Assert.That(document.Objects.CacheState[number] is null).IsTrue();
        _ = PdfDocumentPages.GetPage(document, 0);
        await Assert.That(document.Objects.CacheState[number] is null).IsFalse();
    }

    /// <summary>Gets the most kids any node of the tree holds.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The count.</returns>
    private static int MaxFanOut(PdfDocument document)
    {
        var most = 0;
        var pending = new Stack<PdfDictionary>();
        pending.Push(document.Catalog.GetDictionary(KnownName.Pages)!);
        while (pending.TryPop(out var node))
        {
            var kids = node.GetArray(KnownName.Kids)!;
            most = Math.Max(most, kids.Count);
            for (var i = 0; i < kids.Count; i++)
            {
                if (kids.GetDictionary(i) is { } kid && kid.GetArray(KnownName.Kids) is not null)
                {
                    pending.Push(kid);
                }
            }
        }

        return most;
    }

    /// <summary>Counts pages whose /Parent does not list them as a kid.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The count.</returns>
    private static int CountPagesWithWrongParent(PdfDocument document)
    {
        var wrong = 0;
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            var kids = page.Dictionary.GetDictionary(KnownName.Parent)?.GetArray(KnownName.Kids);
            var found = false;
            for (var k = 0; kids is not null && k < kids.Count && !found; k++)
            {
                found = ReferenceEquals(kids.GetDictionary(k), page.Dictionary);
            }

            wrong += found ? 0 : 1;
        }

        return wrong;
    }
}
