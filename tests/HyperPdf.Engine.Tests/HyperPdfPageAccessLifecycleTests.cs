// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
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

    /// <summary>The pages in each generated fixture.</summary>
    private const int Pages = 3;

    /// <summary>The page read by all workers.</summary>
    private const int PageIndex = Pages - 1;

    /// <summary>The maximum time for one round to start its readers and stop them again; it only guards against a hang.</summary>
    private const int RoundTimeoutSeconds = 60;

    /// <summary>Closing the actual adapter while page readers run does not fail lock disposal.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">All readers finish before close can overlap them.</exception>
    [Test]
    public async Task ConcurrentReadersAndOwnerCloseDoNotThrowSynchronizationExceptions()
    {
        var failures = new ConcurrentQueue<Exception>();
        var bytes = TestPdf.Create(Pages);
        for (var round = 0; round < Rounds; round++)
        {
            // Each round has its own budget, so a slow machine that needs longer overall is not mistaken for a hang.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(RoundTimeoutSeconds));
            var document = new HyperPdfDocument(PdfDocumentReader.Open(bytes, null), "concurrent-close.pdf");
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
            var run = new ReadRun(document, failures, stop.Token);
            var tasks = new Task[Readers];
            var started = new Task[Readers];
            for (var reader = 0; reader < Readers; reader++)
            {
                var state = new ReaderState(run);
                started[reader] = state.Started.Task;
                tasks[reader] = DedicatedThread.Run(state, ReadUntilStopped, CancellationToken.None);
            }

            try
            {
                await WaitForOverlapAsync(started, run, round, timeout.Token);
                Close(document, failures);
            }
            finally
            {
                await stop.CancelAsync();
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(RoundTimeoutSeconds), CancellationToken.None);
                Close(document, failures);
            }
        }

        await Assert.That(string.Join(Environment.NewLine, failures.Select(static failure => failure.ToString()))).IsEqualTo(string.Empty);
    }

    /// <summary>Waits until every reader has finished a first read and is still reading, so the owner close overlaps them.</summary>
    /// <param name="started">The readers' first-read notifications.</param>
    /// <param name="run">The shared reader state.</param>
    /// <param name="round">The round, for the failure message.</param>
    /// <param name="token">Cancelled when the round has run too long.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">A reader did not start in time or stopped before the close.</exception>
    private static async Task WaitForOverlapAsync(Task[] started, ReadRun run, int round, CancellationToken token)
    {
        try
        {
            await Task.WhenAll(started).WaitAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw new InvalidOperationException(Describe("The readers did not finish a first read in time.", run, round));
        }

        if (Volatile.Read(ref run.Active) == Readers)
        {
            return;
        }

        var reason = token.IsCancellationRequested
            ? "The round timeout expired and stopped the readers."
            : "A reader stopped before owner close could overlap it.";
        throw new InvalidOperationException(Describe(reason, run, round));
    }

    /// <summary>Builds a failure message that says why the readers did not overlap the close.</summary>
    /// <param name="reason">What went wrong.</param>
    /// <param name="run">The shared reader state.</param>
    /// <param name="round">The round.</param>
    /// <returns>The message.</returns>
    private static string Describe(string reason, ReadRun run, int round)
    {
        var first = run.Failures.TryPeek(out var failure) ? failure.ToString() : "none";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{reason} Round {round + 1} of {Rounds}: {Volatile.Read(ref run.Active)} of {Readers} readers active, {run.Failures.Count} queued failures, first failure: {first}");
    }

    /// <summary>Reads page data on one dedicated worker without holding access across an await.</summary>
    /// <param name="state">The reader and shared close state.</param>
    private static void ReadUntilStopped(ReaderState state)
    {
        var run = state.Run;
        _ = Interlocked.Increment(ref run.Active);
        try
        {
            // Readers only stop when told to, so every reader is still reading when the owner closes.
            while (!run.Token.IsCancellationRequested)
            {
                _ = HyperPdf.Engine.HyperPdfNavigation.GetPageLabel(run.Document, PageIndex);
                _ = HyperPdf.Engine.HyperPdfNavigation.GetLinks(run.Document, PageIndex);
                _ = HyperPdf.Engine.HyperPdfText.GetCharacterCount(run.Document, PageIndex);
                _ = state.Started.TrySetResult();
            }
        }
        catch (ObjectDisposedException exception) when (!IsRaisedByLock(exception))
        {
            // Closing a document may reject a read; a rejection raised by the disposed lock itself is a failure.
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

    /// <summary>Gets whether a rejection came from the page access lock rather than the document's closed check.</summary>
    /// <param name="exception">The rejection.</param>
    /// <returns>Whether the lock raised it.</returns>
    private static bool IsRaisedByLock(ObjectDisposedException exception) =>
        exception.StackTrace?.Contains(nameof(ReaderWriterLockSlim), StringComparison.Ordinal) == true;

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
