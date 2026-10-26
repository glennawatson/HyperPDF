// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Media;

/// <summary>
/// The parts every media player shares: its state, position, captions, picture and the snapshots it reports. A player that
/// decodes the file supplies how to open it and how to play it from a position until it ends.
/// </summary>
[DebuggerDisplay("MediaPlayerBase: {Snapshot}")]
public abstract class MediaPlayerBase : IMediaPlayback
{
    /// <summary>How often the position is reported while a file plays, in milliseconds.</summary>
    private const int TickMilliseconds = 250;

    /// <summary>Raises the snapshot whenever the state, position, caption or picture changes.</summary>
    private readonly Signal<MediaSnapshot> _changes = new();

    /// <summary>Guards the fields that describe the open file and where it is playing.</summary>
    private readonly Lock _gate = new();

    /// <summary>Makes calls to the signal one at a time, because frames and ticks arrive on different threads.</summary>
    private readonly Lock _publishGate = new();

    /// <summary>The caption cues of the open file.</summary>
    private List<CaptionCue> _cues = [];

    /// <summary>Stops the current run.</summary>
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

    /// <summary>The retained source text of the latest decoder failure.</summary>
    private string? _problem;

    /// <summary>Initializes a new instance of the <see cref="MediaPlayerBase"/> class.</summary>
    protected MediaPlayerBase() => Changes = new(_changes);

    /// <inheritdoc />
    public AsObservableSignal<MediaSnapshot> Changes { get; }

    /// <inheritdoc />
    public string? Problem { get => ProblemMessage.Value; private set => _problem = value; }

    /// <summary>Gets the retained failure message, preserving its meaning when the language changes.</summary>
    public LocalizedMessage ProblemMessage => DescribeProblem(_problem);

    /// <inheritdoc />
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

    /// <inheritdoc />
    public FrameSize FrameSize { get; private set; }

    /// <inheritdoc />
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

    /// <summary>Gets how long ago the current run started.</summary>
    protected TimeSpan SinceRunStarted
    {
        get
        {
            lock (_gate)
            {
                return Stopwatch.GetElapsedTime(_startedAt);
            }
        }
    }

    /// <inheritdoc />
    public abstract Task<bool> OpenAsync(string path, CancellationToken cancellationToken);

    /// <inheritdoc />
    public bool TryCopyFrame(Span<byte> destination)
    {
        FramePump? pump;
        lock (_gate)
        {
            pump = _pump;
        }

        return pump?.TryCopy(destination) ?? false;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

        // Publish checks and raises under this gate, so completing here cannot race a picture thread's last frame.
        lock (_publishGate)
        {
            _changes.OnCompleted();
            _changes.Dispose();
        }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Keeps a position within a file.</summary>
    /// <param name="position">The position.</param>
    /// <param name="duration">How long the file plays, or zero when that is not known.</param>
    /// <returns>The position, not before the start and not after the end.</returns>
    protected static TimeSpan Clamp(TimeSpan position, TimeSpan duration)
    {
        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return duration > TimeSpan.Zero && position > duration ? duration : position;
    }

    /// <summary>Describes a decoder failure without treating third party text as a translation key.</summary>
    /// <param name="problem">The decoder failure.</param>
    /// <returns>The retained failure message.</returns>
    protected virtual LocalizedMessage DescribeProblem(string? problem) => LocalizedMessage.Literal(problem);

    /// <summary>Plays the open file from a position until it ends or the token is cancelled.</summary>
    /// <param name="path">The file.</param>
    /// <param name="start">Where to begin.</param>
    /// <param name="info">What the file holds.</param>
    /// <param name="pump">Receives the pictures, or <see langword="null"/> for a file without a picture.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>A task that completes with <see langword="null"/> when the file played to its end, or with what went wrong.</returns>
    protected abstract Task<string?> PlayAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken);

    /// <summary>Works out how far into the file a run has played. The default follows the wall clock.</summary>
    /// <param name="anchor">The position at which the run started.</param>
    /// <param name="startedAt">The time stamp at which the run started.</param>
    /// <returns>The position.</returns>
    protected virtual TimeSpan ClockPosition(TimeSpan anchor, long startedAt) => anchor + Stopwatch.GetElapsedTime(startedAt);

    /// <summary>Releases what a derived player holds; called once, after the player stops.</summary>
    /// <param name="disposing">Always <see langword="true"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>Stops anything playing and forgets the open file.</summary>
    protected void Halt()
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

    /// <summary>Records the file that was opened and tells the listeners.</summary>
    /// <param name="path">The file.</param>
    /// <param name="info">What it holds.</param>
    /// <param name="cues">Its captions.</param>
    /// <param name="size">The size of its pictures, or an empty size.</param>
    protected void Opened(string path, MediaInfo info, List<CaptionCue> cues, FrameSize size)
    {
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
    }

    /// <summary>Records a failure and tells the listeners.</summary>
    /// <param name="problem">What went wrong, in plain words.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    protected bool Fail(string problem)
    {
        lock (_gate)
        {
            _state = MediaPlayerState.Failed;
        }

        Problem = problem;
        Publish();
        return false;
    }

    /// <summary>Reports the snapshot to the listeners.</summary>
    protected void Publish()
    {
        lock (_publishGate)
        {
            if (!_changes.IsDisposed)
            {
                _changes.OnNext(Snapshot);
            }
        }
    }

    /// <summary>Gets the position, which moves with the clock while the file plays.</summary>
    /// <returns>The position, kept within the file.</returns>
    private TimeSpan PositionLocked() =>
        _state == MediaPlayerState.Playing ? Clamp(ClockPosition(_anchor, _startedAt), _info.Duration) : _anchor;

    /// <summary>Starts a run from the anchor.</summary>
    private void StartRunLocked()
    {
        _run = new();
        _startedAt = Stopwatch.GetTimestamp();
        _state = MediaPlayerState.Playing;
        _ = RunAsync(_path, _anchor, _info, _pump, _run.Token);
    }

    /// <summary>Stops the current run.</summary>
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

    /// <summary>Runs the file and reports how it ended.</summary>
    /// <param name="path">The file.</param>
    /// <param name="start">Where to begin.</param>
    /// <param name="info">What the file holds.</param>
    /// <param name="pump">Receives the pictures, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>A task that completes when the run is over.</returns>
    private async Task RunAsync(string path, TimeSpan start, MediaInfo info, FramePump? pump, CancellationToken cancellationToken)
    {
        try
        {
            _ = TickAsync(cancellationToken);
            var problem = await PlayAsync(path, start, info, pump, cancellationToken).ConfigureAwait(false);
            Finish(problem, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Pausing, stopping or seeking cancels the run, which is the end of it.
        }
        catch (IOException)
        {
            // A program closed its output while the run was being stopped.
        }
    }

    /// <summary>Reports the position at a steady pace until the run is cancelled.</summary>
    /// <param name="cancellationToken">Ends the ticking.</param>
    /// <returns>A task that completes when the run is cancelled.</returns>
    private async Task TickAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TickMilliseconds));

        // Disposing the timer ends the wait with false, which avoids the exception a cancelled wait throws each time a run stops.
        await using var stop = cancellationToken.UnsafeRegister(static state => ((PeriodicTimer)state!).Dispose(), timer);
        while (await timer.WaitForNextTickAsync(CancellationToken.None).ConfigureAwait(false))
        {
            Publish();
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
