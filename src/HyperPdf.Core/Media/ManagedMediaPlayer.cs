// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperCodecs;
using HyperCodecs.Platform.Video;
using HyperCodecs.Video;

namespace HyperPdf.Core.Media;

/// <summary>
/// Plays the sound and video of a file with the readers and decoders written in C# for this app, so no native library is needed.
/// It plays the formats <c>HyperCodecs</c> decodes and declines the rest. The sound output's clock is what is heard and drives the
/// picture, so the two stay together, and a position can be reached at once. Playing never starts by itself: the reader presses Play.
/// </summary>
[DebuggerDisplay("ManagedMediaPlayer: {Snapshot}")]
public sealed class ManagedMediaPlayer : MediaPlayerBase
{
    /// <summary>The samples a second of the sound given to the output.</summary>
    internal const int OutputRate = 48_000;

    /// <summary>The channels of the sound given to the output.</summary>
    internal const int OutputChannels = 2;

    /// <summary>Makes a sound output for each run.</summary>
    private readonly Func<IStreamingAudioOutput> _soundFactory;

    /// <summary>Whether to show a picture only once a second.</summary>
    private readonly bool _reduceMotion;

    /// <summary>The clock of the current run, or <see langword="null"/> before one starts.</summary>
    private PlaybackClock? _clock;

    /// <summary>Initializes a new instance of the <see cref="ManagedMediaPlayer"/> class.</summary>
    /// <param name="soundFactory">Makes a sound output; called once for each run that has sound.</param>
    /// <param name="reduceMotion">Whether to show a picture only once a second, for readers who asked for less motion.</param>
    public ManagedMediaPlayer(Func<IStreamingAudioOutput> soundFactory, bool reduceMotion)
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
        var probed = await Task.Run(() => Probe(path, cancellationToken), cancellationToken).ConfigureAwait(false);
        if (probed is null)
        {
            return Fail("This file is not in a format the built-in decoders read.");
        }

        Opened(path, probed.Info, probed.Cues, probed.Size);
        return true;
    }

    /// <inheritdoc />
    protected override LocalizedMessage DescribeProblem(string? problem) =>
        problem is null ? LocalizedMessage.Literal(null) : LocalizedMessage.Get(UiText, problem);

    /// <inheritdoc />
    protected override TimeSpan ClockPosition(TimeSpan anchor, long startedAt) =>
        Volatile.Read(ref _clock) is { } clock ? clock.Position : base.ClockPosition(anchor, startedAt);

    /// <inheritdoc />
    protected override Task<string?> PlayAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken)
    {
        // The clock exists before anything else happens, so a position asked for at once is already counted from the start.
        var clock = new PlaybackClock(start.TotalSeconds);
        Volatile.Write(ref _clock, clock);
        var request = new RunRequest(this, path, start, pump, clock, cancellationToken);
        return Task.Factory.StartNew(
            static state => ((RunRequest)state!).Player.PlayOnThread((RunRequest)state),
            request,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>Opens the file to see whether it can be played.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="cancellationToken">Stops reading the captions.</param>
    /// <returns>What the file holds, or <see langword="null"/> when no sound or picture in it can be decoded.</returns>
    private static ProbeResult? Probe(string path, CancellationToken cancellationToken)
    {
        using var container = MediaContainers.Open(path);
        if (container is null)
        {
            return null;
        }

        // Making a decoder is how to tell whether this library can decode a track.
        var sound = container.FindTrack(MediaTrackKind.Audio);
        var picture = container.FindTrack(MediaTrackKind.Video);
        var hasSound = sound >= 0 && CanDecodeSound(container.Tracks[sound]);
        var hasPicture = picture >= 0 && CanDecodePicture(container.Tracks[picture]);
        if (!hasSound && !hasPicture)
        {
            return null;
        }

        var size = hasPicture ? FrameSize.Fit(container.Tracks[picture].Width, container.Tracks[picture].Height) : default;
        var caption = container.FindTrack(MediaTrackKind.Caption);
        var cues = caption >= 0 ? ManagedCaptions.Read(container, caption, cancellationToken) : [];
        return new(new(container.Duration, size.Width, size.Height, hasPicture && !size.IsEmpty, hasSound, cues.Count > 0), cues, size);
    }

    /// <summary>Tells whether a sound track can be decoded.</summary>
    /// <param name="track">The track.</param>
    /// <returns><see langword="true"/> when a decoder exists for it.</returns>
    private static bool CanDecodeSound(MediaTrack track)
    {
        using var decoder = AudioDecoders.Create(track);
        return decoder is not null;
    }

    /// <summary>Tells whether a picture track can be decoded.</summary>
    /// <param name="track">The track.</param>
    /// <returns><see langword="true"/> when a decoder exists for it.</returns>
    private static bool CanDecodePicture(MediaTrack track)
    {
        using var decoder = LinuxVideoDecoders.Create(track);
        return decoder is not null;
    }

    /// <summary>Opens the decoder of the file's pictures.</summary>
    /// <param name="container">The file.</param>
    /// <param name="pump">Receives the pictures.</param>
    /// <param name="player">The player, told when a picture is shown.</param>
    /// <returns>The pictures, or <see langword="null"/> when there are none or they cannot be decoded.</returns>
    private static OpenedPicture? OpenPictureTrack(MediaContainer container, FramePump pump, ManagedMediaPlayer player)
    {
        var index = container.FindTrack(MediaTrackKind.Video);
        var decoder = index < 0 ? null : LinuxVideoDecoders.Create(container.Tracks[index]);
        if (decoder is null)
        {
            return null;
        }

        var size = FrameSize.Fit(container.Tracks[index].Width, container.Tracks[index].Height);
        return new(new(index, decoder, new VideoScaler(size.Width, size.Height), pump, player.Publish));
    }

    /// <summary>Plays the file on a thread of its own, because decoding and the sound output block.</summary>
    /// <param name="request">What to play.</param>
    /// <returns><see langword="null"/> when the file played to its end, or what went wrong.</returns>
    private string? PlayOnThread(RunRequest request)
    {
        using var container = MediaContainers.Open(request.Path);
        if (container is null)
        {
            return "The file could not be opened again.";
        }

        using var sound = OpenSoundTrack(container);
        using var picture = request.Pump is null ? null : OpenPictureTrack(container, request.Pump, request.Player);
        if (sound is null && picture is null)
        {
            return "This file has no sound or picture that can be decoded.";
        }

        request.Clock.Begin(sound?.Sound.Output);
        var options = new ManagedRunOptions(container, sound?.Sound, picture?.Picture, request.Clock, _reduceMotion, request.Start.TotalSeconds);
        using var run = new ManagedRun(options, request.Token);
        return run.Execute();
    }

    /// <summary>Opens the decoder and the sound output of the file's sound.</summary>
    /// <param name="container">The file.</param>
    /// <returns>The sound, or <see langword="null"/> when there is none, it cannot be decoded, or the computer cannot play sound.</returns>
    private OpenedSound? OpenSoundTrack(MediaContainer container)
    {
        var index = container.FindTrack(MediaTrackKind.Audio);
        var decoder = index < 0 ? null : AudioDecoders.Create(container.Tracks[index]);
        if (decoder is null)
        {
            return null;
        }

        var output = OpenSound();
        if (output is null)
        {
            decoder.Dispose();
            return null;
        }

        var track = container.Tracks[index];
        return new(new(index, decoder, new(track.SampleRate, track.Channels, OutputRate), output));
    }

    /// <summary>Opens a sound output.</summary>
    /// <returns>The output, or <see langword="null"/> when the computer cannot play sound.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IStreamingAudioOutput? OpenSound()
    {
        var sound = _soundFactory();
        if (sound.IsAvailable && sound.Start(OutputRate, OutputChannels))
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
    private sealed record RunRequest(ManagedMediaPlayer Player, string Path, TimeSpan Start, FramePump? Pump, PlaybackClock Clock, CancellationToken Token);

    /// <summary>What a file holds, found when it is opened.</summary>
    /// <param name="Info">The streams, length and size.</param>
    /// <param name="Cues">The captions.</param>
    /// <param name="Size">The size of the shown pictures.</param>
    private sealed record ProbeResult(MediaInfo Info, List<CaptionCue> Cues, FrameSize Size);

    /// <summary>The sound of a run, which releases its decoder and output when the run is over.</summary>
    /// <param name="Sound">The sound.</param>
    private sealed record OpenedSound(ManagedSound Sound) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose()
        {
            Sound.Decoder.Dispose();
            Sound.Output.Dispose();
        }
    }

    /// <summary>The pictures of a run, which releases the decoder when the run is over.</summary>
    /// <param name="Picture">The pictures.</param>
    private sealed record OpenedPicture(ManagedPicture Picture) : IDisposable
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => Picture.Decoder.Dispose();
    }
}
