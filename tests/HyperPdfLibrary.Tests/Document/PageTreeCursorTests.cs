// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Tests page order, inheritance, recovery and bounds while walking page trees.</summary>
public sealed class PageTreeCursorTests
{
    /// <summary>The pages in the wide tree.</summary>
    private const int WidePages = 50;

    /// <summary>The catalog object with a reference to the tree root.</summary>
    private const string CatalogObject = "<< /Type /Catalog /Pages 2 0 R >>";

    /// <summary>A leaf under the root.</summary>
    private const string RootPageObject = "<< /Type /Page /Parent 2 0 R >>";

    /// <summary>The number of header objects before flat-tree pages.</summary>
    private const int HeaderObjects = 2;

    /// <summary>The root object number.</summary>
    private const int RootObjectNumber = 2;

    /// <summary>The first page object number.</summary>
    private const int FirstPageObjectNumber = 3;

    /// <summary>The nested branch object number.</summary>
    private const int BranchObjectNumber = 4;

    /// <summary>The second root page object number.</summary>
    private const int SecondRootPageObjectNumber = 5;

    /// <summary>The first nested page object number.</summary>
    private const int FirstNestedPageObjectNumber = 6;

    /// <summary>The second nested page object number.</summary>
    private const int SecondNestedPageObjectNumber = 7;

    /// <summary>The mixed tree's page count.</summary>
    private const int MixedPages = 4;

    /// <summary>The duplicate tree's page count.</summary>
    private const int DuplicatePages = 3;

    /// <summary>The inherited page width.</summary>
    private const int RootWidth = 300;

    /// <summary>The nested branch's page width.</summary>
    private const int BranchWidth = 200;

    /// <summary>The leaf's own page width.</summary>
    private const int LeafWidth = 50;

    /// <summary>The largest accepted depth of a leaf.</summary>
    private const int LeafDepth = 64;

    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A half turn.</summary>
    private const int HalfTurn = 180;

    /// <summary>Wide siblings retain document order and the root's inherited attributes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WideTreeKeepsOrderAndInheritance()
    {
        using var document = PdfDocumentReader.Open(WideTree(WidePages), null);

        await Assert.That(document.PageCount).IsEqualTo(WidePages);
        for (var i = 0; i < WidePages; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            await Assert.That(page.Index).IsEqualTo(i);
            await Assert.That(page.Id.Number).IsEqualTo(i + FirstPageObjectNumber);
            await Assert.That(page.MediaBox.Right).IsEqualTo(RootWidth);
            await Assert.That(page.Rotation).IsEqualTo(QuarterTurn);
        }
    }

    /// <summary>Mixed siblings keep depth first order and each branch's inherited values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedTreeKeepsBranchAttributes()
    {
        using var document = PdfDocumentReader.Open(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R] /Count 4 /MediaBox [0 0 300 400] /Rotate 90 >>",
                RootPageObject,
                "<< /Type /Pages /Parent 2 0 R /Kids [6 0 R 7 0 R] /Count 2 /MediaBox [0 0 200 400] /Rotate 180 >>",
                RootPageObject,
                "<< /Type /Page /Parent 4 0 R >>",
                "<< /Type /Page /Parent 4 0 R /MediaBox [0 0 50 60] /Rotate 0 >>"), null);

        await Assert.That(document.PageCount).IsEqualTo(MixedPages);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Id.Number).IsEqualTo(FirstNestedPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, HeaderObjects).Id.Number).IsEqualTo(SecondNestedPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, DuplicatePages).Id.Number).IsEqualTo(SecondRootPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Rotation).IsEqualTo(QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Rotation).IsEqualTo(HalfTurn);
        await Assert.That(PdfDocumentPages.GetPage(document, HeaderObjects).Rotation).IsEqualTo(0);
        await Assert.That(PdfDocumentPages.GetPage(document, DuplicatePages).Rotation).IsEqualTo(QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).MediaBox.Right).IsEqualTo(BranchWidth);
        await Assert.That(PdfDocumentPages.GetPage(document, HeaderObjects).MediaBox.Right).IsEqualTo(LeafWidth);
    }

    /// <summary>A leaf at the depth limit is kept, while a deeper intermediate node is cut off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DepthLimitKeepsLeafAndCutsIntermediateNode()
    {
        using var accepted = PdfDocumentReader.Open(DeepTree(LeafDepth), null);
        var diagnostics = new List<PdfDiagnostic>();
        using var cut = PdfDocumentReader.OpenWith(DeepTree(LeafDepth + 1), new() { Diagnostics = diagnostics.Add });

        await Assert.That(accepted.PageCount).IsEqualTo(HeaderObjects);
        await Assert.That(PdfDocumentPages.GetPage(accepted, 0).Id.Number).IsEqualTo(LeafDepth + HeaderObjects);
        await Assert.That(cut.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(cut, 0).Id.Number).IsEqualTo(LeafDepth + BranchObjectNumber);
        await Assert.That(diagnostics.Exists(static diagnostic => diagnostic.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Repeated page references remain repeated, but a repeated intermediate node is skipped.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DuplicateLeavesRemainAndRepeatedIntermediateIsSkipped()
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [3 0 R 3 0 R 4 0 R 4 0 R] /Count 4 >>",
                RootPageObject,
                "<< /Type /Pages /Parent 2 0 R /Kids [5 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 4 0 R >>"),
            new() { Diagnostics = diagnostics.Add });

        await Assert.That(document.PageCount).IsEqualTo(DuplicatePages);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, HeaderObjects).Id.Number).IsEqualTo(SecondRootPageObjectNumber);
        await Assert.That(diagnostics.Exists(static diagnostic => diagnostic.Code == PdfDiagnosticCode.RecursionLimit && diagnostic.ObjectNumber == BranchObjectNumber)).IsTrue();
    }

    /// <summary>Appending a kid during a diagnostic does not extend an active ancestor's original frontier.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppendedKidDoesNotExtendActiveFrontier()
    {
        PdfArray? rootKids = null;
        void AppendOnRecursion(PdfDiagnostic diagnostic)
        {
            if (diagnostic.Code == PdfDiagnosticCode.RecursionLimit)
            {
                rootKids!.Add(PdfValue.FromReference(new(SecondRootPageObjectNumber, 0)));
            }
        }

        using var document = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [4 0 R 3 0 R] /Count 1 >>",
                RootPageObject,
                "<< /Type /Pages /Kids [] /Count 0 >>",
                RootPageObject),
            new() { Diagnostics = AppendOnRecursion });
        rootKids = document.Catalog.GetDictionary(KnownName.Pages)!.GetArray(KnownName.Kids)!;
        rootKids.SetAt(1, PdfValue.FromReference(new(BranchObjectNumber, 0)));
        rootKids.Add(PdfValue.FromReference(new(FirstPageObjectNumber, 0)));

        var pages = PageTreeReader.Read(document.Objects);

        await Assert.That(pages.Length).IsEqualTo(1);
        await Assert.That(pages[0].Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(rootKids.Count).IsEqualTo(BranchObjectNumber);
    }

    /// <summary>Direct cycles stop at the depth limit, then later siblings can still be read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DirectCycleStopsAndContinuesToSibling()
    {
        var diagnostics = new List<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                RootPageObject),
            new() { Diagnostics = diagnostics.Add });
        var branch = new PdfDictionary(document.Objects);
        var kids = new PdfArray(document.Objects);
        kids.Add(PdfValue.FromDictionary(branch));
        kids.Add(PdfValue.FromReference(new(FirstPageObjectNumber, 0)));
        branch.Set(KnownName.Kids, PdfValue.FromArray(kids));
        document.Catalog.GetDictionary(KnownName.Pages)!.GetArray(KnownName.Kids)!.SetAt(0, PdfValue.FromDictionary(branch));

        var pages = PageTreeReader.Read(document.Objects);

        await Assert.That(pages.Length).IsGreaterThan(0);
        await Assert.That(pages[0].Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(diagnostics.Exists(static diagnostic => diagnostic.Code == PdfDiagnosticCode.RecursionLimit)).IsTrue();
    }

    /// <summary>Malformed kids are skipped, and an empty tree falls back to scanning page objects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedKidsAndEmptyTreeRecover()
    {
        using var mixed = PdfDocumentReader.Open(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [null 42 3 0 R] /Count 1 >>",
                RootPageObject), null);
        var diagnostics = new List<PdfDiagnostic>();
        using var empty = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [] /Count 0 >>",
                RootPageObject),
            new() { Diagnostics = diagnostics.Add });

        await Assert.That(mixed.PageCount).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(mixed, 0).Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(empty.PageCount).IsEqualTo(1);
        await Assert.That(diagnostics.Exists(static diagnostic => diagnostic.Code == PdfDiagnosticCode.PageTreeRebuilt)).IsTrue();
    }

    /// <summary>Cancellation after a loop report stops the next node before it is visited.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsTraversalAfterDiagnostic()
    {
        using var cancellation = new CancellationTokenSource();
        void CancelOnRecursion(PdfDiagnostic diagnostic)
        {
            if (diagnostic.Code == PdfDiagnosticCode.RecursionLimit)
            {
                cancellation.Cancel();
            }
        }

        using var document = PdfDocumentReader.OpenWith(
            MiniPdf.Build(
                CatalogObject,
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                RootPageObject),
            new() { CancellationToken = cancellation.Token, Diagnostics = CancelOnRecursion });
        var kids = document.Catalog.GetDictionary(KnownName.Pages)!.GetArray(KnownName.Kids)!;
        kids.Add(PdfValue.FromReference(new(RootObjectNumber, 0)));
        kids.Add(PdfValue.FromReference(new(FirstPageObjectNumber, 0)));

        await Assert.That(() => PageTreeReader.Read(document.Objects)).Throws<OperationCanceledException>();
    }

    /// <summary>Undo restores the original wide page order after a reorder invalidates the page cache.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoRestoresWidePageOrder()
    {
        using var document = PdfDocumentReader.Open(WideTree(WidePages), null);
        var reverse = new int[WidePages];
        for (var i = 0; i < reverse.Length; i++)
        {
            reverse[i] = reverse.Length - 1 - i;
        }

        PdfDocumentPageOperations.ReorderPages(document, reverse);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Id.Number).IsEqualTo(WidePages + HeaderObjects);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Id.Number).IsEqualTo(FirstPageObjectNumber);
        await Assert.That(PdfDocumentPages.GetPage(document, WidePages - 1).Id.Number).IsEqualTo(WidePages + HeaderObjects);
    }

    /// <summary>Builds a flat tree with a large number of siblings.</summary>
    /// <param name="count">The number of pages.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] WideTree(int count)
    {
        var objects = new string[count + HeaderObjects];
        objects[0] = CatalogObject;
        var references = new string[count];
        for (var i = 0; i < count; i++)
        {
            references[i] = string.Create(CultureInfo.InvariantCulture, $"{i + FirstPageObjectNumber} 0 R");
            objects[i + HeaderObjects] = RootPageObject;
        }

        objects[1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{string.Join(' ', references)}] /Count {count} /MediaBox [0 0 300 400] /Rotate 90 >>");
        return MiniPdf.Build(objects);
    }

    /// <summary>Builds a chain of intermediate nodes with a valid page as a root sibling.</summary>
    /// <param name="intermediateCount">The number of intermediate nodes, including the root.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] DeepTree(int intermediateCount)
    {
        var objects = new string[intermediateCount + FirstPageObjectNumber];
        objects[0] = CatalogObject;
        for (var i = 0; i < intermediateCount; i++)
        {
            var sibling = i == 0 ? string.Create(CultureInfo.InvariantCulture, $" {intermediateCount + FirstPageObjectNumber} 0 R") : string.Empty;
            objects[i + 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{i + FirstPageObjectNumber} 0 R{sibling}] /Count 1 >>");
        }

        objects[intermediateCount + 1] = "<< /Type /Page >>";
        objects[intermediateCount + HeaderObjects] = RootPageObject;
        return MiniPdf.Build(objects);
    }
}
