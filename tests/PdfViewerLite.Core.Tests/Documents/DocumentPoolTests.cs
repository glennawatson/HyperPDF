// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Tests.Fakes;

namespace PdfViewerLite.Core.Tests.Documents;

/// <summary>Tests for <see cref="DocumentPool"/>.</summary>
public sealed class DocumentPoolTests
{
    /// <summary>Verifies documents are opened lazily and the least recently used are closed beyond capacity.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosesLeastRecentlyUsedBeyondCapacity()
    {
        const int capacity = 2;
        const int sourceCount = 3;
        var engine = new FakeEngine();
        using var pool = new DocumentPool(engine, capacity);
        var sources = new DocumentSource[sourceCount];
        for (var i = 0; i < sourceCount; i++)
        {
            sources[i] = pool.Create($"{i}.pdf", null);
        }

        await Assert.That(engine.Opened.Count).IsEqualTo(0);
        _ = sources[0].Acquire();
        await Task.Delay(1);
        _ = sources[1].Acquire();
        await Task.Delay(1);
        _ = sources[2].Acquire();

        await Assert.That(pool.OpenCount).IsEqualTo(capacity);
        await Assert.That(sources[0].IsOpen).IsFalse();
        await Assert.That(sources[0].PageCount).IsEqualTo(engine.Opened[0].PageCount);
        await Assert.That(engine.Opened[0].IsDisposed).IsTrue();
    }

    /// <summary>Verifies reload closes the document and changes the identifier.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReloadChangesIdentifier()
    {
        var engine = new FakeEngine();
        using var pool = new DocumentPool(engine);
        var source = pool.Create("a.pdf", null);
        _ = source.Acquire();
        var id = source.Id;

        source.Reload();

        await Assert.That(source.Id).IsNotEqualTo(id);
        await Assert.That(source.IsOpen).IsFalse();
        await Assert.That(source.PageCount).IsEqualTo(0);
        const int expectedOpens = 2;
        _ = source.Acquire();
        await Assert.That(engine.Opened.Count).IsEqualTo(expectedOpens);
    }

    /// <summary>A cached async acquire completes inline without managed allocations.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmAsyncAcquireCompletesWithoutAllocating()
    {
        using var pool = new DocumentPool(new FakeEngine());
        var source = pool.Create("warm.pdf", null);
        var first = await source.AcquireAsync(CancellationToken.None);
        _ = await source.AcquireAsync(CancellationToken.None);
        var before = GC.GetAllocatedBytesForCurrentThread();
        const int repetitions = 1000;
        var completed = true;
        for (var index = 0; index < repetitions; index++)
        {
            var opening = source.AcquireAsync(CancellationToken.None);
            completed &= opening.IsCompletedSuccessfully;
            _ = await opening;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(completed).IsTrue();
        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(first).IsSameReferenceAs(source.Acquire());
    }

    /// <summary>A cancelled pending open cannot publish its late document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledAsyncAcquireDisposesLateOpen()
    {
        var engine = new DelayedEngine();
        using var pool = new DocumentPool(engine);
        var source = pool.Create("late.pdf", null);
        using var cancellation = new CancellationTokenSource();
        var opening = source.AcquireAsync(cancellation.Token);
        await Assert.That(opening.IsCompleted).IsFalse();
        await cancellation.CancelAsync();
        var late = new FakeDocument("late.pdf", FakeEngine.A4);
        engine.Complete(late);

        await Assert.That(async () => await opening).Throws<OperationCanceledException>();
        await Assert.That(late.IsDisposed).IsTrue();
        await Assert.That(source.IsOpen).IsFalse();
    }

    /// <summary>Holds one async open until the test publishes a result.</summary>
    private sealed class DelayedEngine : IDocumentEngine
    {
        /// <summary>The pending open.</summary>
        private readonly TaskCompletionSource<IDocument> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc/>
        public string Name => "Delayed";

        /// <inheritdoc/>
        public bool CanOpen(string path) => true;

        /// <inheritdoc/>
        public IDocument Open(string path, string? password) => throw new NotSupportedException();

        /// <inheritdoc/>
        public ValueTask<IDocument> OpenAsync(string path, string? password, CancellationToken cancellationToken) => new(_pending.Task);

        /// <summary>Completes the pending open.</summary>
        /// <param name="document">The opened document.</param>
        internal void Complete(IDocument document) => _ = _pending.TrySetResult(document);
    }
}
