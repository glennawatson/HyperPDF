// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Tests.Fakes;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="DocumentPool"/>.</summary>
public sealed class DocumentPoolTests
{
    /// <summary>The path of the document a test opens late.</summary>
    private const string LatePath = "late.pdf";

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

    /// <summary>A cached load gives its document at once, without waiting for the engine.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WarmLoadCompletesInline()
    {
        using var pool = new DocumentPool(new FakeEngine());
        var source = pool.Create("warm.pdf", null);
        var first = await source.Load(ImmediateSequencer.Instance).ToTask();

        var warm = source.Load(ImmediateSequencer.Instance).ToTask();

        await Assert.That(warm.IsCompletedSuccessfully).IsTrue();
        await Assert.That(await warm).IsSameReferenceAs(first);
        await Assert.That(first).IsSameReferenceAs(source.Acquire());
    }

    /// <summary>Unsubscribing from a pending load cancels the engine's open and publishes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledLoadPublishesNothing()
    {
        var engine = new DelayedEngine();
        using var pool = new DocumentPool(engine);
        var source = pool.Create(LatePath, null);
        using var cancellation = new CancellationTokenSource();
        var opening = source.Load(ImmediateSequencer.Instance).ToTask(cancellation.Token);
        await Assert.That(opening.IsCompleted).IsFalse();
        await cancellation.CancelAsync();

        await Assert.That(async () => await opening).Throws<OperationCanceledException>();
        await Assert.That(source.IsOpen).IsFalse();
    }

    /// <summary>A document that finishes opening after its source was closed is disposed, not published.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LoadFinishingAfterCloseDisposesTheDocument()
    {
        var engine = new DelayedEngine();
        using var pool = new DocumentPool(engine);
        var source = pool.Create(LatePath, null);
        var opening = source.Load(ImmediateSequencer.Instance).ToTask();
        source.Close();
        var late = new FakeDocument(LatePath, FakeEngine.A4);
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
        _ = await source.Load(ImmediateSequencer.Instance).ToTask();
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
        public IObservable<IDocument> Load(string path, string? password) =>
            Signal.FromAsync(_pending.Task.WaitAsync);

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
        public IDocument OpenWithCertificate(string path, string certificatePath, string certificatePassword)
        {
            CertificateOpens++;
            LastCertificatePath = certificatePath;
            LastPassword = certificatePassword;
            return new FakeDocument(path, FakeEngine.A4);
        }

        /// <inheritdoc/>
        public IObservable<IDocument> LoadWithCertificate(string path, string certificatePath, string certificatePassword) =>
            Signal.Defer(() => Signal.Return(OpenWithCertificate(path, certificatePath, certificatePassword)));
    }
}
