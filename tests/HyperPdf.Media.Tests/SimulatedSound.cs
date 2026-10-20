// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Media;

namespace HyperPdf.Media.Tests;

/// <summary>
/// A sound output that behaves like a real device without making any noise: it plays what it is given in real time, holds a fifth of
/// a second, makes a write wait while it is full, and reports the latency a real device would. It records what was written.
/// </summary>
[DebuggerDisplay("SimulatedSound: {WrittenSeconds} s written")]
internal sealed class SimulatedSound : IStreamingAudioOutput
{
    /// <summary>The sound the device holds, in seconds.</summary>
    private const double BufferSeconds = 0.2;

    /// <summary>Guards the queue.</summary>
    private readonly Lock _gate = new();

    /// <summary>The seconds of sound waiting at <see cref="_measuredAt"/>.</summary>
    private double _queued;

    /// <summary>The time stamp at which <see cref="_queued"/> was set.</summary>
    private long _measuredAt = Stopwatch.GetTimestamp();

    /// <summary>The samples each second of each channel.</summary>
    private int _rate = 1;

    /// <summary>The channels.</summary>
    private int _channels = 1;

    /// <summary>The samples written so far, counting every channel.</summary>
    private long _samples;

    /// <summary>Gets a value indicating whether sound can be played; always <see langword="true"/>.</summary>
    public bool IsAvailable => true;

    /// <summary>Gets how long the written sound takes to be heard.</summary>
    public TimeSpan Latency
    {
        get
        {
            lock (_gate)
            {
                return TimeSpan.FromSeconds(Remaining());
            }
        }
    }

    /// <summary>Gets the seconds of sound written so far.</summary>
    public double WrittenSeconds
    {
        get
        {
            lock (_gate)
            {
                return (double)Interlocked.Read(ref _samples) / _rate / _channels;
            }
        }
    }

    /// <inheritdoc />
    public bool Start(int sampleRate, int channels)
    {
        lock (_gate)
        {
            _rate = sampleRate;
            _channels = channels;
        }

        return true;
    }

    /// <inheritdoc />
    public bool Write(ReadOnlySpan<float> samples, CancellationToken cancellationToken)
    {
        while (true)
        {
            double wait;
            lock (_gate)
            {
                var remaining = Remaining();
                if (remaining <= BufferSeconds)
                {
                    _queued = remaining + ((double)samples.Length / _rate / _channels);
                    _measuredAt = Stopwatch.GetTimestamp();
                    _ = Interlocked.Add(ref _samples, samples.Length);
                    return true;
                }

                wait = remaining - BufferSeconds;
            }

            if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(wait)))
            {
                return false;
            }
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        lock (_gate)
        {
            _queued = 0;
            _measuredAt = Stopwatch.GetTimestamp();
        }
    }

    /// <inheritdoc />
    public void Close()
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Works out how much sound is still waiting. Callers hold the gate.</summary>
    /// <returns>The seconds of sound waiting.</returns>
    private double Remaining() => Math.Max(0, _queued - Stopwatch.GetElapsedTime(_measuredAt).TotalSeconds);
}
