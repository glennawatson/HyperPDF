// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Speech;

namespace HyperPdf.Platform.Linux.Audio;

/// <summary>
/// Plays speech through PulseAudio's simple API, which PipeWire also provides. Audio is written in short pieces so Stop
/// takes effect at once; one stream is kept open while the sample rate stays the same.
/// </summary>
[DebuggerDisplay("PulseAudioOutput: PulseAudio")]
public sealed unsafe class PulseAudioOutput : IAudioOutput
{
    /// <summary>PulseAudio's 32 bit little endian float format.</summary>
    private const int Float32LittleEndian = 5;

    /// <summary>The playback direction.</summary>
    private const int Playback = 1;

    /// <summary>The sample rate a stream is first opened at, Kokoro's.</summary>
    private const int DefaultRate = 24_000;

    /// <summary>The audio the server keeps buffered, in seconds.</summary>
    private const double TargetSeconds = 0.15;

    /// <summary>The samples written at a time, a tenth of a second at 24 kHz.</summary>
    private const int Chunk = 2_400;

    /// <summary>The longest wait for buffered audio, in microseconds, should the server report nonsense.</summary>
    private const ulong MaxWaitMicroseconds = 10_000_000;

    /// <summary>The latency PulseAudio reports on error.</summary>
    private const ulong LatencyError = ulong.MaxValue;

    /// <summary>How far ahead of playback audio is written.</summary>
    private static readonly TimeSpan MaxAhead = TimeSpan.FromMilliseconds(300);

    /// <summary>The libpulse-simple file names.</summary>
    private static readonly string[] Candidates = ["libpulse-simple.so.0", "libpulse-simple.so"];

    /// <summary>Whether libpulse-simple loads, worked out on first use.</summary>
    private static readonly Lazy<bool> Loaded = new(LoadLibrary);

    /// <summary>Serialises playback.</summary>
    private readonly Lock _gate = new();

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>The open stream.</summary>
    private PulseStreamHandle? _stream;

    /// <summary>The open stream's sample rate.</summary>
    private int _rate;

    /// <summary>Gets a value indicating whether libpulse-simple loads and a sound server accepts a stream.</summary>
    public bool IsAvailable
    {
        get
        {
            if (!LibraryLoads())
            {
                return false;
            }

            // Busy means a clip is playing through an open stream; waiting for it would stall the caller.
            if (!_gate.TryEnter())
            {
                return true;
            }

            try
            {
                return Open(_rate > 0 ? _rate : DefaultRate) is not null;
            }
            finally
            {
                _gate.Exit();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || !_gate.TryEnter())
        {
            // A clip holds the gate; it sees the flag and closes the stream when it stops.
            return;
        }

        try
        {
            CloseStream();
        }
        finally
        {
            _gate.Exit();
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
                var (output, clip, token) = ((PulseAudioOutput, SpeechAudio, CancellationToken))state!;
                output.Play(clip, token);
            },
            (this, audio, cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Waits for the buffered audio to play, flushing it if cancelled. The server's latency, read once after the last
    /// write, is how long the written audio takes to be heard; it is not polled to zero, as an idle stream keeps
    /// reporting the device's own delay.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    private static void WaitUntilPlayed(PulseStreamHandle stream, CancellationToken cancellationToken)
    {
        var latency = NativeMethods.PaSimpleGetLatency(stream, out _);
        if (latency == LatencyError)
        {
            return;
        }

        var remaining = TimeSpan.FromMicroseconds(Math.Min(latency, MaxWaitMicroseconds));
        if (cancellationToken.WaitHandle.WaitOne(remaining))
        {
            _ = NativeMethods.PaSimpleFlush(stream, out _);
        }
    }

    /// <summary>Determines whether libpulse-simple can be loaded.</summary>
    /// <returns><see langword="true"/> when found.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LibraryLoads() => Loaded.Value;

    /// <summary>Registers and loads libpulse-simple, once.</summary>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool LoadLibrary()
    {
        NativeLibraries.Register(typeof(PulseAudioOutput).Assembly, NativeMethods.Library, Candidates);
        return NativeLibraries.TryLoad(typeof(PulseAudioOutput).Assembly, NativeMethods.Library);
    }

    /// <summary>Writes the audio piece by piece, then waits for it to finish playing.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops at once, discarding what is buffered.</param>
    private void Play(SpeechAudio audio, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            try
            {
                PlayLocked(audio, cancellationToken);
            }
            finally
            {
                // Dispose did not wait for this clip, so the stream is closed here.
                if (Volatile.Read(ref _disposed) != 0)
                {
                    CloseStream();
                }
            }
        }
    }

    /// <summary>Closes the open stream. Callers hold the gate.</summary>
    private void CloseStream()
    {
        _stream?.Dispose();
        _stream = null;
    }

    /// <summary>Writes the audio and waits for it. Callers hold the gate.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops at once, discarding what is buffered.</param>
    private void PlayLocked(SpeechAudio audio, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _disposed) != 0 || Open(audio.SampleRate) is not { } stream)
        {
            return;
        }

        var samples = audio.Samples;
        var started = Stopwatch.GetTimestamp();
        for (var start = 0; start < samples.Length; start += Chunk)
        {
            // Stay a little ahead of what has been heard, so a write never blocks for long and Stop is immediate.
            var ahead = TimeSpan.FromSeconds((double)start / audio.SampleRate) - Stopwatch.GetElapsedTime(started) - MaxAhead;
            if ((ahead > TimeSpan.Zero && cancellationToken.WaitHandle.WaitOne(ahead)) || cancellationToken.IsCancellationRequested)
            {
                _ = NativeMethods.PaSimpleFlush(stream, out _);
                return;
            }

            var count = Math.Min(Chunk, samples.Length - start);
            fixed (float* data = &samples[start])
            {
                _ = NativeMethods.PaSimpleWrite(stream, data, (nuint)(count * sizeof(float)), out _);
            }
        }

        WaitUntilPlayed(stream, cancellationToken);
    }

    /// <summary>Opens a stream at a sample rate, keeping one already open at that rate. Callers hold the gate.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <returns>The stream, or <see langword="null"/> when there is no sound server.</returns>
    private PulseStreamHandle? Open(int rate)
    {
        if (_stream is not null && _rate == rate)
        {
            return _stream;
        }

        _stream?.Dispose();
        _stream = null;
        if (!LibraryLoads())
        {
            return null;
        }

        var spec = new PulseSampleSpec(Float32LittleEndian, (uint)rate, 1);

        // A short buffer starts playing at once and lets Stop take effect quickly; the default holds about two seconds.
        var target = (uint)(rate * sizeof(float) * TargetSeconds);
        var attributes = new PulseBufferAttributes(uint.MaxValue, target, uint.MaxValue, uint.MaxValue, uint.MaxValue);
        fixed (byte* name = "HyperPDF\0"u8)
        {
            fixed (byte* description = "Read aloud\0"u8)
            {
                var stream = NativeMethods.PaSimpleNew(null, name, Playback, null, description, &spec, null, &attributes, out _);
                if (stream.IsInvalid)
                {
                    stream.Dispose();
                    return null;
                }

                _stream = stream;
                _rate = rate;
                return stream;
            }
        }
    }
}
