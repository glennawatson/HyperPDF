// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfEditTransaction"/> and <see cref="PdfEditHistory"/>.</summary>
public sealed class EditTransactionTests
{
    /// <summary>Pages in the test document.</summary>
    private const int Pages = 3;

    /// <summary>The last page's index.</summary>
    private const int LastPage = 2;

    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>A small undo depth.</summary>
    private const int SmallDepth = 2;

    /// <summary>Edits made in steps.</summary>
    private const int Steps = 4;

    /// <summary>Undo restores the exact objects, the page cache follows, and redo applies the edit again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UndoRestoresExactObjectsAndPages()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        var firstId = PdfDocumentPages.GetPage(document, 0).Id;

        // An edit made before any transaction is the state undo must give back, instance for instance.
        var edited = PdfDocumentPages.GetPage(document, 0).Dictionary.Clone();
        StoreEditing.Replace(document.Objects, firstId, PdfValue.FromDictionary(edited));
        PdfDocumentPageOperations.ReorderPages(document, [LastPage, 1, 0, Pages, Pages + 1, Pages + LastPage]);
        PdfDocumentPageOperations.DeletePages(document, [1]);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(LastPage);
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoLabel).IsEqualTo("Delete pages");
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsFalse();
        await Assert.That(StoreReading.GetDictionary(document.Objects, firstId)).IsSameReferenceAs(edited);
        await Assert.That(StoreEditing.GetEditedNumbers(document.Objects)).IsEquivalentTo((int[])[firstId.Number]);
        await Assert.That(document.Catalog.GetDictionary(KnownName.Pages)!.GetArray(KnownName.Kids)!.Count).IsEqualTo(LastPage);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("1 2 3 4 5 6"));
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary).IsSameReferenceAs(edited);
        await Assert.That(PdfDocumentNavigation.GetOutline(document).Count).IsEqualTo(LastPage);
        await Assert.That(PdfDocumentEditing.Redo(document)).IsTrue();
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("3 2 1 4 5 6"));
        await Assert.That(PdfDocumentEditing.GetHistory(document).RedoCount).IsEqualTo(1);
    }

    /// <summary>Operations inside one transaction commit and undo together; a new commit clears redo.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OperationsJoinTheOpenTransaction()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        using (var transaction = PdfDocumentEditing.BeginEdit(document, "Tidy"))
        {
            PdfDocumentPageOperations.ReorderPages(document, [LastPage, 1, 0]);
            PdfDocumentPageOperations.SetRotation(document, 0, QuarterTurn);
            PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Tidied" });
            await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("3 2 1"));
            transaction.Commit();
            await Assert.That(transaction.Kinds).IsEqualTo(PdfChangeKinds.PageChanges | PdfChangeKinds.Metadata);
        }

        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(1);
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo("Tidied");
        _ = PdfDocumentEditing.Undo(document);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("1 2 3"));
        await Assert.That(PdfDocumentPages.GetPage(document, LastPage).Rotation).IsEqualTo(0);
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsNull();
        await Assert.That(document.Objects.Trailer.ContainsKey(KnownName.Info)).IsFalse();
        PdfDocumentPageOperations.SetRotation(document, 1, QuarterTurn);
        await Assert.That(PdfDocumentEditing.GetHistory(document).RedoCount).IsEqualTo(0);
    }

    /// <summary>Disposing an open transaction rolls back every change, including ones made directly on the store.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DisposeRollsBack()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        var catalogId = document.Objects.Trailer.GetRaw(KnownName.Root).AsReference();
        using (var transaction = PdfDocumentEditing.BeginEdit(document, "Abandoned"))
        {
            PdfDocumentPageOperations.DeletePages(document, [0]);
            var added = StoreEditing.Add(document.Objects, PdfValue.FromInteger(Pages));
            StoreEditing.Delete(document.Objects, catalogId);
            await Assert.That(StoreReading.GetObject(document.Objects, added).AsInt32()).IsEqualTo(Pages);
            await Assert.That(transaction.ChangedCount).IsGreaterThan(1);
        }

        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
        await Assert.That(document.PageCount).IsEqualTo(Pages);
        await Assert.That(StoreTransactions.GetCurrentTransaction(document.Objects)).IsNull();
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(0);
        await Assert.That(PdfIncrementalWriter.Save(document.Objects).Length).IsGreaterThan(0);
    }

    /// <summary>A failed operation rolls back what it had done.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedOperationLeavesNoTrace()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        await Assert.That(() => PdfDocumentPageOperations.MovePages(document, [0, 0], 0)).Throws<ArgumentException>();
        await Assert.That(StoreEditing.HasEdits(document.Objects)).IsFalse();
        await Assert.That(StoreTransactions.GetCurrentTransaction(document.Objects)).IsNull();
    }

    /// <summary>The undo stack keeps at most its depth, dropping the oldest, and undo is refused while a transaction is open.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DepthBoundsTheUndoStack()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfDocumentEditing.GetHistory(document).Depth = SmallDepth;
        for (var i = 0; i < Steps; i++)
        {
            PdfDocumentPageOperations.RotatePages(document, [0], QuarterTurn);
        }

        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(SmallDepth);
        _ = PdfDocumentEditing.Undo(document);
        _ = PdfDocumentEditing.Undo(document);
        await Assert.That(PdfDocumentEditing.Undo(document)).IsFalse();
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Rotation).IsEqualTo(QuarterTurn + QuarterTurn);
        using var transaction = PdfDocumentEditing.BeginEdit(document, "Open");
        await Assert.That(() => PdfDocumentEditing.Redo(document)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfDocumentEditing.BeginEdit(document, "Second")).Throws<InvalidOperationException>();
    }

    /// <summary>Undo and redo from many threads keep the store consistent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentReadersSeeWholeStates()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        PdfDocumentPageOperations.ReorderPages(document, [LastPage, 1, 0]);
        using var cancellation = new CancellationTokenSource();
        var reader = Task.Run(
        () =>
        {
            var bad = 0;
            while (!cancellation.IsCancellationRequested)
            {
                bad += document.PageCount == Pages ? 0 : 1;
            }

            return bad;
        },
        CancellationToken.None);
        for (var i = 0; i < Steps; i++)
        {
            _ = PdfDocumentEditing.Undo(document);
            _ = PdfDocumentEditing.Redo(document);
        }

        await cancellation.CancelAsync();
        await Assert.That(await reader).IsEqualTo(0);
        await Assert.That(EditingTestDocuments.PageTexts(document)).IsEquivalentTo(EditingTestDocuments.Expected("3 2 1"));
    }
}
