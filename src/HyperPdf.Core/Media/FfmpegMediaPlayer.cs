// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>
/// Plays the sound and video of a file with the FFmpeg programs found on the computer, for a computer where the FFmpeg libraries
/// shipped with the app cannot be loaded. <c>ffplay</c> plays the sound, and <c>ffmpeg</c> decodes the pictures, which the view
/// draws. Playing never starts by itself: the reader presses Play. The pictures and the sound start together and are not locked
/// to one clock, so they can drift apart by a fraction of a second.
/// </summary>
[DebuggerDisplay("FfmpegMediaPlayer: {Snapshot}")]
public sealed class FfmpegMediaPlayer : MediaPlayerBase
{
    /// <summary>The pictures decoded each second at normal motion.</summary>
    private const int NormalFramesPerSecond = 15;

    /// <summary>The programs one run starts at most: one for the picture and one for the sound.</summary>
    private const int ProgramCount = 2;

    /// <summary>A run of sound that ends sooner than this after it starts did not play.</summary>
    private static readonly TimeSpan SoundStartTime = TimeSpan.FromSeconds(1);

    /// <summary>A file shorter than this is not taken as a failed sound when it ends at once.</summary>
    private static readonly TimeSpan ShortestFile = TimeSpan.FromSeconds(3);

    /// <summary>The tools that play the file.</summary>
    private readonly MediaTools _tools;

    /// <summary>The pictures decoded each second.</summary>
    private readonly int _framesPerSecond;

    /// <summary>Initializes a new instance of the <see cref="FfmpegMediaPlayer"/> class.</summary>
    /// <param name="tools">The FFmpeg programs.</param>
    /// <param name="reduceMotion">Whether to show a picture only once a second, for readers who asked for less motion.</param>
    public FfmpegMediaPlayer(MediaTools tools, bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools;
        _framesPerSecond = reduceMotion ? 1 : NormalFramesPerSecond;
    }

    /// <inheritdoc />
    public override async Task<bool> OpenAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        Halt();
        var probed = await MediaProcesses.RunAsync(_tools.Ffprobe, FfmpegArguments.Probe(path), cancellationToken).ConfigureAwait(false);
        var info = probed is null ? default : MediaProbe.Parse(probed);
        if (!info.HasVideo && !info.HasAudio)
        {
            return Fail("FFmpeg could not read any sound or picture from this file.");
        }

        var cues = info.HasCaptions ? await ReadCaptionsAsync(path, cancellationToken).ConfigureAwait(false) : [];
        Opened(path, info, cues, info.HasVideo ? FfmpegArguments.Fit(info.Width, info.Height) : default);
        return true;
    }

    /// <inheritdoc />
    protected override async Task<string?> PlayAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken)
    {
        var processes = new List<Process>(ProgramCount);
        try
        {
            var playing = new List<Task>(ProgramCount);
            if (pump is not null && MediaProcesses.Start(_tools.Ffmpeg, FfmpegArguments.Video(path, start, FrameSize, _framesPerSecond)) is { } video)
            {
                processes.Add(video);
                playing.Add(pump.RunAsync(video.StandardOutput.BaseStream, Publish, cancellationToken));
                _ = MediaProcesses.DrainAsync(video.StandardError, cancellationToken);
            }

            var hasSound = info.HasAudio && MediaProcesses.Start(_tools.Ffplay, FfmpegArguments.Audio(path, start)) is { } audio && Track(audio, playing, processes, cancellationToken);
            if (playing.Count == 0)
            {
                return "FFmpeg could not start to play this file.";
            }

            await Task.WhenAll(playing).ConfigureAwait(false);
            return hasSound && SoundFailed(start, info.Duration) ? "The sound could not be played on this computer." : null;
        }
        finally
        {
            foreach (var process in processes)
            {
                MediaProcesses.Stop(process);
                process.Dispose();
            }
        }
    }

    /// <summary>Follows the sound program until it ends.</summary>
    /// <param name="sound">The sound program.</param>
    /// <param name="playing">The work the run waits for.</param>
    /// <param name="processes">The programs to stop when the run is over.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool Track(Process sound, List<Task> playing, List<Process> processes, CancellationToken cancellationToken)
    {
        processes.Add(sound);
        playing.Add(sound.WaitForExitAsync(cancellationToken));
        _ = MediaProcesses.DrainAsync(sound.StandardError, cancellationToken);
        return true;
    }

    /// <summary>Reads the captions of a file; a file whose captions cannot be read plays without them.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>The captions.</returns>
    private async Task<List<CaptionCue>> ReadCaptionsAsync(string path, CancellationToken cancellationToken)
    {
        var text = await MediaProcesses.RunAsync(_tools.Ffmpeg, FfmpegArguments.Captions(path), cancellationToken).ConfigureAwait(false);
        return text is null ? [] : SrtCaptions.Parse(text);
    }

    /// <summary>Works out whether the sound program stopped almost at once on a file that should have played for longer, which happens when the computer has no audio output.</summary>
    /// <param name="start">Where the run began.</param>
    /// <param name="duration">How long the file plays, or zero when that is not known.</param>
    /// <returns><see langword="true"/> when the sound did not play.</returns>
    private bool SoundFailed(TimeSpan start, TimeSpan duration) => duration - start > ShortestFile && SinceRunStarted < SoundStartTime;
}
