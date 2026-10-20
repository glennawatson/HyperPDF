// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Media;

namespace HyperPdf.Platform.Linux.Audio;

/// <summary>
/// Plays a continuous stream of sound through PulseAudio's simple API, which PipeWire also provides. A write waits while the server
/// holds about a fifth of a second, so the caller stays just ahead of what is heard. The latency is read after each write and
/// counted down by the clock in between, because the simple API must not be called from two threads at once.
/// </summary>
[DebuggerDisplay("PulseStreamingAudioOutput: PulseAudio")]
public sealed unsafe class PulseStreamingAudioOutput : IStreamingAudioOutput
{
    /// <summary>PulseAudio's 32 bit little endian float format.</summary>
    private const int Float32LittleEndian = 5;

    /// <summary>The playback direction.</summary>
    private const int Playback = 1;

    /// <summary>The audio the server keeps buffered, in seconds.</summary>
    private const double TargetSeconds = 0.2;

    /// <summary>The seconds of sound written at a time, so a stop is noticed quickly.</summary>
    private const double ChunkSeconds = 0.025;

    /// <summary>The microseconds in a second.</summary>
    private const double Microseconds = 1_000_000.0;

    /// <summary>The latency PulseAudio reports on error.</summary>
    private const ulong LatencyError = ulong.MaxValue;

    /// <summary>Serialises calls to the simple API.</summary>
    private readonly Lock _gate = new();

    /// <summary>The open stream.</summary>
    private PulseStreamHandle? _stream;

    /// <summary>The samples written at a time.</summary>
    private int _chunk;

    /// <summary>The latency at the last measurement, in seconds.</summary>
    private double _measuredLatency;

    /// <summary>The time stamp of the last measurement.</summary>
    private long _measuredAt;

    /// <summary>Gets a value indicating whether libpulse-simple loads.</summary>
    public bool IsAvailable => PulseAudioOutput.LibraryLoads();

    /// <inheritdoc/>
    public TimeSpan Latency
    {
        get
        {
            var remaining = Volatile.Read(ref _measuredLatency) - Stopwatch.GetElapsedTime(Volatile.Read(ref _measuredAt)).TotalSeconds;
            return TimeSpan.FromSeconds(Math.Max(0, remaining));
        }
    }

    /// <inheritdoc/>
    public bool Start(int sampleRate, int channels)
    {
        if (!IsAvailable)
        {
            return false;
        }

        lock (_gate)
        {
            _stream?.Dispose();
            _chunk = Math.Max(1, (int)(sampleRate * ChunkSeconds)) * channels;
            var spec = new PulseSampleSpec(Float32LittleEndian, (uint)sampleRate, (byte)channels);
            var target = (uint)(sampleRate * channels * sizeof(float) * TargetSeconds);
            var attributes = new PulseBufferAttributes(uint.MaxValue, target, uint.MaxValue, uint.MaxValue, uint.MaxValue);
            fixed (byte* name = "HyperPDF\0"u8)
            {
                fixed (byte* description = "Media\0"u8)
                {
                    var stream = NativeMethods.PaSimpleNew(null, name, Playback, null, description, &spec, null, &attributes, out _);
                    if (stream.IsInvalid)
                    {
                        stream.Dispose();
                        _stream = null;
                        return false;
                    }

                    _stream = stream;
                    Measure(stream);
                    return true;
                }
            }
        }
    }

    /// <inheritdoc/>
    public bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_stream is not { } stream)
            {
                return false;
            }

            for (var offset = 0; offset < samples.Length; offset += _chunk)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return false;
                }

                var piece = samples.Slice(offset, Math.Min(_chunk, samples.Length - offset));
                fixed (float* data = piece)
                {
                    if (NativeMethods.PaSimpleWrite(stream, data, (nuint)(piece.Length * sizeof(float)), out _) != 0)
                    {
                        return false;
                    }
                }

                Measure(stream);
            }

            return true;
        }
    }

    /// <inheritdoc/>
    public void Flush()
    {
        lock (_gate)
        {
            if (_stream is { } stream)
            {
                _ = NativeMethods.PaSimpleFlush(stream, out _);
                Measure(stream);
            }
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_gate)
        {
            _stream?.Dispose();
            _stream = null;
            Volatile.Write(ref _measuredLatency, 0);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Close();

    /// <summary>Reads how long the written sound takes to be heard. Callers hold the gate.</summary>
    /// <param name="stream">The stream.</param>
    private void Measure(PulseStreamHandle stream)
    {
        var latency = NativeMethods.PaSimpleGetLatency(stream, out _);
        if (latency == LatencyError)
        {
            return;
        }

        Volatile.Write(ref _measuredAt, Stopwatch.GetTimestamp());
        Volatile.Write(ref _measuredLatency, latency / Microseconds);
    }
}
