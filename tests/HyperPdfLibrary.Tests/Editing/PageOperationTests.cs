// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Writing;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for the page operations on <see cref="PdfDocument"/>.</summary>
public sealed class PageOperationTests
{
    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A half turn.</summary>
    private const int HalfTurn = 180;

    /// <summary>Three quarter turns.</summary>
    private const int ThreeQuarterTurn = 270;

    /// <summary>A rotation that is not normalised.</summary>
    private const int UnnormalisedTurn = 450;

    /// <summary>The third page's index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The fourth page's index.</summary>
    private const int FourthPage = 3;

    /// <summary>The fifth page's index.</summary>
    private const int FifthPage = 4;

    /// <summary>The last page's index.</summary>
    private const int LastPage = 5;

    /// <summary>The position in the parent tree's /Nums of the value for key 1.</summary>
    private const int SecondParentTreeValue = 3;

    /// <summary>The entries of a /Names array holding one pair.</summary>
    private const int OneNamePair = 2;

    /// <summary>Pages in a small flat document.</summary>
    private const int FlatPages = 3;

    /// <summary>Gets the reversed order of the structured document.</summary>
    private static int[] Reversed => [LastPage, FifthPage, FourthPage, ThirdPage, 1, 0];

    /// <summary>A cancelled page operation leaves both source and destination unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledAsyncPageOperationsDoNotEdit()
    {
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var token = cancellation.Token;
        int[] selected = [0];

        await Assert.That(await Cancelled(() => PdfDocumentPageEditing.RotatePagesAsync(target, selected, QuarterTurn, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentPageEditing.DeletePagesAsync(target, selected, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentPageEditing.MovePagesAsync(target, selected, 1, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentPageEditing.InsertPagesAsync(target, 1, source, selected, token).AsTask())).IsTrue();
        await Assert.That(await Cancelled(() => PdfDocumentPageEditing.ExtractPagesAsync(target, selected, token).AsTask())).IsTrue();
        await Assert.That(target.PageCount).IsEqualTo(FlatPages);
        await Assert.That(StoreEditing.HasEdits(target.Objects)).IsFalse();
    }

    /// <summary>Reordering rewrites a flat tree, pushes inherited boxes and rotation down, and keeps labels with their pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderKeepsInheritedAttributesAndLabels()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentPageOperations.ReorderPages(document, Reversed);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("6 5 4 3 2 1"));
        await Assert.That(PdfDocumentPages.GetPage(document, LastPage).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Rotation).IsEqualTo(0);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).MediaBox.Width).IsEqualTo(EditingTestDocuments.WideWidth);
        await Assert.That(PdfDocumentPages.GetPage(document, LastPage).MediaBox.Width).IsEqualTo(EditingTestDocuments.TallWidth);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Resources).IsNotNull();
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("A-4");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, FourthPage)).IsEqualTo("A-1");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, FifthPage)).IsEqualTo("ii");
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, LastPage)).IsEqualTo("i");
        await AssertFlatTree(document);
    }

    /// <summary>The page tree survives an incremental save and a compact rewrite, and old nodes are freed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReorderSurvivesBothWriters()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentPageOperations.ReorderPages(document, Reversed);
        using var incremental = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        foreach (var saved in (PdfDocument[])[incremental, compact])
        {
            await Assert.That(EditingTestDocuments.PageTexts(saved)).IsEquivalentTo(EditingTestDocuments.Expected("6 5 4 3 2 1"));
            await Assert.That(PdfDocumentPages.GetPage(saved, LastPage).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
            await Assert.That(PdfDocumentLabels.GetPageLabel(saved, 0)).IsEqualTo("A-4");
            await AssertFlatTree(saved);
        }

        await Assert.That(StoreReading.GetObject(incremental.Objects, new(EditingTestDocuments.FirstNodeNumber, 0)).IsNull).IsTrue();
    }

    /// <summary>Moving follows FPDF_MovePages: the moved pages, in the order given, start at the destination.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MovePagesPlacesThemAtTheDestination()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentPageOperations.MovePages(document, [LastPage, 0], 1);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("2 6 1 3 4 5"));
    }

    /// <summary>Deleting drops outline leaves and named destinations that led to the page; tags still resolve.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeleteDropsDestinationsAndKeepsTags()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentPageOperations.DeletePages(document, [0]);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("2 3 4 5 6"));
        var outline = PdfDocumentNavigation.GetOutline(document);
        await Assert.That(outline.Count).IsEqualTo(1);
        await Assert.That(outline[0].Title).IsEqualTo("Two");
        await Assert.That(document.Catalog.GetDictionary(KnownName.Outlines)!.GetInt32(KnownName.Count)).IsEqualTo(1);
        await Assert.That(document.Catalog.GetDictionary(KnownName.Dests)!.Count).IsEqualTo(1);
        await Assert.That(document.Catalog.GetDictionary(KnownName.Names)!.GetDictionary(KnownName.Dests)!.GetArray(KnownName.Names)!.Count).IsEqualTo(OneNamePair);
        await Assert.That(PdfDocumentLabels.GetPageLabel(document, 0)).IsEqualTo("ii");

        // The second element names a page that is still in the document, and its parent tree entry still resolves.
        var element = StoreReading.GetDictionary(document.Objects, new(EditingTestDocuments.SecondElementNumber, 0))!;
        await Assert.That(PdfDocumentPages.GetPageIndex(document, element.GetRaw(KnownName.Pg).AsReference())).IsEqualTo(0);
        var parentTree = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!.GetDictionary(KnownName.ParentTree)!.GetArray(KnownName.Nums)!;
        await Assert.That(parentTree.GetArray(SecondParentTreeValue)!.GetDictionary(0)).IsSameReferenceAs(element);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary.GetInt32(KnownName.StructParents)).IsEqualTo(1);
    }

    /// <summary>Deleting every page is refused and changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeletingEveryPageIsRefused()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        await Assert.That(() => PdfDocumentPageOperations.DeletePages(document, [0, 1, ThirdPage])).Throws<ArgumentException>();
        await Assert.That(document.PageCount).IsEqualTo(FlatPages);
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
    }

    /// <summary>Rotation is set absolutely or turned relatively, normalised to quarter turns.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RotationIsNormalised()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        PdfDocumentPageOperations.SetRotation(document, FourthPage, UnnormalisedTurn);
        PdfDocumentPageOperations.RotatePages(document, [0, FourthPage], -QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(document, FourthPage).Rotation).IsEqualTo(0);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Rotation).IsEqualTo(0);
        PdfDocumentPageOperations.RotatePages(document, [1], QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Rotation).IsEqualTo(HalfTurn);
        PdfDocumentPageOperations.SetRotation(document, 1, -QuarterTurn);
        await Assert.That(PdfDocumentPages.GetPage(document, 1).Rotation).IsEqualTo(ThreeQuarterTurn);
    }

    /// <summary>Inserted pages keep their content, boxes and rotation, and links between them name the copies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertCopiesPagesFromAnotherDocument()
    {
        using var source = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        using var target = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        PdfDocumentPageOperations.InsertPages(target, 1, source, [0, LastPage]);
        await Assert.That(EditingTestDocuments.PageTexts(target)).IsEquivalentTo(EditingTestDocuments.Expected("1 1 6 2 3"));
        await Assert.That(PdfDocumentPages.GetPage(target, 1).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
        await Assert.That(PdfDocumentPages.GetPage(target, ThirdPage).MediaBox.Width).IsEqualTo(EditingTestDocuments.WideWidth);
        await Assert.That(PdfDocumentPages.GetPage(target, 1).Dictionary.ContainsKey(KnownName.StructParents)).IsFalse();
        await Assert.That(PdfDocumentPages.GetPage(target, 1).Resources!.GetDictionary(KnownName.Font)).IsNotNull();
        using var saved = PdfDocumentReader.Open(PdfIncrementalWriter.Save(target.Objects), null);
        await Assert.That(EditingTestDocuments.PageTexts(saved)).IsEquivalentTo(EditingTestDocuments.Expected("1 1 6 2 3"));
        await Assert.That(WritingTestDocuments.CountMissing(saved.Objects)).IsEqualTo(0);
    }

    /// <summary>Pages can be inserted from the same document, as copies.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsertFromItselfDuplicatesPages()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(FlatPages), null);
        PdfDocumentPageOperations.InsertPages(document, FlatPages, document, [0]);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("1 2 3 1"));
        await Assert.That(PdfDocumentPages.GetPage(document, FlatPages).Id).IsNotEqualTo(PdfDocumentPages.GetPage(document, 0).Id);
    }

    /// <summary>Extracting pages writes a new document with just those pages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractWritesTheChosenPages()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        using var extracted = PdfDocumentReader.Open(PdfDocumentPageOperations.ExtractPages(document, [FourthPage, 0]), null);
        await Assert.That(EditingTestDocuments.PageTexts(extracted)).IsEquivalentTo(EditingTestDocuments.Expected("4 1"));
        await Assert.That(PdfDocumentPages.GetPage(extracted, 1).Rotation).IsEqualTo(EditingTestDocuments.InheritedRotation);
    }

    /// <summary>An encrypted document stays encrypted, and its reordered pages read back with both writers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncryptedDocumentStaysEncrypted()
    {
        using var document = PdfDocumentReader.Open(WritingTestDocuments.Encrypt(EditingTestDocuments.CreateFlat(FlatPages)), null);
        PdfDocumentPageOperations.ReorderPages(document, [ThirdPage, 0, 1]);
        PdfDocumentPageOperations.SetRotation(document, 0, QuarterTurn);
        using var incremental = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Classic), null);
        foreach (var saved in (PdfDocument[])[incremental, compact])
        {
            await Assert.That(saved.IsEncrypted).IsTrue();
            await Assert.That(EditingTestDocuments.PageTexts(saved)).IsEquivalentTo(EditingTestDocuments.Expected("3 1 2"));
            await Assert.That(PdfDocumentPages.GetPage(saved, 0).Rotation).IsEqualTo(QuarterTurn);
        }
    }

    /// <summary>Reports whether an operation stopped on cancellation.</summary>
    /// <param name="action">The operation.</param>
    /// <returns>Whether it was cancelled.</returns>
    private static async Task<bool> Cancelled(Func<Task> action)
    {
        try
        {
            await action();
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Checks the page tree is one root whose kids are the pages, each pointing back at it.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    private static async Task AssertFlatTree(PdfDocument document)
    {
        var rootRef = document.Catalog.GetRaw(KnownName.Pages).AsReference();
        var root = document.Catalog.GetDictionary(KnownName.Pages)!;
        await Assert.That(root.GetInt32(KnownName.Count)).IsEqualTo(document.PageCount);
        await Assert.That(root.GetArray(KnownName.Kids)!.Count).IsEqualTo(document.PageCount);
        await Assert.That(root.ContainsKey(KnownName.Resources)).IsFalse();
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i).Dictionary;
            await Assert.That(page.GetRaw(KnownName.Parent).AsReference().Number).IsEqualTo(rootRef.Number);
            await Assert.That(page.ContainsKey(KnownName.MediaBox)).IsTrue();
        }
    }
}
