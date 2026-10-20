// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>
/// Plays the sound and video of a file with the FFmpeg libraries that ship with the app. The sound output's clock drives the
/// picture, so the two stay together, and a position can be reached at once. Playing never starts by itself: the reader presses Play.
/// </summary>
[DebuggerDisplay("LibavMediaPlayer: {Snapshot}")]
public sealed class LibavMediaPlayer : MediaPlayerBase
{
    /// <summary>Makes a sound output for each run.</summary>
    private readonly Func<IStreamingAudioOutput> _soundFactory;

    /// <summary>Whether to show a picture only once a second.</summary>
    private readonly bool _reduceMotion;

    /// <summary>The clock of the current run, or <see langword="null"/> before one starts.</summary>
    private PlaybackClock? _clock;

    /// <summary>Initializes a new instance of the <see cref="LibavMediaPlayer"/> class.</summary>
    /// <param name="soundFactory">Makes a sound output; called once for each run that has sound.</param>
    /// <param name="reduceMotion">Whether to show a picture only once a second, for readers who asked for less motion.</param>
    public LibavMediaPlayer(Func<IStreamingAudioOutput> soundFactory, bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(soundFactory);
        _soundFactory = soundFactory;
        _reduceMotion = reduceMotion;
    }

    /// <inheritdoc />
    public override async Task<bool> OpenAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Halt();
        if (!FfmpegLibraries.IsAvailable)
        {
            return Fail("The FFmpeg libraries could not be loaded.");
        }

        var probed = await Task.Run(() => Probe(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (probed is null)
        {
            return Fail("FFmpeg could not read any sound or picture from this file.");
        }

        Opened(path, probed.Info, probed.Cues, probed.Size);
        return true;
    }

    /// <inheritdoc />
    protected override TimeSpan ClockPosition(TimeSpan anchor, long startedAt) =>
        Volatile.Read(ref _clock) is { } clock ? clock.Position : base.ClockPosition(anchor, startedAt);

    /// <inheritdoc />
    protected override async Task<string?> PlayAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken)
    {
        // The clock exists before anything else happens, so a position asked for at once is already counted from the start.
        var clock = new PlaybackClock(start.TotalSeconds);
        Volatile.Write(ref _clock, clock);
        var request = new RunRequest(this, path, start, pump, clock, cancellationToken);
        return await RunOnThread(request).ConfigureAwait(false);
    }

    /// <summary>Starts the thread that plays a run; decoding and the sound output block, so the run does not use a pool thread.</summary>
    /// <param name="request">What to play.</param>
    /// <returns>A task that completes with <see langword="null"/> when the file played to its end, or with what went wrong.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<string?> RunOnThread(RunRequest request) =>
        Task.Factory.StartNew(static state => ((RunRequest)state!).Player.PlayOnThread((RunRequest)state), request, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    /// <summary>Opens the file and reads what it holds.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="cancellationToken">Stops reading the captions.</param>
    /// <returns>What the file holds, or <see langword="null"/> when it has no sound or picture that can be decoded.</returns>
    private static ProbeResult? Probe(string path, CancellationToken cancellationToken)
    {
        using var source = LibavSource.Open(path);
        if (source is null)
        {
            return null;
        }

        // Opening a decoder is how to tell whether this build of FFmpeg can decode a stream.
        using var sound = source.OpenSoundDecoder();
        var size = source.PictureSize();
        using var picture = source.OpenPictureDecoder(size);
        if (sound is null && picture is null)
        {
            return null;
        }

        var cues = source.CaptionIndex >= 0 ? LibavCaptions.Read(path, cancellationToken) : [];
        var shown = picture is null ? default : size;
        return new(new(source.Duration, shown.Width, shown.Height, picture is not null, sound is not null, cues.Count > 0), cues, shown);
    }

    /// <summary>Plays the file on a thread of its own, because decoding and the sound output block.</summary>
    /// <param name="request">What to play.</param>
    /// <returns><see langword="null"/> when the file played to its end, or what went wrong.</returns>
    private string? PlayOnThread(RunRequest request)
    {
        var (_, path, start, pump, clock, cancellationToken) = request;
        using var source = LibavSource.Open(path);
        if (source is null)
        {
            return "The file could not be opened again.";
        }

        using var audio = source.OpenSoundDecoder();
        using var video = pump is null ? null : source.OpenPictureDecoder(source.PictureSize());
        if (audio is null && video is null)
        {
            return "This file has no sound or picture that can be decoded.";
        }

        using var sound = audio is null ? null : OpenSound();
        clock.Begin(sound);
        var options = new LibavRunOptions(source, audio, video, sound, clock, pump, Publish, _reduceMotion, start.TotalSeconds);
        using var run = new LibavRun(options, cancellationToken);
        return run.Execute();
    }

    /// <summary>Opens a sound output.</summary>
    /// <returns>The output, or <see langword="null"/> when the computer cannot play sound.</returns>
    private IStreamingAudioOutput? OpenSound()
    {
        var sound = _soundFactory();
        if (sound.IsAvailable && sound.Start(LibavAudioDecoder.OutputRate, LibavAudioDecoder.OutputChannels))
        {
            return sound;
        }

        sound.Dispose();
        return null;
    }

    /// <summary>What one run needs, handed to the thread that plays it.</summary>
    /// <param name="Player">The player.</param>
    /// <param name="Path">The full path of the file.</param>
    /// <param name="Start">Where to begin.</param>
    /// <param name="Pump">Receives the pictures, or <see langword="null"/>.</param>
    /// <param name="Clock">The clock to play by.</param>
    /// <param name="Token">Stops the run.</param>
    private sealed record RunRequest(LibavMediaPlayer Player, string Path, TimeSpan Start, FramePump? Pump, PlaybackClock Clock, CancellationToken Token);

    /// <summary>What a file holds, found when it is opened.</summary>
    /// <param name="Info">The streams, length and size.</param>
    /// <param name="Cues">The captions.</param>
    /// <param name="Size">The size of the scaled pictures.</param>
    private sealed record ProbeResult(MediaInfo Info, List<CaptionCue> Cues, FrameSize Size);
}
