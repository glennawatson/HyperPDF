// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that edits from several threads, and reads beside them, leave the document whole.</summary>
public sealed class ConcurrentDocumentEditTests
{
    /// <summary>The edits made at once.</summary>
    private const int ParallelEdits = 32;

    /// <summary>The adds made one after another while other threads read.</summary>
    private const int ReadWhileEditing = 150;

    /// <summary>The threads that read while edits run.</summary>
    private const int Readers = 3;

    /// <summary>The note and field pairs added at once.</summary>
    private const int Pairs = 24;

    /// <summary>The size of the form field boxes.</summary>
    private const float BoxSize = 20;

    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 3;

    /// <summary>The page items added for each pair: a note and a widget.</summary>
    private const int ItemsPerPair = 2;

    /// <summary>The first page, as a selection.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>Some bytes to attach.</summary>
    private static readonly byte[] AttachedBytes = [1, 2, 3];

    /// <summary>Many bookmarks added at once all land, because each edit waits for the one before.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParallelBookmarkAddsAllSucceed(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var editor = DocumentFeature<IOutlineEditor>.Cast(test.Document)!;
        var existing = test.Document.GetOutline().Count;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task<DocumentEditResult>[ParallelEdits];
        for (var i = 0; i < tasks.Length; i++)
        {
            var title = $"Bookmark {i}";
            tasks[i] = Task.Run(
                async () =>
                {
                    await start.Task;
                    return await editor.AddBookmarkAsync(0, -1, title, 0, cancellationToken);
                },
                cancellationToken);
        }

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        await Assert.That(string.Join(" | ", results.Where(static r => !r.Succeeded).Select(static r => r.Problem).Distinct())).IsEqualTo(string.Empty);
        await Assert.That(test.Document.GetOutline().Count).IsEqualTo(existing + ParallelEdits);
    }

    /// <summary>A bookmark read straight after an add always includes it, even while other threads read the outline.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutlineReadAfterAnAddIsNeverStale(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var editor = DocumentFeature<IOutlineEditor>.Cast(test.Document)!;
        var stale = await EditWhileReadingAsync(
            () => test.Document.GetOutline().Count,
            async index => (await editor.AddBookmarkAsync(0, -1, $"Bookmark {index}", 0, cancellationToken)).Succeeded,
            cancellationToken);

        await Assert.That(stale).IsEqualTo(0);
    }

    /// <summary>An attachment list read straight after an add always includes it, even while other threads list the attachments.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentListAfterAnAddIsNeverStale(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var editor = DocumentFeature<IAttachmentEditor>.Cast(test.Document)!;
        var source = DocumentFeature<IAttachmentSource>.Cast(test.Document)!;
        var stale = await EditWhileReadingAsync(
            () => source.GetAttachments().Count,
            async index => (await editor.AddAttachmentAsync($"file{index}.txt", AttachedBytes, cancellationToken)).Succeeded,
            cancellationToken);

        await Assert.That(stale).IsEqualTo(0);
    }

    /// <summary>A note added to a page and a form field added to the same page at once both stay on the page.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoteAndFormFieldAddedAtOnceBothStay(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(1));
        var annotations = DocumentFeature<HyperPdf.Core.Annotations.IAnnotationEditor>.Cast(test.Document)!;
        var designer = DocumentFeature<IFormDesigner>.Get(test.Document)!;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new List<Task>(Pairs * ItemsPerPair);
        for (var i = 0; i < Pairs; i++)
        {
            var index = i;
            tasks.Add(Task.Run(
                async () =>
                {
                    await start.Task;
                    _ = annotations.AddNote(0, new(BoxSize, BoxSize * (index + 1)), "note", 0xFFFF00);
                },
                cancellationToken));
            tasks.Add(Task.Run(
                async () =>
                {
                    await start.Task;
                    _ = await designer.CreateFieldAsync(new(FormFieldKind.Text, $"Field{index}", [new(0, new PageRect(BoxSize * (index + 1), BoxSize, BoxSize, BoxSize))]), cancellationToken);
                },
                cancellationToken));
        }

        start.SetResult();
        await Task.WhenAll(tasks);

        var page = PdfDocumentPages.GetPage(test.Document.Document, 0);
        await Assert.That(page.Dictionary.GetArray(KnownName.Annots)!.Count).IsEqualTo(Pairs * ItemsPerPair);
        await Assert.That((await designer.GetFieldsAsync(cancellationToken)).Count).IsEqualTo(Pairs);
    }

    /// <summary>A page edit, an undo and a form data import that find another edit holding the document's transaction say so plainly and change nothing.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsFindingAnOpenTransactionAreRefusedPlainly(CancellationToken cancellationToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(Pages));
        var manager = HyperPdfDocumentPageManagement.GetPageManager(test.Document);
        var importer = new HyperPdfFormDataImporter(test.Document);
        using (PdfDocumentEditing.BeginEdit(test.Document.Document, "Held elsewhere"))
        {
            var apply = await Assert.That(async () => await manager.ApplyAsync(new(PageEditKind.Delete, FirstPage, 0), cancellationToken)).Throws<InvalidOperationException>();
            var undo = await Assert.That(async () => await manager.UndoAsync(cancellationToken)).Throws<InvalidOperationException>();
            var imported = importer.Import("<xfdf xmlns=\"http://ns.adobe.com/xfdf/\"><fields/></xfdf>"u8.ToArray());

            await Assert.That(apply!.Message).IsEqualTo(HyperPdfEditTransactions.EditInProgress);
            await Assert.That(undo!.Message).IsEqualTo(HyperPdfEditTransactions.EditInProgress);
            await Assert.That(imported.Succeeded).IsFalse();
        }

        await manager.ApplyAsync(new(PageEditKind.Delete, FirstPage, 0), cancellationToken);
        await Assert.That(test.Document.PageCount).IsEqualTo(Pages - 1);
    }

    /// <summary>The library refusing an edit is a refusal, but a document closed during the edit is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosedDocumentIsNotReportedAsARefusedEdit()
    {
        await Assert.That(HyperPdfDocumentEdits.IsRefusal(new InvalidOperationException("certified"))).IsTrue();
        await Assert.That(HyperPdfDocumentEdits.IsRefusal(new ArgumentException("no such entry"))).IsTrue();
        await Assert.That(HyperPdfDocumentEdits.IsRefusal(new ObjectDisposedException("document"))).IsFalse();
    }

    /// <summary>Adds one at a time on this thread while other threads read, and counts the adds that a read straight after could not see.</summary>
    /// <param name="read">Reads the count the edits change.</param>
    /// <param name="add">Makes one add; returns whether it succeeded.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>The adds that failed or were not visible to the next read.</returns>
    private static async Task<int> EditWhileReadingAsync(Func<int> read, Func<int, Task<bool>> add, CancellationToken cancellationToken)
    {
        using var stop = new CancellationTokenSource();
        var readers = new Task[Readers];
        for (var r = 0; r < readers.Length; r++)
        {
            // The readers spin until stopped, so they run on their own threads and never starve the edits' continuations.
            readers[r] = DedicatedThread.Run(new ReadLoop(read, stop.Token), ReadUntilStopped, cancellationToken);
        }

        var baseline = read();
        var stale = 0;
        for (var i = 0; i < ReadWhileEditing; i++)
        {
            if (!await add(i) || read() != baseline + i + 1)
            {
                stale++;
            }
        }

        await stop.CancelAsync();
        await Task.WhenAll(readers);
        return stale;
    }

    /// <summary>Reads until stopped.</summary>
    /// <param name="loop">The read and what stops it.</param>
    private static void ReadUntilStopped(ReadLoop loop)
    {
        while (!loop.Stop.IsCancellationRequested)
        {
            _ = loop.Read();
        }
    }

    /// <summary>What one reading thread calls and what stops it.</summary>
    /// <param name="Read">Reads the count the edits change.</param>
    /// <param name="Stop">Stops the loop.</param>
    private sealed record ReadLoop(Func<int> Read, CancellationToken Stop);
}
