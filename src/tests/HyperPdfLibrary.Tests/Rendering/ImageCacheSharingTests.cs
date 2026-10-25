// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that threads needing the same image share one decode, and that waiting and cancelling are safe.</summary>
public sealed class ImageCacheSharingTests
{
    /// <summary>The side of the test images in pixels.</summary>
    private const int Side = 8;

    /// <summary>The threads that ask for the same image.</summary>
    private const int Callers = 8;

    /// <summary>The decodes made when the first fails and a waiter decodes again.</summary>
    private const int FailedThenRetried = 2;

    /// <summary>The longest any wait in these tests lasts.</summary>
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    /// <summary>The time given to waiting callers to queue up behind a slow decode.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(100);

    /// <summary>Many threads asking for one image while it decodes get the same entry from a single decode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SimultaneousRequestsDecodeOnce()
    {
        var cache = new ImageCache();
        var stream = Stream();
        using var probe = new Probe();
        var tasks = new List<Task<ImageEntry?>>();
        for (var i = 0; i < Callers; i++)
        {
            tasks.Add(StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None));
        }

        await probe.Started.Task.WaitAsync(Limit);
        await Task.Delay(Settle);
        probe.Release.Set();
        var entries = await Task.WhenAll(tasks);

        await Assert.That(probe.Decodes).IsEqualTo(1);
        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(entries.Distinct().Count()).IsEqualTo(1);
        foreach (var entry in entries)
        {
            entry!.Release();
        }
    }

    /// <summary>A waiter that cancels stops waiting, and the decode the others share carries on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellingAWaiterDoesNotCancelTheDecode()
    {
        var cache = new ImageCache();
        var stream = Stream();
        using var probe = new Probe();
        using var cancel = new CancellationTokenSource();
        var leader = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await probe.Started.Task.WaitAsync(Limit);
        var waiter = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode, cancel.Token), CancellationToken.None);
        await Task.Delay(Settle);

        await cancel.CancelAsync();
        var cancelled = await Cancelled(waiter);
        probe.Release.Set();
        var entry = await leader;
        var later = cache.Acquire(stream, probe, Probe.Decode);

        await Assert.That(cancelled).IsTrue();
        await Assert.That(entry).IsNotNull();
        await Assert.That(later).IsSameReferenceAs(entry);
        await Assert.That(probe.Decodes).IsEqualTo(1);
        entry!.Release();
        later!.Release();
    }

    /// <summary>When the decode fails, the exception reaches its caller, and a waiter decodes for itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AFailedDecodeLetsAWaiterTryAgain()
    {
        var cache = new ImageCache();
        var stream = Stream();
        using var probe = new Probe { ThrowFirst = true };
        var leader = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await probe.Started.Task.WaitAsync(Limit);
        var waiter = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await Task.Delay(Settle);

        probe.Release.Set();
        var failure = await Fault(leader);
        var entry = await waiter;

        await Assert.That(failure).IsTypeOf<InvalidOperationException>();
        await Assert.That(entry).IsNotNull();
        await Assert.That(probe.Decodes).IsEqualTo(FailedThenRetried);
        entry!.Release();
    }

    /// <summary>A stream that cannot be decoded is decoded once, and the waiters get the same answer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnImageThatCannotBeDecodedIsSharedToo()
    {
        var cache = new ImageCache();
        var stream = Stream();
        using var probe = new Probe { ReturnNull = true };
        var leader = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await probe.Started.Task.WaitAsync(Limit);
        var waiter = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await Task.Delay(Settle);

        probe.Release.Set();

        await Assert.That(await leader).IsNull();
        await Assert.That(await waiter).IsNull();
        await Assert.That(probe.Decodes).IsEqualTo(1);
    }

    /// <summary>Different images decode at the same time: a slow decode does not hold up another stream.</summary>
    /// <param name="testToken">Cancels the test operation.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DifferentImagesDecodeInParallel(CancellationToken testToken)
    {
        var cache = new ImageCache();
        using var both = new CountdownEvent(FailedThenRetried);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(testToken);
        timeout.CancelAfter(Limit);

        // The synchronous decoder rendezvous needs dedicated workers rather than occupied ThreadPool threads.
        var first = StartDecode(() => cache.Acquire(Stream(), both, MeetThenCreate), timeout.Token);
        var second = StartDecode(() => cache.Acquire(Stream(), both, MeetThenCreate), timeout.Token);
        var entries = await Task.WhenAll(first, second).WaitAsync(timeout.Token);
        try
        {
            await Assert.That(entries[0]).IsNotNull();
            await Assert.That(entries[1]).IsNotNull();
            await Assert.That(entries[0]).IsNotSameReferenceAs(entries[1]);
            await Assert.That(cache.Count).IsEqualTo(FailedThenRetried);
        }
        finally
        {
            foreach (var entry in entries)
            {
                entry?.Release();
            }

            cache.Close();
        }
    }

    /// <summary>Async callers wait without holding a thread, share the decode, and can cancel alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncWaitersShareTheDecodeAndCancelAlone()
    {
        var cache = new ImageCache();
        var stream = Stream();
        using var probe = new Probe();
        using var cancel = new CancellationTokenSource();
        var leader = StartDecode(() => cache.AcquireAsync(stream, probe, Probe.Decode, CancellationToken.None).AsTask(), CancellationToken.None);
        await probe.Started.Task.WaitAsync(Limit);
        var patient = cache.AcquireAsync(stream, probe, Probe.Decode, CancellationToken.None);
        var impatient = cache.AcquireAsync(stream, probe, Probe.Decode, cancel.Token);
        await Task.Delay(Settle);

        var patientWaiting = !patient.IsCompleted;
        await cancel.CancelAsync();
        var cancelled = await Cancelled(impatient.AsTask());
        probe.Release.Set();
        var shared = await patient;

        await Assert.That(patientWaiting).IsTrue();
        await Assert.That(cancelled).IsTrue();
        await Assert.That(shared).IsSameReferenceAs(await leader);
        await Assert.That(probe.Decodes).IsEqualTo(1);
        shared!.Release();
        shared.Release();
    }

    /// <summary>Closing the cache while an image decodes fails the caller and the waiters, and the new image is not kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClosingDuringADecodeFailsEveryCaller()
    {
        var cache = new ImageCache();
        var stream = Stream();
        using var probe = new Probe();
        var leader = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await probe.Started.Task.WaitAsync(Limit);
        var waiter = StartDecode(() => cache.Acquire(stream, probe, Probe.Decode), CancellationToken.None);
        await Task.Delay(Settle);

        cache.Close();
        probe.Release.Set();

        await Assert.That(await Fault(leader)).IsTypeOf<ObjectDisposedException>();
        await Assert.That(await Fault(waiter)).IsTypeOf<ObjectDisposedException>();
        await Assert.That(cache.Count).IsEqualTo(0);
        await Assert.That(probe.Created!.IsDisposed).IsTrue();
        var skiaImage = (SkiaRenderImage)probe.Created!.Image;
        await Assert.That(() => skiaImage.Native).Throws<ObjectDisposedException>();
    }

    /// <summary>A decode that needs its own image (a mask that refers back to the image) gets nothing instead of waiting on itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodeThatNeedsItselfDoesNotHang()
    {
        var cache = new ImageCache();
        var stream = Stream();
        var decode = Task.Run(() => cache.Acquire(stream, cache, DecodeNeedingItself));

        await Assert.That(await Task.WhenAny(decode, Task.Delay(Limit)) == decode).IsTrue();
        (await decode)?.Release();
    }

    /// <summary>Starts a synchronous decoder on a dedicated worker.</summary>
    /// <param name="decode">The bounded decode callback.</param>
    /// <param name="cancellationToken">Cancels queued execution.</param>
    /// <returns>The decoder's completion task.</returns>
    private static Task<ImageEntry?> StartDecode(Func<ImageEntry?> decode, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(static state => ((Func<ImageEntry?>)state!)(), decode, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>Starts an async decoder on a dedicated worker, where a leader's synchronous decode may block.</summary>
    /// <param name="decode">The bounded decode callback.</param>
    /// <param name="cancellationToken">Cancels queued execution.</param>
    /// <returns>The decoder's completion task.</returns>
    private static Task<ImageEntry?> StartDecode(Func<Task<ImageEntry?>> decode, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(static state => ((Func<Task<ImageEntry?>>)state!)(), decode, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    /// <summary>Decodes an image whose decode asks the cache for the same image, as a self-referencing mask does.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cache">The cache.</param>
    /// <returns>The entry.</returns>
    private static ImageEntry? DecodeNeedingItself(PdfStream stream, ImageCache cache)
    {
        var inner = cache.Acquire(stream, cache, DecodeNeedingItself);
        return inner is null ? Create() : inner;
    }

    /// <summary>Waits for another decode to start, then creates an image, so it only finishes when decodes overlap.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="both">Signalled by each decode.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="TimeoutException">The two independent decoders did not overlap.</exception>
    private static ImageEntry? MeetThenCreate(PdfStream stream, CountdownEvent both)
    {
        _ = both.Signal();
        if (!both.Wait(Limit))
        {
            throw new TimeoutException("The independent image decoders did not overlap.");
        }

        return Create();
    }

    /// <summary>Awaits a task and reports whether it ended as cancelled.</summary>
    /// <param name="task">The task.</param>
    /// <returns><see langword="true"/> when it was cancelled.</returns>
    private static async Task<bool> Cancelled(Task task)
    {
        try
        {
            await task;
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    /// <summary>Awaits a task that is expected to fail.</summary>
    /// <param name="task">The task.</param>
    /// <returns>The exception, or <see langword="null"/> when it did not fail.</returns>
    private static async Task<Exception?> Fault(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>Creates an empty stream to key the cache with.</summary>
    /// <returns>The stream.</returns>
    private static PdfStream Stream() => new(new PdfDictionary(null), []);

    /// <summary>Creates a small opaque image entry.</summary>
    /// <returns>The entry.</returns>
    private static ImageEntry Create()
    {
        var pixels = new byte[Side * Side * RenderedImage.BytesPerPixel];
        var native = SKImage.FromPixelCopy(new(Side, Side, SKColorType.Bgra8888, SKAlphaType.Premul), pixels, Side * RenderedImage.BytesPerPixel);
        return new(new SkiaRenderImage(native), false, false);
    }

    /// <summary>A decoder the test controls: it counts calls, says when it started, and waits to be released.</summary>
    private sealed class Probe : IDisposable
    {
        /// <summary>The decodes started.</summary>
        private int _decodes;

        /// <summary>Gets the decodes started.</summary>
        internal int Decodes => Volatile.Read(ref _decodes);

        /// <summary>Gets the signal completed when the first decode starts, so the test awaits it without holding a thread.</summary>
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets the event the decoder waits for before it finishes.</summary>
        internal ManualResetEventSlim Release { get; } = new();

        /// <summary>Gets or sets a value indicating whether the first decode throws.</summary>
        internal bool ThrowFirst { get; set; }

        /// <summary>Gets or sets a value indicating whether decodes return null.</summary>
        internal bool ReturnNull { get; set; }

        /// <summary>Gets the last entry the decoder made.</summary>
        internal ImageEntry? Created { get; private set; }

        /// <inheritdoc/>
        public void Dispose() => Release.Dispose();

        /// <summary>Decodes with the probe.</summary>
        /// <param name="stream">The stream.</param>
        /// <param name="probe">The probe.</param>
        /// <returns>The entry.</returns>
        /// <exception cref="InvalidOperationException">The probe is set to throw on its first decode and this is it.</exception>
        internal static ImageEntry? Decode(PdfStream stream, Probe probe)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var number = Interlocked.Increment(ref probe._decodes);
            _ = probe.Started.TrySetResult();
            _ = probe.Release.Wait(Limit);
            if (probe.ThrowFirst && number == 1)
            {
                throw new InvalidOperationException("The decode failed.");
            }

            return probe.ReturnNull ? null : probe.Created = Create();
        }
    }
}
