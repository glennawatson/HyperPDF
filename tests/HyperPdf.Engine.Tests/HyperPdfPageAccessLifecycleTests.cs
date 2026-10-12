// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks page readers racing with adapter disposal.</summary>
[NotInParallel]
public sealed class HyperPdfPageAccessLifecycleTests
{
    /// <summary>The independent readers contending with owner disposal.</summary>
    private const int Readers = 8;

    /// <summary>The independently opened documents closed during reads.</summary>
    private const int Rounds = 16;

    /// <summary>The maximum reads per worker when the close operation does not stop it.</summary>
    private const int MaximumReads = 100_000;

    /// <summary>The pages in each generated fixture.</summary>
    private const int Pages = 3;

    /// <summary>The page read by all workers.</summary>
    private const int PageIndex = Pages - 1;

    /// <summary>The maximum time for the regression to complete.</summary>
    private const int TimeoutSeconds = 30;

    /// <summary>Closing the actual adapter while page readers run does not fail lock disposal.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">All readers finish before close can overlap them.</exception>
    [Test]
    public async Task ConcurrentReadersAndOwnerCloseDoNotThrowSynchronizationExceptions()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        var failures = new ConcurrentQueue<Exception>();
        var bytes = TestPdf.Create(Pages);
        for (var round = 0; round < Rounds; round++)
        {
            var document = new HyperPdfDocument(PdfDocumentReader.Open(bytes, null), "concurrent-close.pdf");
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var run = new ReadRun(document, failures, stop.Token);
            var tasks = new Task[Readers];
            var started = new Task[Readers];
            for (var reader = 0; reader < Readers; reader++)
            {
                var state = new ReaderState(run);
                started[reader] = state.Started.Task;
                tasks[reader] = Task.Factory.StartNew(
                    static value => ReadUntilStopped((ReaderState)value!),
                    state,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default);
            }

            try
            {
                await Task.WhenAll(started).WaitAsync(timeout.Token);
                if (Volatile.Read(ref run.Active) == 0)
                {
                    throw new InvalidOperationException("All readers finished before owner close could overlap them.");
                }

                Close(document, failures);
            }
            finally
            {
                await stop.CancelAsync();
                await Task.WhenAll(tasks).WaitAsync(timeout.Token);
                Close(document, failures);
            }
        }

        await Assert.That(string.Join(Environment.NewLine, failures.Select(static failure => failure.ToString()))).IsEqualTo(string.Empty);
    }

    /// <summary>Reads page data on one dedicated worker without holding access across an await.</summary>
    /// <param name="state">The reader and shared close state.</param>
    private static void ReadUntilStopped(ReaderState state)
    {
        var run = state.Run;
        _ = Interlocked.Increment(ref run.Active);
        try
        {
            for (var read = 0; read < MaximumReads && !run.Token.IsCancellationRequested; read++)
            {
                _ = HyperPdf.Engine.HyperPdfNavigation.GetPageLabel(run.Document, PageIndex);
                _ = HyperPdf.Engine.HyperPdfNavigation.GetLinks(run.Document, PageIndex);
                _ = HyperPdf.Engine.HyperPdfText.GetCharacterCount(run.Document, PageIndex);
                _ = state.Started.TrySetResult();
            }
        }
        catch (ObjectDisposedException)
        {
            // Closing a document may reject a read; synchronization failures are retained below.
        }
        catch (Exception exception)
        {
            run.Failures.Enqueue(exception);
        }
        finally
        {
            _ = state.Started.TrySetResult();
            _ = Interlocked.Decrement(ref run.Active);
        }
    }

    /// <summary>Records an actual owner-close failure while permitting all readers to drain.</summary>
    /// <param name="document">The owner being closed.</param>
    /// <param name="failures">The exceptions observed by the regression.</param>
    private static void Close(HyperPdfDocument document, ConcurrentQueue<Exception> failures)
    {
        try
        {
            document.Dispose();
        }
        catch (Exception exception)
        {
            failures.Enqueue(exception);
        }
    }

    /// <summary>The mutable reader count and shared fixture lifetime.</summary>
    /// <param name="document">The adapter read concurrently.</param>
    /// <param name="failures">The exceptions captured from readers and close.</param>
    /// <param name="token">Stops finite reader loops.</param>
    private sealed class ReadRun(HyperPdfDocument document, ConcurrentQueue<Exception> failures, CancellationToken token)
    {
        /// <summary>The workers still reading when close begins.</summary>
        private int _active;

        /// <summary>Gets the worker count used by atomic reader progress checks.</summary>
        internal ref int Active => ref _active;

        /// <summary>Gets the adapter read concurrently.</summary>
        internal HyperPdfDocument Document { get; } = document;

        /// <summary>Gets the token stopping finite reader loops.</summary>
        internal CancellationToken Token { get; } = token;

        /// <summary>Gets the exceptions captured from readers and close.</summary>
        internal ConcurrentQueue<Exception> Failures { get; } = failures;
    }

    /// <summary>One reader's actual-start notification.</summary>
    /// <param name="run">The shared reader state.</param>
    private sealed class ReaderState(ReadRun run)
    {
        /// <summary>Gets the notification completed after the first read, or an earlier reader failure.</summary>
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the shared reader state.</summary>
        internal ReadRun Run { get; } = run;
    }
}
