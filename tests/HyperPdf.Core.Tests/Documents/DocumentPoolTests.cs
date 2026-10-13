// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Tests.Fakes;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="DocumentPool"/>.</summary>
public sealed class DocumentPoolTests
{
    /// <summary>The longest a test waits for the clock to move.</summary>
    private static readonly TimeSpan ClockLimit = TimeSpan.FromSeconds(30);

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
        await WaitForClockToPassAsync(sources[0].LastUsed);
        _ = sources[1].Acquire();
        await WaitForClockToPassAsync(sources[1].LastUsed);
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
        const int repetitions = 1000;
        var completed = true;
        var attempts = new AllocationAttempts();
        while (attempts.Next())
        {
            for (var index = 0; index < repetitions; index++)
            {
                var opening = source.AcquireAsync(CancellationToken.None);
                completed &= opening.IsCompletedSuccessfully;
                _ = await opening;
            }

            attempts.Stop();
        }

        var allocated = attempts.Smallest;

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

    /// <summary>A certificate selection is used for both async opening and a later cache reopen.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CertificateSelectionSurvivesCacheReopen()
    {
        const int expectedOpens = 2;
        var engine = new CertificateEngine();
        using var pool = new DocumentPool(engine);
        var source = pool.Create("encrypted.pdf", null);

        source.UseCertificate("recipient.pfx", "secret");
        _ = await source.AcquireAsync(CancellationToken.None);
        source.Reload();
        _ = source.Acquire();

        await Assert.That(engine.CertificateOpens).IsEqualTo(expectedOpens);
        await Assert.That(engine.LastCertificatePath).IsEqualTo("recipient.pfx");
        await Assert.That(engine.LastPassword).IsEqualTo("secret");
        await Assert.That(engine.NormalOpens).IsEqualTo(0);
    }

    /// <summary>
    /// Waits until the clock the pool stamps use has moved past a stamp, so the next acquire is stamped later. The clock
    /// can tick as slowly as every 16 milliseconds, so a fixed short delay does not always separate two stamps.
    /// </summary>
    /// <param name="stamp">The stamp to get past.</param>
    /// <returns>A task.</returns>
    private static async Task WaitForClockToPassAsync(long stamp)
    {
        using var timeout = new CancellationTokenSource(ClockLimit);
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1));
        while (Environment.TickCount64 <= stamp)
        {
            _ = await timer.WaitForNextTickAsync(timeout.Token);
        }
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

    /// <summary>Records the credential path without doing cryptography.</summary>
    private sealed class CertificateEngine : IDocumentEngine, ICertificateDocumentEngine
    {
        /// <inheritdoc/>
        public string Name => "Certificate";

        /// <summary>Gets the number of certificate opens.</summary>
        internal int CertificateOpens { get; private set; }

        /// <summary>Gets the number of password opens.</summary>
        internal int NormalOpens { get; private set; }

        /// <summary>Gets the last certificate path.</summary>
        internal string? LastCertificatePath { get; private set; }

        /// <summary>Gets the last certificate password.</summary>
        internal string? LastPassword { get; private set; }

        /// <inheritdoc/>
        public bool CanOpen(string path) => true;

        /// <inheritdoc/>
        public IDocument Open(string path, string? password)
        {
            NormalOpens++;
            return new FakeDocument(path, FakeEngine.A4);
        }

        /// <inheritdoc/>
        public ValueTask<IDocument> OpenAsync(string path, string? password, CancellationToken cancellationToken) => new(Open(path, password));

        /// <inheritdoc/>
        public IDocument OpenWithCertificate(string path, string certificatePath, string certificatePassword)
        {
            CertificateOpens++;
            LastCertificatePath = certificatePath;
            LastPassword = certificatePassword;
            return new FakeDocument(path, FakeEngine.A4);
        }

        /// <inheritdoc/>
        public ValueTask<IDocument> OpenWithCertificateAsync(string path, string certificatePath, string certificatePassword, CancellationToken cancellationToken) =>
            new(OpenWithCertificate(path, certificatePath, certificatePassword));
    }
}
