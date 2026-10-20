// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdf.Core.Media;

namespace HyperPdf.Platform.MacOS.Audio;

/// <summary>
/// Plays a continuous stream of sound through a CoreAudio output queue on the default device. A small ring of buffers is reused:
/// a write waits for CoreAudio to hand a buffer back, so the caller stays just ahead of what is heard. The latency is the sound
/// in the buffers CoreAudio has not handed back yet.
/// </summary>
[DebuggerDisplay("AudioQueueStreamingOutput: CoreAudio")]
public sealed unsafe class AudioQueueStreamingOutput : IStreamingAudioOutput
{
    /// <summary>The buffers in the ring.</summary>
    private const int BufferCount = 8;

    /// <summary>The seconds of sound in each buffer.</summary>
    private const double BufferSeconds = 0.025;

    /// <summary>The offset of AudioQueueBuffer.mAudioData.</summary>
    private const int DataOffset = 8;

    /// <summary>The offset of AudioQueueBuffer.mAudioDataByteSize.</summary>
    private const int SizeOffset = 16;

    /// <summary>The rate used to find out whether an output queue can be created.</summary>
    private const int ProbeRate = 48_000;

    /// <summary>The channels used to find out whether an output queue can be created.</summary>
    private const int ProbeChannels = 2;

    /// <summary>How long to wait for CoreAudio to hand a buffer back, in milliseconds.</summary>
    private const int WaitMilliseconds = 5;

    /// <summary>Guards the queue.</summary>
    private readonly Lock _gate = new();

    /// <summary>The buffers CoreAudio handed back and the count of buffers it has played.</summary>
    private QueueState? _state;

    /// <summary>Keeps <see cref="_state"/> reachable from CoreAudio's thread.</summary>
    private GCHandle _handle;

    /// <summary>The queue, or zero.</summary>
    private nint _queue;

    /// <summary>The samples in a buffer, counting every channel.</summary>
    private int _bufferSamples;

    /// <summary>The seconds of sound in a buffer.</summary>
    private double _bufferLength;

    /// <summary>Whether the queue was started.</summary>
    private bool _started;

    /// <summary>Gets a value indicating whether an output queue can be created.</summary>
    public bool IsAvailable
    {
        get
        {
            var format = StreamDescription.Float(ProbeRate, ProbeChannels);
            nint queue = 0;
            if (NativeMethods.NewOutputQueue(&format, &OnBufferDone, 0, 0, 0, 0, &queue) != 0)
            {
                return false;
            }

            _ = NativeMethods.DisposeQueue(queue, 1);
            return true;
        }
    }

    /// <inheritdoc/>
    public TimeSpan Latency => _state is { } state ? TimeSpan.FromSeconds(state.Queued * _bufferLength) : TimeSpan.Zero;

    /// <inheritdoc/>
    public bool Start(int sampleRate, int channels)
    {
        lock (_gate)
        {
            Release();
            _state = new();
            _handle = GCHandle.Alloc(_state);
            _bufferSamples = Math.Max(1, (int)(sampleRate * BufferSeconds)) * channels;
            _bufferLength = (double)_bufferSamples / channels / sampleRate;
            var format = StreamDescription.Float(sampleRate, channels);
            nint queue = 0;
            if (NativeMethods.NewOutputQueue(&format, &OnBufferDone, GCHandle.ToIntPtr(_handle), 0, 0, 0, &queue) != 0 || !AllocateBuffers(queue))
            {
                Release();
                return false;
            }

            _queue = queue;
            return true;
        }
    }

    /// <inheritdoc/>
    public bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_queue == 0 || _state is not { } state)
            {
                return false;
            }

            for (var offset = 0; offset < samples.Length; offset += _bufferSamples)
            {
                nint buffer = 0;
                while (!state.Free.TryDequeue(out buffer))
                {
                    if (cancellationToken.WaitHandle.WaitOne(WaitMilliseconds))
                    {
                        return false;
                    }
                }

                var piece = samples.Slice(offset, Math.Min(_bufferSamples, samples.Length - offset));
                piece.CopyTo(new(*(void**)(buffer + DataOffset), piece.Length));
                *(uint*)(buffer + SizeOffset) = (uint)(piece.Length * sizeof(float));
                if (NativeMethods.EnqueueBuffer(_queue, buffer, 0, 0) != 0)
                {
                    return false;
                }

                if (!_started && NativeMethods.StartQueue(_queue, 0) == 0)
                {
                    _started = true;
                }
            }

            return true;
        }
    }

    /// <inheritdoc/>
    public void Flush()
    {
        lock (_gate)
        {
            if (_queue != 0)
            {
                _ = NativeMethods.StopQueue(_queue, 1);
                _started = false;
            }
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_gate)
        {
            Release();
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Close();

    /// <summary>Called by CoreAudio as each buffer finishes playing; hands the buffer back to the ring.</summary>
    /// <param name="userData">A handle to the queue's state.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="buffer">The buffer.</param>
    [UnmanagedCallersOnly]
    private static void OnBufferDone(nint userData, nint queue, nint buffer)
    {
        if (userData != 0 && GCHandle.FromIntPtr(userData).Target is QueueState state)
        {
            state.Return(buffer);
        }
    }

    /// <summary>Allocates the ring of buffers. Callers hold the gate.</summary>
    /// <param name="queue">The queue.</param>
    /// <returns><see langword="true"/> when every buffer was allocated.</returns>
    private bool AllocateBuffers(nint queue)
    {
        for (var i = 0; i < BufferCount; i++)
        {
            nint buffer = 0;
            if (NativeMethods.AllocateBuffer(queue, (uint)(_bufferSamples * sizeof(float)), &buffer) != 0)
            {
                return false;
            }

            _state!.Free.Enqueue(buffer);
        }

        return true;
    }

    /// <summary>Stops and frees the queue. Callers hold the gate.</summary>
    private void Release()
    {
        if (_queue != 0)
        {
            _ = NativeMethods.StopQueue(_queue, 1);
            _ = NativeMethods.DisposeQueue(_queue, 1);
            _queue = 0;
        }

        if (_handle.IsAllocated)
        {
            _handle.Free();
        }

        _state = null;
        _started = false;
    }

    /// <summary>The buffers waiting to be filled, shared with CoreAudio's thread.</summary>
    private sealed class QueueState
    {
        /// <summary>Gets the buffers CoreAudio handed back.</summary>
        internal ConcurrentQueue<nint> Free { get; } = new();

        /// <summary>Gets the number of buffers queued and not yet handed back.</summary>
        internal int Queued => BufferCount - Free.Count;

        /// <summary>Takes a buffer back into the ring.</summary>
        /// <param name="buffer">The buffer.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Return(nint buffer) => Free.Enqueue(buffer);
    }
}
