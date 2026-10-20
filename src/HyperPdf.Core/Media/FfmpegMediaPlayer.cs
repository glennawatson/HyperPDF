// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Media;

/// <summary>
/// Plays the sound and video of a file with the FFmpeg programs found on the computer. <c>ffplay</c> plays the sound, and
/// <c>ffmpeg</c> decodes the pictures, which the view draws. Playing never starts by itself: the reader presses Play. The
/// pictures and the sound start together and are not locked to one clock, so they can drift apart by a fraction of a second.
/// </summary>
[DebuggerDisplay("FfmpegMediaPlayer: {Snapshot}")]
public sealed class FfmpegMediaPlayer : IDisposable
{
    /// <summary>How often the position is reported while a file plays, in milliseconds.</summary>
    private const int TickMilliseconds = 250;

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

    /// <summary>Raises the snapshot whenever the state, position, caption or picture changes.</summary>
    private readonly Signal<MediaSnapshot> _changes = new();

    /// <summary>Guards the fields that describe the open file and where it is playing.</summary>
    private readonly Lock _gate = new();

    /// <summary>Makes calls to the signal one at a time, because frames and ticks arrive on different threads.</summary>
    private readonly Lock _publishGate = new();

    /// <summary>The caption cues of the open file.</summary>
    private List<CaptionCue> _cues = [];

    /// <summary>Stops the processes of the current run.</summary>
    private CancellationTokenSource? _run;

    /// <summary>The open file.</summary>
    private string _path = string.Empty;

    /// <summary>What the open file holds.</summary>
    private MediaInfo _info;

    /// <summary>Holds the latest picture, or <see langword="null"/> for a file without one.</summary>
    private FramePump? _pump;

    /// <summary>Where the player is in its life.</summary>
    private MediaPlayerState _state;

    /// <summary>The position at which the current run started.</summary>
    private TimeSpan _anchor;

    /// <summary>The time stamp at which the current run started.</summary>
    private long _startedAt;

    /// <summary>Whether the player was disposed.</summary>
    private bool _disposed;

    /// <summary>Initializes a new instance of the <see cref="FfmpegMediaPlayer"/> class.</summary>
    /// <param name="tools">The FFmpeg programs.</param>
    /// <param name="reduceMotion">Whether to show a picture only once a second, for readers who asked for less motion.</param>
    public FfmpegMediaPlayer(MediaTools tools, bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(tools);
        _tools = tools;
        _framesPerSecond = reduceMotion ? 1 : NormalFramesPerSecond;
        Changes = new(_changes);
    }

    /// <summary>Gets the changes to the snapshot, raised on a thread pool thread.</summary>
    public AsObservableSignal<MediaSnapshot> Changes { get; }

    /// <summary>Gets what went wrong, in plain words, or <see langword="null"/>.</summary>
    public string? Problem { get; private set; }

    /// <summary>Gets what the open file holds.</summary>
    public MediaInfo Info
    {
        get
        {
            lock (_gate)
            {
                return _info;
            }
        }
    }

    /// <summary>Gets the size of the pictures, or an empty size for a file without a picture.</summary>
    public FrameSize FrameSize { get; private set; }

    /// <summary>Gets the current snapshot.</summary>
    public MediaSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                var position = PositionLocked();
                return new(_state, position, _info.Duration, SrtCaptions.TextAt(_cues, position), _pump?.Version ?? 0);
            }
        }
    }

    /// <summary>Opens a file and reads its length, size and captions. Nothing plays.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="cancellationToken">Stops opening.</param>
    /// <returns><see langword="true"/> when the file has sound or a picture that can be played.</returns>
    public async Task<bool> OpenAsync(string path, CancellationToken cancellationToken)
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
        var size = info.HasVideo ? FfmpegArguments.Fit(info.Width, info.Height) : default;
        lock (_gate)
        {
            _path = path;
            _info = info;
            _cues = cues;
            _pump = size.IsEmpty ? null : new FramePump(size);
            _anchor = TimeSpan.Zero;
            _state = MediaPlayerState.Ready;
        }

        FrameSize = size;
        Problem = null;
        Publish();
        return true;
    }

    /// <summary>Copies the latest picture.</summary>
    /// <param name="destination">Receives blue, green, red and alpha bytes; it must hold at least <c>FrameSize.ByteCount</c> bytes.</param>
    /// <returns><see langword="true"/> when a picture was copied.</returns>
    public bool TryCopyFrame(Span<byte> destination)
    {
        FramePump? pump;
        lock (_gate)
        {
            pump = _pump;
        }

        return pump?.TryCopy(destination) ?? false;
    }

    /// <summary>Starts playing, or carries on after a pause. Starts again from the beginning after the end.</summary>
    public void Play()
    {
        lock (_gate)
        {
            if (_disposed || _state is not (MediaPlayerState.Ready or MediaPlayerState.Paused or MediaPlayerState.Ended))
            {
                return;
            }

            _anchor = _state == MediaPlayerState.Ended ? TimeSpan.Zero : _anchor;
            StartRunLocked();
        }

        Publish();
    }

    /// <summary>Pauses at the current position.</summary>
    public void Pause()
    {
        lock (_gate)
        {
            if (_state != MediaPlayerState.Playing)
            {
                return;
            }

            _anchor = PositionLocked();
            CancelRunLocked();
            _state = MediaPlayerState.Paused;
        }

        Publish();
    }

    /// <summary>Stops and goes back to the start.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            if (_state is not (MediaPlayerState.Playing or MediaPlayerState.Paused or MediaPlayerState.Ended))
            {
                return;
            }

            CancelRunLocked();
            _anchor = TimeSpan.Zero;
            _state = MediaPlayerState.Ready;
        }

        Publish();
    }

    /// <summary>Moves to a position. A file that is playing carries on from there.</summary>
    /// <param name="position">The position; it is kept within the file.</param>
    public void Seek(TimeSpan position)
    {
        lock (_gate)
        {
            if (_disposed || _state is MediaPlayerState.Idle or MediaPlayerState.Failed)
            {
                return;
            }

            _anchor = Clamp(position, _info.Duration);
            if (_state == MediaPlayerState.Playing)
            {
                CancelRunLocked();
                StartRunLocked();
            }
            else if (_state == MediaPlayerState.Ended)
            {
                _state = MediaPlayerState.Paused;
            }
        }

        Publish();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelRunLocked();
        }

        _changes.OnCompleted();
        _changes.Dispose();
    }

    /// <summary>Keeps a position within a file.</summary>
    /// <param name="position">The position.</param>
    /// <param name="duration">How long the file plays, or zero when that is not known.</param>
    /// <returns>The position, not before the start and not after the end.</returns>
    private static TimeSpan Clamp(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return duration > TimeSpan.Zero && position > duration ? duration : position;
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

    /// <summary>Stops anything playing and forgets the open file.</summary>
    private void Halt()
    {
        lock (_gate)
        {
            CancelRunLocked();
            _state = MediaPlayerState.Idle;
            _info = default;
            _cues = [];
            _pump = null;
            _anchor = TimeSpan.Zero;
        }
    }

    /// <summary>Records a failure and tells the listeners.</summary>
    /// <param name="problem">What went wrong, in plain words.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    private bool Fail(string problem)
    {
        lock (_gate)
        {
            _state = MediaPlayerState.Failed;
        }

        Problem = problem;
        Publish();
        return false;
    }

    /// <summary>Gets the position, which moves with the clock while the file plays.</summary>
    /// <returns>The position, kept within the file.</returns>
    private TimeSpan PositionLocked()
    {
        if (_state != MediaPlayerState.Playing)
        {
            return _anchor;
        }

        var position = _anchor + Stopwatch.GetElapsedTime(_startedAt);
        return _info.Duration > TimeSpan.Zero && position > _info.Duration ? _info.Duration : position;
    }

    /// <summary>Starts the processes that play the file from the anchor.</summary>
    private void StartRunLocked()
    {
        _run = new();
        _startedAt = Stopwatch.GetTimestamp();
        _state = MediaPlayerState.Playing;
        _ = RunAsync(_path, _anchor, _info, _pump, _run.Token);
    }

    /// <summary>Stops the processes of the current run.</summary>
    private void CancelRunLocked()
    {
        if (_run is null)
        {
            return;
        }

        _run.Cancel();
        _run.Dispose();
        _run = null;
    }

    /// <summary>Reports the snapshot to the listeners.</summary>
    private void Publish()
    {
        lock (_publishGate)
        {
            if (!_changes.IsDisposed)
            {
                _changes.OnNext(Snapshot);
            }
        }
    }

    /// <summary>Runs the processes that play a file until they end or the run is cancelled.</summary>
    /// <param name="path">The file.</param>
    /// <param name="start">Where to begin.</param>
    /// <param name="info">What the file holds.</param>
    /// <param name="pump">Receives the pictures, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>A task that completes when the run is over.</returns>
    private async Task RunAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken)
    {
        var processes = new List<Process>(ProgramCount);
        try
        {
            _ = TickAsync(cancellationToken);
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
                Finish("FFmpeg could not start to play this file.", cancellationToken);
                return;
            }

            await Task.WhenAll(playing).ConfigureAwait(false);
            Finish(hasSound && SoundFailed(start, info.Duration) ? "The sound could not be played on this computer." : null, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Pausing, stopping or seeking cancels the run, which is the end of it.
        }
        catch (IOException)
        {
            // A program closed its output while the run was being stopped.
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

    /// <summary>Reports the position at a steady pace until the run is cancelled.</summary>
    /// <param name="cancellationToken">Ends the ticking.</param>
    /// <returns>A task that completes when the run is cancelled.</returns>
    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMilliseconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Publish();
            }
        }
        catch (OperationCanceledException)
        {
            // The run ended.
        }
    }

    /// <summary>Works out whether the sound program stopped almost at once on a file that should have played for longer, which happens when the computer has no audio output.</summary>
    /// <param name="start">Where the run began.</param>
    /// <param name="duration">How long the file plays, or zero when that is not known.</param>
    /// <returns><see langword="true"/> when the sound did not play.</returns>
    private bool SoundFailed(TimeSpan start, TimeSpan duration)
    {
        lock (_gate)
        {
            return duration - start > ShortestFile && Stopwatch.GetElapsedTime(_startedAt) < SoundStartTime;
        }
    }

    /// <summary>Marks the file as ended, or as failed, unless the run was cancelled.</summary>
    /// <param name="problem">Why the file could not play, or <see langword="null"/> when it played to its end.</param>
    /// <param name="cancellationToken">The run's token.</param>
    private void Finish(string? problem, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (cancellationToken.IsCancellationRequested || _disposed)
            {
                return;
            }

            _anchor = problem is null ? _info.Duration : _anchor;
            _state = problem is null ? MediaPlayerState.Ended : MediaPlayerState.Failed;
        }

        Problem = problem;
        Publish();
    }
}
