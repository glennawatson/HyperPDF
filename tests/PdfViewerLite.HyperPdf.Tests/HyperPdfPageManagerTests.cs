// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Editing;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks the adapter's transactional page edits and retained import sources.</summary>
public sealed class HyperPdfPageManagerTests
{
    /// <summary>The generated document's page count.</summary>
    private const int PageCount = 3;

    /// <summary>The source page moved to the beginning.</summary>
    private const int LastPage = 2;

    /// <summary>The second book's first page after insertion.</summary>
    private const int ImportedPage = 3;

    /// <summary>The merged document's page count.</summary>
    private const int MergedPageCount = 5;

    /// <summary>The merged outline entry count.</summary>
    private const int OutlineCount = 4;

    /// <summary>The generated page's width.</summary>
    private const float PageWidth = 612;

    /// <summary>The generated page's height.</summary>
    private const float PageHeight = 792;

    /// <summary>The note's horizontal position.</summary>
    private const float NoteX = 100;

    /// <summary>The visible note's vertical position.</summary>
    private const float VisibleY = 100;

    /// <summary>The removed note's vertical position.</summary>
    private const float RemovedY = 150;

    /// <summary>The notes' colour.</summary>
    private const uint NoteColor = 0xFFFF00;

    /// <summary>The unedited page order.</summary>
    private const string OriginalTexts = "Page 1,Page 2,Page 3";

    /// <summary>The author retained through cache invalidation.</summary>
    private const string Author = "Page test author";

    /// <summary>The visible annotation's text.</summary>
    private const string VisibleNote = "Visible page note";

    /// <summary>The removed annotation's text.</summary>
    private const string RemovedNote = "Removed page note";

    /// <summary>The number of overlapping read and edit rounds.</summary>
    private const int ConcurrentRounds = 300;

    /// <summary>The concurrent readers.</summary>
    private const int ReaderCount = 2;

    /// <summary>The small render target's edge.</summary>
    private const int RenderEdge = 32;

    /// <summary>The bytes per BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The maximum wait for readers and edits to make progress.</summary>
    private static readonly TimeSpan ConcurrentLimit = TimeSpan.FromSeconds(30);

    /// <summary>A selection of the first page.</summary>
    private static readonly int[] FirstSelection = [0];

    /// <summary>A selection of the second page.</summary>
    private static readonly int[] SecondSelection = [1];

    /// <summary>A selection of the last page.</summary>
    private static readonly int[] LastSelection = [LastPage];

    /// <summary>An extraction in reverse endpoint order.</summary>
    private static readonly int[] ExtractionSelection = [LastPage, 0];

    /// <summary>A selection of all pages.</summary>
    private static readonly int[] AllSelection = [0, 1, LastPage];

    /// <summary>Every supported edit changes page order or geometry and survives undo and redo.</summary>
    /// <param name="kind">The operation.</param>
    /// <param name="page">The selected page.</param>
    /// <param name="value">The turn or destination.</param>
    /// <param name="expected">The resulting page texts in order.</param>
    /// <param name="label">The history label.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageEditKind.Delete, 1, 0, "Page 1,Page 3", "Delete pages")]
    [Arguments(PageEditKind.Rotate, 1, 90, "Page 1,Page 2,Page 3", "Rotate pages")]
    [Arguments(PageEditKind.Move, 2, 0, "Page 3,Page 1,Page 2", "Move pages")]
    [Arguments(PageEditKind.Duplicate, 1, 3, "Page 1,Page 2,Page 3,Page 2", "Duplicate pages")]
    public async Task EditsHaveOneLabelledHistoryEntry(PageEditKind kind, int page, int value, string expected, string label)
    {
        using var document = Open(EditingTestDocuments.CreateFlat(PageCount));
        var manager = PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document);
        await manager.ApplyAsync(new(kind, new[] { page }, value), CancellationToken.None);
        await Assert.That(Texts(document)).IsEqualTo(expected);
        await Assert.That(manager.UndoLabel).IsEqualTo(label);
        await Assert.That(PdfDocumentEditing.GetHistory(document.Document).UndoCount).IsEqualTo(1);
        await Assert.That(document.PageCount).IsEqualTo(document.Document.PageCount);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.GetHasUnsavedChanges(document)).IsTrue();
        if (kind == PageEditKind.Rotate)
        {
            await Assert.That(PdfViewerLite.HyperPdf.HyperPdfNavigation.GetPageSizes(document)[page].Width).IsEqualTo(PageHeight);
            await Assert.That(PdfViewerLite.HyperPdf.HyperPdfNavigation.GetPageSizes(document)[page].Height).IsEqualTo(PageWidth);
        }

        await Assert.That(await manager.UndoAsync(CancellationToken.None)).IsTrue();
        await Assert.That(Texts(document)).IsEqualTo(OriginalTexts);
        await Assert.That(document.PageCount).IsEqualTo(PageCount);
        await Assert.That(manager.RedoLabel).IsEqualTo(label);
        await Assert.That(await manager.RedoAsync(CancellationToken.None)).IsTrue();
        await Assert.That(Texts(document)).IsEqualTo(expected);
        await using var saved = new MemoryStream();
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.Save(document, saved)).IsTrue();
        using var reopened = new HyperPdfTestDocument(saved.ToArray());
        await Assert.That(reopened.Document.PageCount).IsEqualTo(document.PageCount);
        if (kind != PageEditKind.Rotate)
        {
            return;
        }

        await Assert.That(reopened.Document.GetPageSizes()[page].Width).IsEqualTo(PageHeight);
    }

    /// <summary>Multiple source files form one edit and retain copied fields, outlines and links through redo and saving.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipleImportsRetainStructuresAcrossUndoAndSave()
    {
        var first = Path.Combine(Path.GetTempPath(), $"page-import-{Guid.NewGuid():N}.pdf");
        var second = Path.Combine(Path.GetTempPath(), $"page-import-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(first, CarryTestDocuments.CreateBook());
            await File.WriteAllBytesAsync(second, CarryTestDocuments.CreateBook());
            using var document = Open(EditingTestDocuments.CreateFlat(1));
            await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).InsertAsync(1, [first, second], CancellationToken.None);
            await Assert.That(document.PageCount).IsEqualTo(MergedPageCount);
            await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).UndoLabel).IsEqualTo("Insert pages");
            await Assert.That(PdfDocumentEditing.GetHistory(document.Document).UndoCount).IsEqualTo(1);
            await Assert.That(await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).UndoAsync(CancellationToken.None)).IsTrue();
            await Assert.That(document.PageCount).IsEqualTo(1);
            await Assert.That(await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).RedoAsync(CancellationToken.None)).IsTrue();
            await using var saved = new MemoryStream();
            await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.Save(document, saved)).IsTrue();
            using var reopened = new HyperPdfTestDocument(saved.ToArray());
            await AssertStructures(reopened.Document);
        }
        finally
        {
            File.Delete(first);
            File.Delete(second);
        }
    }

    /// <summary>A failed later import leaves the target and history unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedImportDoesNotPartiallyInsert()
    {
        var source = Path.Combine(Path.GetTempPath(), $"page-import-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(source, CarryTestDocuments.CreateBook());
            using var document = Open(EditingTestDocuments.CreateFlat(PageCount));
            await Assert.That(async () => await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).InsertAsync(1, [source, $"{source}.missing"], CancellationToken.None))
                .Throws<PdfException>();
            await Assert.That(Texts(document)).IsEqualTo(OriginalTexts);
            await Assert.That(PdfDocumentEditing.GetHistory(document.Document).UndoCount).IsEqualTo(0);
        }
        finally
        {
            File.Delete(source);
        }
    }

    /// <summary>Extraction writes the selected order without changing history, and cancellation leaves pages untouched.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtractionAndCancellationDoNotEditTheSource()
    {
        using var document = Open(EditingTestDocuments.CreateFlat(PageCount));
        await using var output = new MemoryStream();
        await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).ExtractAsync(ExtractionSelection, output, CancellationToken.None);
        using var extracted = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(string.Join(',', EditingTestDocuments.PageTexts(extracted))).IsEqualTo("Page 3,Page 1");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.That(async () => await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).ApplyAsync(new(PageEditKind.Delete, SecondSelection, 0), cancellation.Token))
            .Throws<OperationCanceledException>();
        await Assert.That(Texts(document)).IsEqualTo(OriginalTexts);
        await Assert.That(PdfDocumentEditing.GetHistory(document.Document).UndoCount).IsEqualTo(0);
    }

    /// <summary>A retained manager rejects actions after its document closes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosedOwnerRejectsPageActions()
    {
        var document = Open(EditingTestDocuments.CreateFlat(PageCount));
        var manager = PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document);
        document.Dispose();
        await Assert.That(manager.UndoLabel).IsNull();
        await Assert.That(manager.RedoLabel).IsNull();
        await Assert.That(async () => await manager.UndoAsync(CancellationToken.None)).Throws<ObjectDisposedException>();
        await Assert.That(async () => await manager.ApplyAsync(new(PageEditKind.Move, LastSelection, 0), CancellationToken.None))
            .Throws<ObjectDisposedException>();
    }

    /// <summary>An invalid edit rolls back without adding history or changing the adapter's page cache.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectedDeleteLeavesPagesAndHistoryUntouched()
    {
        using var document = Open(EditingTestDocuments.CreateFlat(PageCount));
        await Assert.That(async () => await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).ApplyAsync(
            new(
                PageEditKind.Delete,
                AllSelection,
                0),
            CancellationToken.None))
            .Throws<ArgumentException>();
        await Assert.That(Texts(document)).IsEqualTo(OriginalTexts);
        await Assert.That(document.PageCount).IsEqualTo(PageCount);
        await Assert.That(PdfDocumentEditing.GetHistory(document.Document).UndoCount).IsEqualTo(0);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.GetHasUnsavedChanges(document)).IsFalse();
    }

    /// <summary>Removed annotations follow reordered and duplicated pages through history and stay omitted from saved files.</summary>
    /// <param name="kind">The page operation.</param>
    /// <param name="destination">The new page index.</param>
    /// <param name="expected">The pages carrying the visible annotation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PageEditKind.Move, 2, "2")]
    [Arguments(PageEditKind.Duplicate, 3, "0,3")]
    public async Task RemovedAnnotationsFollowPageEdits(PageEditKind kind, int destination, string expected)
    {
        using var document = Open(EditingTestDocuments.CreateFlat(PageCount));
        PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.SetAuthor(document, Author);
        var kept = PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.AddNote(document, 0, new(NoteX, VisibleY), VisibleNote, NoteColor);
        var removed = PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.AddNote(document, 0, new(NoteX, RemovedY), RemovedNote, NoteColor);
        await Assert.That(kept).IsGreaterThanOrEqualTo(0);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.SetRemoved(document, 0, removed, true)).IsTrue();
        await AssertNotes(document, "0");
        await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).ApplyAsync(new(kind, FirstSelection, destination), CancellationToken.None);
        await AssertNotes(document, expected);
        await Assert.That(await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).UndoAsync(CancellationToken.None)).IsTrue();
        await AssertNotes(document, "0");
        await Assert.That(await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).RedoAsync(CancellationToken.None)).IsTrue();
        await AssertNotes(document, expected);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.GetAuthor(document)).IsEqualTo(Author);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.SetRemoved(document, destination, removed, false)).IsTrue();
        var restored = new List<PageAnnotation>();
        PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.GetAnnotations(document, destination, restored);
        await Assert.That(restored.Exists(static annotation => annotation.Contents == RemovedNote)).IsTrue();
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.SetRemoved(document, destination, removed, true)).IsTrue();
        await using var saved = new MemoryStream();
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.Save(document, saved)).IsTrue();
        using var reopened = new HyperPdfTestDocument(saved.ToArray());
        await AssertNotes(reopened.Document, expected);
    }

    /// <summary>Readers of the last page remain safe while its page index is deleted, restored and moved.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadersRemainSafeDuringPageEdits(CancellationToken cancellationToken)
    {
        using var document = Open(EditingTestDocuments.CreateFlat(PageCount));
        _ = PdfViewerLite.HyperPdf.HyperPdfDocumentAnnotationEditing.AddNote(document, LastPage, new(NoteX, VisibleY), VisibleNote, NoteColor);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConcurrentLimit);
        using var progress = new SemaphoreSlim(0);
        var failures = new ConcurrentQueue<string>();
        var running = 1;
        var readers = new Task[ReaderCount];
        for (var index = 0; index < readers.Length; index++)
        {
            readers[index] = Task.Run(() => ReadWhileEditing(document, progress, failures, () => Volatile.Read(ref running) != 0, timeout.Token), timeout.Token);
        }

        try
        {
            for (var round = 0; round < ConcurrentRounds; round++)
            {
                await progress.WaitAsync(timeout.Token);
                await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).ApplyAsync(new(PageEditKind.Delete, LastSelection, 0), timeout.Token);
                _ = await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).UndoAsync(timeout.Token);
                await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).ApplyAsync(new(PageEditKind.Move, LastSelection, 0), timeout.Token);
                _ = await PdfViewerLite.HyperPdf.HyperPdfDocumentPageManagement.GetPageManager(document).UndoAsync(timeout.Token);
            }
        }
        finally
        {
            Volatile.Write(ref running, 0);
            await Task.WhenAll(readers);
        }

        await Assert.That(failures).IsEmpty();
        await Assert.That(Texts(document)).IsEqualTo(OriginalTexts);
        var count = PdfViewerLite.HyperPdf.HyperPdfText.GetCharacterCount(document, LastPage);
        await Assert.That(PdfViewerLite.HyperPdf.HyperPdfText.GetText(document, LastPage, 0, count)).Contains("Page 3");
        await AssertNotes(document, "2");
    }

    /// <summary>Reads text, links, annotations and pixels until the writer completes, recording the first failure.</summary>
    /// <param name="document">The shared adapter.</param>
    /// <param name="progress">Signals a completed read round.</param>
    /// <param name="failures">Receives actual exceptions.</param>
    /// <param name="keepReading">Whether the edit loop is still running.</param>
    /// <param name="cancellationToken">Bounds the loop.</param>
    private static void ReadWhileEditing(HyperPdfDocument document, SemaphoreSlim progress, ConcurrentQueue<string> failures, Func<bool> keepReading, CancellationToken cancellationToken)
    {
        var pixels = new byte[RenderEdge * RenderEdge * PixelBytes];
        var annotations = new List<PageAnnotation>();
        var info = new PageRenderInfo(LastPage, 1, PageRotation.None, 0, 0, RenderFlags.None);
        while (keepReading() && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                _ = PdfViewerLite.HyperPdf.HyperPdfText.GetCharacterCount(document, LastPage);
                _ = PdfViewerLite.HyperPdf.HyperPdfNavigation.GetLinks(document, LastPage);
                annotations.Clear();
                DocumentFeature<IAnnotationEditor>.Cast(document)!.GetAnnotations(LastPage, annotations);
                _ = PdfViewerLite.HyperPdf.HyperPdfRendering.Render(document, info, new(pixels, RenderEdge, RenderEdge, RenderEdge * PixelBytes));
            }
            catch (Exception exception)
            {
                if (failures.IsEmpty)
                {
                    failures.Enqueue(exception.ToString());
                }
            }

            _ = progress.Release();
        }
    }

    /// <summary>Checks which pages contain a visible note and that every removed note stays hidden.</summary>
    /// <param name="document">The current or saved document.</param>
    /// <param name="expected">The expected page indexes.</param>
    /// <returns>A task.</returns>
    private static async Task AssertNotes(HyperPdfDocument document, string expected)
    {
        var pages = new List<int>();
        var notes = new List<PageAnnotation>();
        for (var page = 0; page < document.PageCount; page++)
        {
            notes.Clear();
            DocumentFeature<IAnnotationEditor>.Cast(document)!.GetAnnotations(page, notes);
            await Assert.That(notes.Exists(static annotation => annotation.Contents == RemovedNote)).IsFalse();
            if (notes.Exists(static annotation => annotation.Contents == VisibleNote))
            {
                pages.Add(page);
            }
        }

        await Assert.That(string.Join(',', pages)).IsEqualTo(expected);
    }

    /// <summary>Checks the carried form, outline and page-link targets after reopening.</summary>
    /// <param name="document">The reopened saved document.</param>
    /// <returns>A task.</returns>
    private static async Task AssertStructures(HyperPdfDocument document)
    {
        await Assert.That(document.PageCount).IsEqualTo(MergedPageCount);
        await Assert.That(document.GetOutline().Count).IsEqualTo(OutlineCount);
        var fields = new List<FormField>();
        DocumentFeature<IFormFiller>.Cast(document)!.GetFields(ImportedPage, fields);
        await Assert.That(string.Join(',', fields.ConvertAll(static field => field.Name))).IsEqualTo("Name_1,Address_1.Street");
        var links = new List<int>();
        foreach (var link in document.GetLinks(ImportedPage))
        {
            links.Add(link.Target.PageIndex);
        }

        await Assert.That(string.Join(',', links)).IsEqualTo("4,4");
    }

    /// <summary>Opens an in-memory adapter for a generated PDF.</summary>
    /// <param name="bytes">The generated PDF.</param>
    /// <returns>The owned adapter.</returns>
    private static HyperPdfDocument Open(byte[] bytes) => new(SkiaTestDocuments.Open(bytes), "memory.pdf");

    /// <summary>Gets the current page text in page order.</summary>
    /// <param name="document">The edited adapter.</param>
    /// <returns>The comma-separated page text.</returns>
    private static string Texts(HyperPdfDocument document) => string.Join(',', EditingTestDocuments.PageTexts(document.Document));
}
