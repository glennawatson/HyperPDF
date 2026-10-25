// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>An open transaction belongs to the thread that started it: edits from other threads do not join it.</summary>
public sealed class TransactionOwnerThreadTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 2;

    /// <summary>The title another thread tries to set.</summary>
    private const string OtherThreadTitle = "Other thread";

    /// <summary>An edit from another thread is refused, and the owner's transaction stays open and usable.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditFromAnotherThreadIsRefusedWhileATransactionIsOpen(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        using var finished = new ManualResetEventSlim(false);
        using var transaction = PdfDocumentEditing.BeginEdit(document, "Owner edit");

        // The owner waits on an event, not with await, so it is still on the thread that started the transaction when it edits.
        var other = DedicatedThread.Run(new OtherEdit(document, finished), static edit => edit.IsRefused(), cancellationToken);
        finished.Wait(cancellationToken);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Owner" });
        transaction.Commit();
        var refused = await other;

        await Assert.That(refused).IsTrue();
        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo("Owner");
        await Assert.That(PdfDocumentEditing.GetHistory(document).UndoCount).IsEqualTo(1);
    }

    /// <summary>An edit from another thread runs in a transaction of its own once the owner's has ended.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditFromAnotherThreadWorksOnceTheTransactionHasEnded(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(Pages), null);
        using (var transaction = PdfDocumentEditing.BeginEdit(document, "Owner edit"))
        {
            transaction.Commit();
        }

        await DedicatedThread.Run(document, static open => PdfDocumentMetadataEditing.SetMetadata(open, new() { Title = OtherThreadTitle }), cancellationToken);

        await Assert.That(PdfDocumentMetadata.GetInfo(document).Title).IsEqualTo(OtherThreadTitle);
    }

    /// <summary>An edit made on another thread while a transaction is open.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Finished">Set when the edit has been tried.</param>
    private sealed record OtherEdit(PdfDocument Document, ManualResetEventSlim Finished)
    {
        /// <summary>Makes an edit and reports whether it was refused.</summary>
        /// <returns><see langword="true"/> when the edit was refused because another thread's transaction is open.</returns>
        internal bool IsRefused()
        {
            try
            {
                PdfDocumentMetadataEditing.SetMetadata(Document, new() { Title = OtherThreadTitle });
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
            finally
            {
                Finished.Set();
            }
        }
    }
}
