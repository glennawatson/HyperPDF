// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Speech;

namespace HyperPdf.Platform.MacOS.Audio;

/// <summary>
/// Plays speech through a CoreAudio output queue on the default device. Each clip is queued in short buffers and the
/// output waits until CoreAudio reports every buffer played, then lets the queue drain; cancelling stops it at once.
/// The queue starts asynchronously, so whether it is running says nothing until it has started; counting the played
/// buffers does not depend on that.
/// </summary>
[DebuggerDisplay("AudioQueueOutput: CoreAudio")]
public sealed unsafe class AudioQueueOutput : IAudioOutput
{
    /// <summary>The samples in each queued buffer, a quarter of a second at 24 kHz.</summary>
    private const int BufferSamples = 6_000;

    /// <summary>The property telling whether a queue is running (kAudioQueueProperty_IsRunning, <c>'aqrn'</c>).</summary>
    private const uint IsRunningProperty = 0x6171726E;

    /// <summary>The offset of AudioQueueBuffer.mAudioData.</summary>
    private const int DataOffset = 8;

    /// <summary>The offset of AudioQueueBuffer.mAudioDataByteSize.</summary>
    private const int SizeOffset = 16;

    /// <summary>How often to check whether the queue has finished.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(15);

    /// <summary>The longest the queue may take to drain once its last buffer has played.</summary>
    private static readonly TimeSpan DrainLimit = TimeSpan.FromMilliseconds(500);

    /// <summary>How long past its length a clip may take before playback is given up, if CoreAudio never reports it played.</summary>
    private static readonly TimeSpan PlaybackSlack = TimeSpan.FromSeconds(2);

    /// <summary>Serialises playback.</summary>
    private readonly Lock _gate = new();

    /// <summary>Gets a value indicating whether an output queue can be created.</summary>
    public bool IsAvailable
    {
        get
        {
            var queue = Create(BufferSamples, null);
            if (queue == 0)
            {
                return false;
            }

            _ = NativeMethods.DisposeQueue(queue, 1);
            return true;
        }
    }

    /// <inheritdoc/>
    public Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);

        // Playback waits for the clip to be heard, so it gets its own thread rather than holding a pool thread.
        return Task.Factory.StartNew(
            static state =>
            {
                var (output, clip, token) = ((AudioQueueOutput, SpeechAudio, CancellationToken))state!;
                output.Play(clip, token);
            },
            (this, audio, cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Each clip disposes its own queue.
    }

    /// <summary>Called by CoreAudio as each buffer finishes playing; counts it. The buffers are freed with the queue.</summary>
    /// <param name="userData">The count of played buffers, or zero.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="buffer">The buffer.</param>
    [UnmanagedCallersOnly]
    private static void OnBufferDone(nint userData, nint queue, nint buffer)
    {
        if (userData != 0)
        {
            _ = Interlocked.Increment(ref *(int*)userData);
        }
    }

    /// <summary>Creates an output queue for float samples at a rate.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <param name="played">Where to count played buffers, or <see langword="null"/>.</param>
    /// <returns>The queue, or zero.</returns>
    private static nint Create(int rate, int* played)
    {
        var format = StreamDescription.MonoFloat(rate);
        nint queue = 0;
        return NativeMethods.NewOutputQueue(&format, &OnBufferDone, (nint)played, 0, 0, 0, &queue) == 0 ? queue : 0;
    }

    /// <summary>Waits until every queued buffer has played, the clip has long overrun, or playback is cancelled.</summary>
    /// <param name="played">The count of played buffers.</param>
    /// <param name="buffers">How many buffers were queued.</param>
    /// <param name="duration">The clip's length.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    private static void WaitForBuffers(int* played, int buffers, TimeSpan duration, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var limit = duration + PlaybackSlack;
        while (Volatile.Read(ref *played) < buffers
               && Stopwatch.GetElapsedTime(started) < limit
               && !cancellationToken.WaitHandle.WaitOne(PollInterval))
        {
            // Wait for the queued audio to be heard.
        }
    }

    /// <summary>Waits briefly for the queue to stop once its buffers have played, so the end of the clip is heard.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    private static void WaitToDrain(nint queue, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        while (IsRunning(queue)
               && Stopwatch.GetElapsedTime(started) < DrainLimit
               && !cancellationToken.WaitHandle.WaitOne(PollInterval))
        {
            // Let the output device finish the last buffer.
        }
    }

    /// <summary>Determines whether a queue is still playing.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns><see langword="true"/> while running.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsRunning(nint queue)
    {
        Span<uint> running = [0, sizeof(uint)];
        fixed (uint* value = running)
        {
            return NativeMethods.GetQueueProperty(queue, IsRunningProperty, value, value + 1) == 0 && running[0] != 0;
        }
    }

    /// <summary>Queues every sample in short buffers.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="samples">The samples.</param>
    /// <returns><see langword="true"/> when everything was queued.</returns>
    /// <remarks>The number of buffers is the samples divided into <see cref="BufferSamples"/>, rounded up.</remarks>
    private static bool Enqueue(nint queue, ReadOnlySpan<float> samples)
    {
        for (var start = 0; start < samples.Length; start += BufferSamples)
        {
            var piece = samples.Slice(start, Math.Min(BufferSamples, samples.Length - start));
            var bytes = (uint)(piece.Length * sizeof(float));
            nint buffer = 0;
            if (NativeMethods.AllocateBuffer(queue, bytes, &buffer) != 0)
            {
                return false;
            }

            piece.CopyTo(new(*(void**)(buffer + DataOffset), piece.Length));
            *(uint*)(buffer + SizeOffset) = bytes;
            if (NativeMethods.EnqueueBuffer(queue, buffer, 0, 0) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Plays the clip and waits for it to finish.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops at once.</param>
    private void Play(SpeechAudio audio, CancellationToken cancellationToken)
    {
        if (audio.Samples.Length == 0)
        {
            return;
        }

        lock (_gate)
        {
            // CoreAudio counts played buffers here from its own thread; the queue is disposed synchronously before this
            // frame returns, so no callback outlives it.
            var played = 0;
            var queue = Create(audio.SampleRate, &played);
            if (queue == 0)
            {
                return;
            }

            try
            {
                if (!Enqueue(queue, audio.Samples) || NativeMethods.StartQueue(queue, 0) != 0)
                {
                    return;
                }

                _ = NativeMethods.FlushQueue(queue);
                var buffers = (audio.Samples.Length + BufferSamples - 1) / BufferSamples;
                WaitForBuffers(&played, buffers, audio.Duration, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    _ = NativeMethods.StopQueue(queue, 1);
                    return;
                }

                _ = NativeMethods.StopQueue(queue, 0);
                WaitToDrain(queue, cancellationToken);
            }
            finally
            {
                _ = NativeMethods.DisposeQueue(queue, 1);
            }
        }
    }
}
