// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>
/// The clock a run plays by. While there is sound, the clock is what is being heard: the end of the sound written so far, minus the
/// time the sound output still needs to play it. The picture waits for this clock, so it matches the sound. Without sound the
/// clock follows the wall clock. Until <see cref="Begin"/> is called the clock stands still at the start.
/// </summary>
[DebuggerDisplay("PlaybackClock: {Position}")]
internal sealed class PlaybackClock
{
    /// <summary>The position at which the run starts, in seconds.</summary>
    private readonly double _start;

    /// <summary>The end of the sound written so far, in seconds; negative before any is written.</summary>
    private double _soundEnd = -1;

    /// <summary>The position when the sound finished, or negative while it plays.</summary>
    private double _finishedAtPosition = -1;

    /// <summary>The time stamp at which the sound finished.</summary>
    private long _finishedAt;

    /// <summary>The time stamp at which the clock began, or zero before it begins.</summary>
    private long _startedAt;

    /// <summary>The latest position given, so the clock never goes backward when the output's latency wobbles.</summary>
    private double _latest;

    /// <summary>Initializes a new instance of the <see cref="PlaybackClock"/> class.</summary>
    /// <param name="start">The position at which the run starts, in seconds.</param>
    internal PlaybackClock(double start)
    {
        _start = start;
        _latest = start;
    }

    /// <summary>Gets the position in seconds from the start of the file.</summary>
    internal double Seconds
    {
        get
        {
            var position = Compute();
            var latest = Volatile.Read(ref _latest);
            if (position <= latest)
            {
                return latest;
            }

            Volatile.Write(ref _latest, position);
            return position;
        }
    }

    /// <summary>Gets the position.</summary>
    internal TimeSpan Position => TimeSpan.FromSeconds(Seconds);

    /// <summary>Gets or sets the sound output the clock follows, or <see langword="null"/> when it follows the wall clock.</summary>
    private IStreamingAudioOutput? Sound { get; set; }

    /// <summary>Starts the clock.</summary>
    /// <param name="sound">The sound output the clock follows, or <see langword="null"/> to follow the wall clock.</param>
    internal void Begin(IStreamingAudioOutput? sound)
    {
        Sound = sound;
        Volatile.Write(ref _startedAt, Stopwatch.GetTimestamp());
    }

    /// <summary>Notes that sound up to a position was handed to the output.</summary>
    /// <param name="end">The end of the sound, in seconds from the start of the file.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SoundWritten(double end) => Volatile.Write(ref _soundEnd, end);

    /// <summary>Notes that all the sound was heard, so the clock carries on by the wall clock.</summary>
    internal void SoundFinished()
    {
        Volatile.Write(ref _finishedAt, Stopwatch.GetTimestamp());
        Volatile.Write(ref _finishedAtPosition, Math.Max(Volatile.Read(ref _soundEnd), _start));
    }

    /// <summary>Works out the position without the guard against going backward.</summary>
    /// <returns>The position in seconds.</returns>
    private double Compute()
    {
        var began = Volatile.Read(ref _startedAt);
        if (began == 0)
        {
            return _start;
        }

        var sound = Sound;
        if (sound is null)
        {
            return _start + Stopwatch.GetElapsedTime(began).TotalSeconds;
        }

        var finished = Volatile.Read(ref _finishedAtPosition);
        if (finished >= 0)
        {
            return finished + Stopwatch.GetElapsedTime(Volatile.Read(ref _finishedAt)).TotalSeconds;
        }

        var end = Volatile.Read(ref _soundEnd);
        return end < 0 ? _start : Math.Max(_start, end - sound.Latency.TotalSeconds);
    }
}
