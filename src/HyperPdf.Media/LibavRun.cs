// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;

namespace HyperPdf.Media;

/// <summary>
/// One run of a file from a position to its end: a reader that hands packets to a sound thread and a picture thread. The sound
/// thread feeds the sound output and moves the clock; the picture thread shows each picture when the clock reaches it.
/// </summary>
/// <param name="options">What the run needs.</param>
/// <param name="cancellationToken">Stops the run.</param>
[DebuggerDisplay("LibavRun")]
internal sealed class LibavRun(LibavRunOptions options, CancellationToken cancellationToken) : IDisposable
{
    /// <summary>The most packets waiting for each decoder.</summary>
    private const int QueueLimit = 4_096;

    /// <summary>A picture later than this behind the clock is dropped, in seconds.</summary>
    private const double LateLimit = 0.15;

    /// <summary>The longest a picture waits in one sleep, so a cancel is noticed quickly, in seconds.</summary>
    private const double MaxSleep = 0.05;

    /// <summary>Positions earlier than the start by less than this still count as the start, in seconds.</summary>
    private const double StartTolerance = 0.001;

    /// <summary>The seconds between pictures at reduced motion.</summary>
    private const double ReducedInterval = 1.0;

    /// <summary>The longest to wait for the sound output to play what is left, in seconds.</summary>
    private const double MaxDrain = 5;

    /// <summary>How long to wait between looks at the sound output while it drains, in milliseconds.</summary>
    private const int DrainPoll = 20;

    /// <summary>How long to wait for room in a full queue before looking at the token again, in milliseconds.</summary>
    private const int FullPoll = 50;

    /// <summary>Packets for the sound thread.</summary>
    private readonly BlockingCollection<LibavPacket> _soundPackets = new(QueueLimit);

    /// <summary>Packets for the picture thread.</summary>
    private readonly BlockingCollection<LibavPacket> _picturePackets = new(QueueLimit);

    /// <summary>Packets that were used and are ready to be filled again, so reading a file allocates a packet only as often as packets are waiting at once.</summary>
    private readonly ConcurrentQueue<LibavPacket> _spare = new();

    /// <summary>Failures from the threads, in plain words.</summary>
    private readonly ConcurrentQueue<string> _problems = new();

    /// <summary>A picture waiting to be shown.</summary>
    private readonly byte[] _scratch = options.Video is null ? [] : new byte[options.Video.Size.ByteCount];

    /// <inheritdoc />
    public void Dispose()
    {
        Discard(_soundPackets);
        Discard(_picturePackets);
        while (_spare.TryDequeue(out var spare))
        {
            spare.Dispose();
        }

        _soundPackets.Dispose();
        _picturePackets.Dispose();
    }

    /// <summary>Plays the file to its end or until the token is cancelled.</summary>
    /// <returns><see langword="null"/> when the file played to its end, or what went wrong.</returns>
    internal string? Execute()
    {
        if (options.Start > 0 && !options.Source.SeekTo(TimeSpan.FromSeconds(options.Start)))
        {
            return "The file could not move to that position.";
        }

        var soundThread = options.Audio is null ? null : Launch(SoundLoop);
        var pictureThread = options.Video is null ? null : Launch(PictureLoop);
        try
        {
            ReadPackets();
        }
        finally
        {
            _soundPackets.CompleteAdding();
            _picturePackets.CompleteAdding();
            soundThread?.Join();
            pictureThread?.Join();
        }

        return _problems.TryPeek(out var problem) ? problem : null;
    }

    /// <summary>Frees the packets left in a queue.</summary>
    /// <param name="queue">The queue.</param>
    private static void Discard(BlockingCollection<LibavPacket> queue)
    {
        while (queue.TryTake(out var left))
        {
            left.Dispose();
        }
    }

    /// <summary>Starts a thread that ends with the run.</summary>
    /// <param name="body">What the thread does.</param>
    /// <returns>The running thread.</returns>
    private static Thread Launch(ThreadStart body)
    {
        var thread = new Thread(body) { IsBackground = true };
        thread.Start();
        return thread;
    }

    /// <summary>Reads packets and hands each to the thread for its stream.</summary>
    private void ReadPackets()
    {
        var packet = Rent();
        try
        {
            while (!cancellationToken.IsCancellationRequested && options.Source.Read(packet))
            {
                var queue = QueueFor(packet.StreamIndex);
                if (queue is null)
                {
                    Return(packet);
                }
                else if (!Hand(queue, packet))
                {
                    return;
                }

                packet = Rent();
            }
        }
        finally
        {
            packet.Dispose();
        }
    }

    /// <summary>Takes a packet that is ready to be filled.</summary>
    /// <returns>A used packet that was emptied, or a new one when none is waiting.</returns>
    private LibavPacket Rent() => _spare.TryDequeue(out var packet) ? packet : new();

    /// <summary>Empties a packet that has been decoded and keeps it for the reader.</summary>
    /// <param name="packet">The packet.</param>
    private void Return(LibavPacket packet)
    {
        packet.Unref();
        _spare.Enqueue(packet);
    }

    /// <summary>Puts a packet in a queue, waiting while it is full. Stopping the run ends the wait without an exception.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="packet">The packet; the queue owns it once this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the run was stopped before the packet fitted.</returns>
    private bool Hand(BlockingCollection<LibavPacket> queue, LibavPacket packet)
    {
        while (!queue.TryAdd(packet, FullPoll))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the queue for a stream.</summary>
    /// <param name="stream">The index of the stream.</param>
    /// <returns>The queue, or <see langword="null"/> for a stream that is not played.</returns>
    private BlockingCollection<LibavPacket>? QueueFor(int stream)
    {
        if (stream == options.Source.AudioIndex && options.Audio is not null)
        {
            return _soundPackets;
        }

        return stream == options.Source.VideoIndex && options.Video is not null ? _picturePackets : null;
    }

    /// <summary>Decodes the sound and gives it to the output, keeping the clock.</summary>
    private void SoundLoop()
    {
        var audio = options.Audio!;
        try
        {
            // The reader completes the queue when the run is stopped, so this loop ends without an exception; what is left is only freed.
            foreach (var packet in _soundPackets.GetConsumingEnumerable())
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    WriteSound(audio.Send(packet), audio);
                }

                Return(packet);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            WriteSound(audio.Send(null), audio);
            WaitUntilHeard();
            options.Clock.SoundFinished();
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            _problems.Enqueue($"The sound could not be played: {ex.Message}");
        }
    }

    /// <summary>Writes all the sound the decoder has ready.</summary>
    /// <param name="accepted">Whether the decoder took the packet.</param>
    /// <param name="audio">The sound decoder.</param>
    private void WriteSound(bool accepted, LibavAudioDecoder audio)
    {
        while (accepted && audio.Receive(out var samples, out var begin))
        {
            var position = begin - options.Source.Origin;
            var end = position + ((double)samples.Length / LibavAudioDecoder.OutputChannels / LibavAudioDecoder.OutputRate);
            if (end <= options.Start)
            {
                continue;
            }

            // The first frame after a seek can begin before the position that was asked for; the part before it is not played.
            var skip = position < options.Start ? (int)((options.Start - position) * LibavAudioDecoder.OutputRate) * LibavAudioDecoder.OutputChannels : 0;
            if (options.Sound is not null && !options.Sound.Write(samples[Math.Min(skip, samples.Length)..], cancellationToken))
            {
                return;
            }

            options.Clock.SoundWritten(end);
        }
    }

    /// <summary>Waits for the output to play what it holds, so the end of the file is not cut off.</summary>
    private void WaitUntilHeard()
    {
        var waited = Stopwatch.GetTimestamp();
        while (options.Sound is { } sound && sound.Latency > TimeSpan.Zero && Stopwatch.GetElapsedTime(waited).TotalSeconds < MaxDrain)
        {
            if (cancellationToken.WaitHandle.WaitOne(DrainPoll))
            {
                return;
            }
        }
    }

    /// <summary>Decodes the pictures and shows each when the clock reaches it.</summary>
    private void PictureLoop()
    {
        var video = options.Video!;
        var lastShown = double.NegativeInfinity;
        try
        {
            foreach (var packet in _picturePackets.GetConsumingEnumerable())
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    lastShown = ShowPictures(video, video.Send(packet), lastShown);
                }

                Return(packet);
            }

            _ = ShowPictures(video, video.Send(null), lastShown);
        }
        catch (Exception ex) when (ex is InvalidOperationException or DllNotFoundException or EntryPointNotFoundException)
        {
            _problems.Enqueue($"The picture could not be shown: {ex.Message}");
        }
    }

    /// <summary>Shows every picture the decoder has ready, each at its time.</summary>
    /// <param name="video">The picture decoder.</param>
    /// <param name="accepted">Whether the decoder took the packet.</param>
    /// <param name="lastShown">When the previous picture was shown, in seconds.</param>
    /// <returns>When the latest picture was shown, in seconds.</returns>
    private double ShowPictures(LibavVideoDecoder video, bool accepted, double lastShown)
    {
        while (accepted && video.Receive(out var begin))
        {
            var time = begin - options.Source.Origin;
            if (time >= options.Start - StartTolerance && Wait(time) && !SkipPicture(time, lastShown) && video.Scale(_scratch) && options.Pump!.Show(_scratch))
            {
                lastShown = time;
                options.Shown();
            }

            video.Release();
        }

        return lastShown;
    }

    /// <summary>Decides whether a picture is dropped: at reduced motion when one was shown a moment ago, or when the clock is already past it.</summary>
    /// <param name="time">When the picture shows, in seconds.</param>
    /// <param name="lastShown">When the previous picture was shown, in seconds.</param>
    /// <returns><see langword="true"/> to drop the picture.</returns>
    private bool SkipPicture(double time, double lastShown)
    {
        if (!double.IsFinite(lastShown))
        {
            return false;
        }

        return options.ReduceMotion ? time - lastShown < ReducedInterval : options.Clock.Seconds - time > LateLimit;
    }

    /// <summary>Waits until the clock reaches a time.</summary>
    /// <param name="time">The time, in seconds.</param>
    /// <returns><see langword="false"/> when the run was stopped while waiting.</returns>
    private bool Wait(double time)
    {
        for (var delay = time - options.Clock.Seconds; delay > 0; delay = time - options.Clock.Seconds)
        {
            if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(Math.Min(delay, MaxSleep))))
            {
                return false;
            }
        }

        return !cancellationToken.IsCancellationRequested;
    }
}
