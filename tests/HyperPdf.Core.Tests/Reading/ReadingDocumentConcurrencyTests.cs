// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Tests.Fakes;

namespace HyperPdf.Core.Tests.Reading;

/// <summary>Tests that <see cref="ReadingDocument"/> works pages out at the same time, and each page once.</summary>
public sealed class ReadingDocumentConcurrencyTests
{
    /// <summary>The number of threads asking for the same page.</summary>
    private const int Requesters = 8;

    /// <summary>The characters read for a page when it is worked out once: one read to analyse it and one to sample margins.</summary>
    private const int ReadsForOneAnalysis = 2;

    /// <summary>The longest a step of a test waits before it fails.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    /// <summary>A page that is held while it is read does not stop another page being worked out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeldPageDoesNotBlockAnotherPage()
    {
        using var fake = new FakeDocument("held.pdf", PageBuilder.Letter, PageBuilder.Letter);
        using var layout = new HoldingLayout(fake);
        var document = new ReadingDocument(layout, [PageBuilder.Letter, PageBuilder.Letter]);

        // Each request has its own thread: the hold blocks one, and a busy pool must not delay or cut short either.
        var held = StartOnOwnThread(() => document.GetPage(0));
        try
        {
            await layout.Entered.Task.WaitAsync(Limit);

            var other = await StartOnOwnThread(() => document.GetPage(1)).WaitAsync(Limit);

            await Assert.That(other.PageIndex).IsEqualTo(1);
            await Assert.That(held.IsCompleted).IsFalse();
        }
        finally
        {
            layout.Release.Set();
        }

        var first = await held.WaitAsync(Limit);
        await Assert.That(first.PageIndex).IsEqualTo(0);
    }

    /// <summary>Threads asking for one page at the same time share a single analysis.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SamePageIsWorkedOutOnce()
    {
        using var fake = new FakeDocument("held.pdf", PageBuilder.Letter);
        using var layout = new HoldingLayout(fake);
        var document = new ReadingDocument(layout, [PageBuilder.Letter]);
        var started = 0;

        var requests = new Task<ReadingPage>[Requesters];
        for (var requester = 0; requester < requests.Length; requester++)
        {
            requests[requester] = StartOnOwnThread(() =>
            {
                if (Interlocked.Increment(ref started) == Requesters)
                {
                    layout.Release.Set();
                }

                return document.GetPage(0);
            });
        }

        var pages = await Task.WhenAll(requests).WaitAsync(Limit);

        await Assert.That(pages.Select(static page => (object)page).Distinct(ReferenceEqualityComparer.Instance).Count()).IsEqualTo(1);
        await Assert.That(layout.Reads).IsEqualTo(ReadsForOneAnalysis);
    }

    /// <summary>Starts a page request on a thread of its own, so a busy thread pool cannot delay it.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The task for its page.</returns>
    private static Task<ReadingPage> StartOnOwnThread(Func<ReadingPage> request) =>
        Task.Factory.StartNew(request, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>A layout source whose first read of page 0 waits to be released.</summary>
    /// <param name="document">The document the feature belongs to.</param>
    private sealed class HoldingLayout(IDocument document) : ITextLayoutSource, IDisposable
    {
        /// <summary>The left edge of the page text.</summary>
        private const float Left = 72;

        /// <summary>The top edge of the page text.</summary>
        private const float Top = 160;

        /// <summary>The longest the held read waits; far longer than any step of a test, so only a stuck test reaches it.</summary>
        private static readonly TimeSpan HoldLimit = TimeSpan.FromMinutes(2);

        /// <summary>Whether page 0 has been read yet, as 1 or 0.</summary>
        private int _held;

        /// <summary>The number of reads of page 0.</summary>
        private int _reads;

        /// <inheritdoc/>
        public IDocument Document => document;

        /// <summary>Gets the signal that the held read has started.</summary>
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the signal that lets the held read finish.</summary>
        internal ManualResetEventSlim Release { get; } = new(false);

        /// <summary>Gets the number of reads of page 0.</summary>
        internal int Reads => Volatile.Read(ref _reads);

        /// <inheritdoc/>
        public void Dispose() => Release.Dispose();

        /// <inheritdoc/>
        public void GetCharacters(int pageIndex, List<PageCharacter> output)
        {
            if (pageIndex == 0)
            {
                _ = Interlocked.Increment(ref _reads);
                if (Interlocked.Exchange(ref _held, 1) == 0)
                {
                    _ = Entered.TrySetResult();

                    // The call is synchronous, so the hold waits on the release event with a bound.
                    _ = Release.Wait(HoldLimit);
                }
            }

            output.AddRange(new PageBuilder().Body("Some body text on the page.", Left, Top).Characters);
        }
    }
}
