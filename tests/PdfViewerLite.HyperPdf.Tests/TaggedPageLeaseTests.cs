// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.TestAssets;
using TUnit.Assertions;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks tagged reads remain protected while page edits replace the page tree.</summary>
[NotInParallel]
public sealed class TaggedPageLeaseTests
{
    /// <summary>The number of page edit and undo cycles performed during the tagged reads.</summary>
    private const int EditCount = 300;

    /// <summary>The page duplicated and then restored during each edit cycle.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>The maximum time allowed without completing an edit cycle.</summary>
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Tagged reads and reading-structure parsing hold a page lease across concurrent duplicate edits.</summary>
    /// <param name="testToken">Cancels the test operation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="TimeoutException">An edit cycle does not complete within the progress deadline.</exception>
    [Test]
    public async Task TaggedReadsStayValidDuringRepeatedPageEdits(CancellationToken testToken)
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateTagged());
        var document = test.Document;
        var tagged = DocumentFeature<ITaggedStructureSource>.Get(document)!;
        var pageManager = DocumentFeature<IPageManagementSource>.Get(document)!;
        var initial = new List<TaggedBlock>();

        await Assert.That(tagged.GetTaggedBlocks(0, initial)).IsTrue();
        await Assert.That(initial.Count).IsGreaterThan(0);
        _ = HyperPdfTagged.ReadReadingStructure(document, 0);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        timeout.CancelAfter(OperationTimeout);
        using var progress = new SemaphoreSlim(0, 1);
        var failure = new FirstFailure();
        var running = 1;
        var completed = 0;
        var reader = StartReader(() => ReadRepeatedly(document, tagged, progress, failure, () => Volatile.Read(ref running) != 0, timeout.Token), timeout.Token);

        try
        {
            for (var index = 0; index < EditCount; index++)
            {
                await progress.WaitAsync(timeout.Token);
                await pageManager.PageManager.ApplyAsync(new(PageEditKind.Duplicate, FirstPage, 1), timeout.Token);
                _ = await pageManager.PageManager.UndoAsync(timeout.Token);

                var afterUndo = new List<TaggedBlock>();
                if (!tagged.GetTaggedBlocks(0, afterUndo) || afterUndo.Count == 0)
                {
                    failure.Record($"Tagged read after undo {index} returned no blocks.");
                }

                completed++;
                timeout.CancelAfter(OperationTimeout);
            }
        }
        catch (OperationCanceledException exception) when (timeout.IsCancellationRequested && !testToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Tagged reads and edits stopped after {completed} of {EditCount} cycles.", exception);
        }
        finally
        {
            Volatile.Write(ref running, 0);
            await reader;
        }

        await Assert.That(failure.Value).IsNull();
        await Assert.That(document.PageCount).IsEqualTo(1);

        document.Dispose();
        var afterClose = new List<TaggedBlock>();
        await Assert.That(tagged.GetTaggedBlocks(0, afterClose)).IsFalse();
        await Assert.That(afterClose.Count).IsEqualTo(0);
        await Assert.That(() => HyperPdfTagged.ReadReadingStructure(document, 0)).Throws<ObjectDisposedException>();
    }

    /// <summary>Runs the synchronous reader without occupying the worker needed by edit continuations.</summary>
    /// <param name="read">The bounded reader loop.</param>
    /// <param name="cancellationToken">Cancels queued execution.</param>
    /// <returns>The reader's completion task.</returns>
    private static Task StartReader(Action read, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(static state => ((Action)state!)(), read, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>Reads the stable page until the editing thread finishes.</summary>
    /// <param name="document">The document whose reading structure is parsed.</param>
    /// <param name="tagged">The retained tagged-structure feature.</param>
    /// <param name="progress">Signals that another read completed.</param>
    /// <param name="failure">Collects the first exception or invalid result.</param>
    /// <param name="keepReading">Returns whether the editing thread is still active.</param>
    /// <param name="cancellationToken">Bounds the read loop.</param>
    private static void ReadRepeatedly(
        HyperPdfDocument document,
        ITaggedStructureSource tagged,
        SemaphoreSlim progress,
        FirstFailure failure,
        Func<bool> keepReading,
        CancellationToken cancellationToken)
    {
        while (keepReading() && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var blocks = new List<TaggedBlock>();
                if (!tagged.GetTaggedBlocks(0, blocks) || blocks.Count == 0)
                {
                    failure.Record("Tagged reader returned no blocks during page edits.");
                }

                _ = HyperPdfTagged.ReadReadingStructure(document, 0);
            }
            catch (Exception exception)
            {
                failure.Record($"Tagged reader failed: {exception.GetType().Name}.");
            }

            if (progress.CurrentCount == 0)
            {
                _ = progress.Release();
            }
        }
    }

    /// <summary>Keeps the first concurrent test failure.</summary>
    private sealed class FirstFailure
    {
        /// <summary>The first failure, when one occurs.</summary>
        private string? _value;

        /// <summary>Gets the recorded failure.</summary>
        internal string? Value => Volatile.Read(ref _value);

        /// <summary>Records the message when no earlier failure exists.</summary>
        /// <param name="message">The failure message.</param>
        internal void Record(string message) => _ = Interlocked.CompareExchange(ref _value, message, null);
    }
}
