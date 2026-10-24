// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Codecs;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>
/// One run of a file from a position to its end: a reader that hands packets to a sound thread and a picture thread. The sound
/// thread decodes and feeds the sound output, which moves the clock; the picture thread shows each picture when the clock reaches it.
/// A run without sound follows the wall clock. A packet that does not decode is skipped, so a damaged file plays what is sound.
/// </summary>
/// <param name="options">What the run needs.</param>
/// <param name="cancellationToken">Stops the run.</param>
[DebuggerDisplay("ManagedRun")]
internal sealed class ManagedRun(ManagedRunOptions options, CancellationToken cancellationToken) : IDisposable
{
    /// <summary>The most packets waiting for each decoder.</summary>
    private const int QueueLimit = 1_024;

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

    /// <summary>The channels of the sound given to the output.</summary>
    private const int OutputChannels = ManagedMediaPlayer.OutputChannels;

    /// <summary>Packets for the sound thread.</summary>
    private readonly BlockingCollection<MediaPacket> _soundPackets = new(QueueLimit);

    /// <summary>Packets for the picture thread.</summary>
    private readonly BlockingCollection<MediaPacket> _picturePackets = new(QueueLimit);

    /// <summary>Packets that were used and are ready to be filled again, so reading a file makes a packet only as often as packets are waiting at once.</summary>
    private readonly ConcurrentQueue<MediaPacket> _spare = new();

    /// <summary>Failures from the threads, in plain words.</summary>
    private readonly ConcurrentQueue<string> _problems = new();

    /// <summary>A picture waiting to be shown.</summary>
    private readonly byte[] _scratch = options.Picture is null ? [] : new byte[options.Picture.Scaler.ByteCount];

    /// <inheritdoc />
    public void Dispose()
    {
        _soundPackets.Dispose();
        _picturePackets.Dispose();
    }

    /// <summary>Plays the file to its end or until the token is cancelled.</summary>
    /// <returns><see langword="null"/> when the file played to its end, or what went wrong.</returns>
    internal string? Execute()
    {
        if (options.Start > 0 && !options.Container.Seek(TimeSpan.FromSeconds(options.Start)))
        {
            return "The file could not move to that position.";
        }

        var soundThread = options.Sound is null ? null : Launch(SoundLoop);
        var pictureThread = options.Picture is null ? null : Launch(PictureLoop);
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

    /// <summary>Starts a thread that ends with the run.</summary>
    /// <param name="body">What the thread does.</param>
    /// <returns>The running thread.</returns>
    private static Thread Launch(ThreadStart body)
    {
        var thread = new Thread(body) { IsBackground = true };
        thread.Start();
        return thread;
    }

    /// <summary>Decodes a packet of sound.</summary>
    /// <param name="sound">The sound of the run.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="decoded">Receives the sound.</param>
    /// <returns><see langword="false"/> when the packet is damaged and was skipped.</returns>
    private static bool TryDecodeSound(ManagedSound sound, MediaPacket packet, PcmBuffer decoded)
    {
        try
        {
            sound.Decoder.Decode(packet, decoded);
            return true;
        }
        catch (InvalidDataException)
        {
            sound.Decoder.Reset();
            return false;
        }
    }

    /// <summary>Reads packets and hands each to the thread for its track.</summary>
    private void ReadPackets()
    {
        var packet = Rent();
        while (!cancellationToken.IsCancellationRequested && options.Container.ReadPacket(packet))
        {
            var queue = QueueFor(packet.Track);
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

    /// <summary>Takes a packet that is ready to be filled.</summary>
    /// <returns>A used packet that was emptied, or a new one when none is waiting.</returns>
    private MediaPacket Rent() => _spare.TryDequeue(out var packet) ? packet : new();

    /// <summary>Empties a packet that has been decoded and keeps it for the reader.</summary>
    /// <param name="packet">The packet.</param>
    private void Return(MediaPacket packet)
    {
        packet.Clear();
        _spare.Enqueue(packet);
    }

    /// <summary>Puts a packet in a queue, waiting while it is full. Stopping the run ends the wait without an exception.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="packet">The packet; the queue owns it once this returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when the run was stopped before the packet fitted.</returns>
    private bool Hand(BlockingCollection<MediaPacket> queue, MediaPacket packet)
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

    /// <summary>Finds the queue for a track.</summary>
    /// <param name="track">The number of the track.</param>
    /// <returns>The queue, or <see langword="null"/> for a track that is not played.</returns>
    private BlockingCollection<MediaPacket>? QueueFor(int track)
    {
        if (track == options.Sound?.Track)
        {
            return _soundPackets;
        }

        return track == options.Picture?.Track ? _picturePackets : null;
    }

    /// <summary>Decodes the sound and gives it to the output, keeping the clock.</summary>
    private void SoundLoop()
    {
        var sound = options.Sound!;
        var decoded = new PcmBuffer();
        var output = new PcmBuffer();
        var position = double.NaN;

        // The reader completes the queue when the run is stopped, so this loop ends without an exception; what is left is only recycled.
        foreach (var packet in _soundPackets.GetConsumingEnumerable())
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                position = double.IsNaN(position) ? packet.Time : position;
                position = Play(sound, packet, decoded, output, position);
            }

            Return(packet);
        }

        if (!cancellationToken.IsCancellationRequested && WaitUntilHeard(sound))
        {
            options.Clock.SoundFinished();
        }
    }

    /// <summary>Decodes a packet of sound and writes it to the output.</summary>
    /// <param name="sound">The sound of the run.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="decoded">Receives the decoded sound.</param>
    /// <param name="output">Receives the sound at the output's rate.</param>
    /// <param name="position">When the first sample of the packet plays, in seconds.</param>
    /// <returns>When the sound after this packet plays, in seconds.</returns>
    private double Play(ManagedSound sound, MediaPacket packet, PcmBuffer decoded, PcmBuffer output, double position)
    {
        decoded.Clear();
        output.Clear();
        if (!TryDecodeSound(sound, packet, decoded))
        {
            return position;
        }

        sound.Resampler.Process(decoded.Samples, output);
        return Write(sound.Output, output.Samples, position);
    }

    /// <summary>Gives the sound to the output, leaving out what comes before the position the run began at.</summary>
    /// <param name="sound">The sound output.</param>
    /// <param name="samples">The stereo samples at the output rate.</param>
    /// <param name="begin">When the first sample plays, in seconds.</param>
    /// <returns>When the sound after these samples plays, in seconds.</returns>
    private double Write(IStreamingAudioOutput sound, ReadOnlySpan<float> samples, double begin)
    {
        var end = begin + ((double)samples.Length / OutputChannels / ManagedMediaPlayer.OutputRate);
        if (end <= options.Start)
        {
            return end;
        }

        // The first packet after a seek can begin before the position that was asked for; the part before it is not played.
        var skip = begin < options.Start ? (int)((options.Start - begin) * ManagedMediaPlayer.OutputRate) * OutputChannels : 0;
        if (sound.Write(samples[Math.Min(skip, samples.Length)..], cancellationToken))
        {
            options.Clock.SoundWritten(end);
        }

        return end;
    }

    /// <summary>Waits for the output to play what it holds, so the end of the file is not cut off.</summary>
    /// <param name="sound">The sound of the run.</param>
    /// <returns><see langword="false"/> when the run was stopped while waiting.</returns>
    private bool WaitUntilHeard(ManagedSound sound)
    {
        var waited = Stopwatch.GetTimestamp();
        while (sound.Output.Latency > TimeSpan.Zero && Stopwatch.GetElapsedTime(waited).TotalSeconds < MaxDrain)
        {
            if (cancellationToken.WaitHandle.WaitOne(DrainPoll))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes the pictures and shows each when the clock reaches it.</summary>
    private void PictureLoop()
    {
        var picture = options.Picture!;
        var frames = new List<VideoFrame>();
        var lastShown = double.NegativeInfinity;
        var failed = false;
        foreach (var packet in _picturePackets.GetConsumingEnumerable())
        {
            if (!failed && !cancellationToken.IsCancellationRequested)
            {
                failed = !TryDecodePicture(picture, packet, frames);
                lastShown = Show(picture, frames, lastShown);
            }

            Return(packet);
        }

        if (failed || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        picture.Decoder.Flush(frames);
        _ = Show(picture, frames, lastShown);
    }

    /// <summary>Decodes a packet of pictures.</summary>
    /// <param name="picture">The pictures of the run.</param>
    /// <param name="packet">The packet.</param>
    /// <param name="frames">Receives the pictures that are ready.</param>
    /// <returns><see langword="false"/> when the pictures cannot be decoded at all and the rest of the file is not shown.</returns>
    private bool TryDecodePicture(ManagedPicture picture, MediaPacket packet, List<VideoFrame> frames)
    {
        try
        {
            picture.Decoder.Decode(packet, frames);
            return true;
        }
        catch (InvalidDataException)
        {
            picture.Decoder.Reset();
            return true;
        }
        catch (NotSupportedException ex)
        {
            _problems.Enqueue($"The picture could not be decoded: {ex.Message}");
            return false;
        }
    }

    /// <summary>Shows every picture the decoder has ready, each at its time.</summary>
    /// <param name="picture">The pictures of the run.</param>
    /// <param name="frames">The pictures that are ready; they are removed.</param>
    /// <param name="lastShown">When the previous picture was shown, in seconds.</param>
    /// <returns>When the latest picture was shown, in seconds.</returns>
    private double Show(ManagedPicture picture, List<VideoFrame> frames, double lastShown)
    {
        foreach (var frame in frames)
        {
            if (!IsDue(frame.Time, lastShown))
            {
                continue;
            }

            picture.Scaler.Scale(frame, _scratch);
            if (!picture.Pump.Show(_scratch))
            {
                continue;
            }

            lastShown = frame.Time;
            picture.Shown();
        }

        foreach (var frame in frames)
        {
            picture.Decoder.Recycle(frame);
        }

        frames.Clear();
        return lastShown;
    }

    /// <summary>Waits for a picture's time and tells whether it is shown: not before the start, and not dropped.</summary>
    /// <param name="time">When the picture shows, in seconds.</param>
    /// <param name="lastShown">When the previous picture was shown, in seconds.</param>
    /// <returns><see langword="true"/> to show the picture.</returns>
    private bool IsDue(double time, double lastShown) => time >= options.Start - StartTolerance && Wait(time) && !SkipPicture(time, lastShown);

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
