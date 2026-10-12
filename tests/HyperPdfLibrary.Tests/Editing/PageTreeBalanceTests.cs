// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that a rewritten page tree is balanced above <see cref="PdfPageTreeWriter.FanOut"/> pages.</summary>
public sealed class PageTreeBalanceTests
{
    /// <summary>A page count that needs one level of intermediate nodes.</summary>
    private const int TwoLevelPages = 200;

    /// <summary>A page count that needs two levels of intermediate nodes.</summary>
    private const int ThreeLevelPages = 4100;

    /// <summary>A page count that fits in the root.</summary>
    private const int FlatPages = 64;

    /// <summary>A reordered tree keeps every page, in order, with at most the fan-out under any node.</summary>
    /// <param name="pageCount">The number of pages.</param>
    /// <param name="expectedDepth">The expected number of node levels above the pages.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FlatPages, 1)]
    [Arguments(TwoLevelPages, 2)]
    public async Task ReorderedTreeIsBalanced(int pageCount, int expectedDepth)
    {
        using var document = PdfDocumentReader.Open(TestPdf.Create(pageCount), null);
        var before = WritingTestDocuments.PageContents(document.Objects);
        var order = new int[pageCount];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = pageCount - 1 - i;
        }

        PdfDocumentPageOperations.ReorderPages(document, order);
        using var reopened = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);

        await Assert.That(reopened.PageCount).IsEqualTo(pageCount);
        await Assert.That(MaxFanOut(reopened)).IsLessThanOrEqualTo(PdfPageTreeWriter.FanOut);
        await Assert.That(Depth(reopened.Catalog.GetDictionary(KnownName.Pages)!)).IsEqualTo(expectedDepth);
        var after = WritingTestDocuments.PageContents(reopened.Objects);
        await Assert.That(after[0]).IsEquivalentTo(before[pageCount - 1]);
        await Assert.That(after[pageCount - 1]).IsEquivalentTo(before[0]);
        await Assert.That(CountPagesWithWrongParent(reopened)).IsEqualTo(0);
    }

    /// <summary>Each child of a node holds the smallest power of the fan-out that lets the node cover every page, so deep trees stay balanced.</summary>
    /// <param name="pageCount">The pages below the node.</param>
    /// <param name="expected">The pages per child.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(TwoLevelPages, PdfPageTreeWriter.FanOut)]
    [Arguments(FlatPages * FlatPages, PdfPageTreeWriter.FanOut)]
    [Arguments(ThreeLevelPages, FlatPages * FlatPages)]
    public async Task ChildCapacityIsAPowerOfTheFanOut(int pageCount, int expected) =>
        await Assert.That(PdfPageTreeWriter.ChildCapacity(pageCount)).IsEqualTo(expected);

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

    /// <summary>Gets the number of node levels above the pages.</summary>
    /// <param name="node">The root.</param>
    /// <returns>The depth.</returns>
    private static int Depth(PdfDictionary node)
    {
        var kids = node.GetArray(KnownName.Kids)!;
        return kids.GetDictionary(0) is { } first && first.GetArray(KnownName.Kids) is not null ? 1 + Depth(first) : 1;
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
