// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Media;
using HyperPdf.Media;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// Plays the chosen sound or video inside the viewer with the decoders that ship with the app. Nothing plays until the
/// reader presses Play. When media cannot be played the buttons stay off and the panel says so; saving the media and
/// opening it with the system player still work.
/// </summary>
[DebuggerDisplay("MediaPlayerViewModel: {State}")]
public sealed partial class MediaPlayerViewModel : ReactiveObject, IDisposable
{
    /// <summary>How far the skip buttons move, in seconds.</summary>
    private const double SkipSeconds = 10;

    /// <summary>How far a slider value may be from the position and still be taken as the player moving it, in seconds.</summary>
    private const double FollowTolerance = 0.6;

    /// <summary>What the panel says when media cannot be played here.</summary>
    private const string CannotPlay =
        "Sound and video cannot be played here. You can still save the media or open it with your system's player.";

    /// <summary>What the panel says before anything is chosen.</summary>
    private const string ChooseSomething = "Choose a sound or video, then press Play.";

    /// <summary>The seconds in a minute, for showing the time.</summary>
    private const int SecondsPerMinute = 60;

    /// <summary>The owning media list.</summary>
    private readonly DocumentMediaViewModel _media;

    /// <summary>Makes the player of a piece of media.</summary>
    private readonly MediaPlaybackFactory _factory;

    /// <summary>Owns the subscriptions.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether Play can run.</summary>
    private readonly IObservable<bool> _canPlay;

    /// <summary>Whether Pause can run.</summary>
    private readonly IObservable<bool> _canPause;

    /// <summary>Whether Stop and the skip buttons can run.</summary>
    private readonly IObservable<bool> _canMove;

    /// <summary>The player of the chosen media, or <see langword="null"/> when none is loaded.</summary>
    private IMediaPlayback? _player;

    /// <summary>Where the reader is dragging to, in seconds.</summary>
    private double _seekTarget;

    /// <summary>Whether the reader is moving the position bar.</summary>
    private bool _seeking;

    /// <summary>Follows the loaded player.</summary>
    private IDisposable? _playerSubscription;

    /// <summary>The folder holding the file the player has open.</summary>
    private string? _folder;

    /// <summary>The <see cref="DocumentMedia.Index"/> of the loaded media, or -1.</summary>
    private int _loadedIndex = -1;

    /// <summary>Initializes a new instance of the <see cref="MediaPlayerViewModel"/> class.</summary>
    /// <param name="media">The owning media list.</param>
    /// <param name="factory">Makes the player of a piece of media.</param>
    public MediaPlayerViewModel(DocumentMediaViewModel media, MediaPlaybackFactory factory)
    {
        _media = media;
        _factory = factory;
        Status = IdleStatus();
        Clock = string.Empty;
        Caption = string.Empty;
        _canPlay = this.WhenChanged(static vm => vm.CanPlay);
        _canPause = this.WhenChanged(static vm => vm.CanPause);
        _canMove = this.WhenChanged(static vm => vm.CanMove);
        _subscriptions.Add(media.WhenChanged(static vm => vm.Selected).SubscribeSafe(_ => SelectionChanged(), ErrorTrace.Write));
    }

    /// <summary>Gets a value indicating whether media can be played here.</summary>
    public bool IsAvailable => _factory.IsAvailable;

    /// <summary>Gets the size of the pictures, or an empty size when the media has no picture.</summary>
    public FrameSize FrameSize => _player?.FrameSize ?? default;

    /// <summary>Gets where the player is in its life.</summary>
    [Reactive]
    public partial MediaPlayerState State { get; private set; }

    /// <summary>Gets what the panel says about the player, in plain words.</summary>
    [Reactive]
    public partial string Status { get; private set; }

    /// <summary>Gets the position and length, such as <c>0:03 / 0:10</c>.</summary>
    [Reactive]
    public partial string Clock { get; private set; }

    /// <summary>Gets the position in seconds.</summary>
    [Reactive]
    public partial double PositionSeconds { get; private set; }

    /// <summary>Gets the length in seconds, or zero when it is not known.</summary>
    [Reactive]
    public partial double DurationSeconds { get; private set; }

    /// <summary>Gets the words of the caption showing now.</summary>
    [Reactive]
    public partial string Caption { get; private set; }

    /// <summary>Gets a number that grows with each new picture.</summary>
    [Reactive]
    public partial int FrameVersion { get; private set; }

    /// <summary>Gets a value indicating whether the loaded media has a picture.</summary>
    [Reactive]
    public partial bool HasPicture { get; private set; }

    /// <summary>Gets a value indicating whether Play can run.</summary>
    [Reactive]
    public partial bool CanPlay { get; private set; }

    /// <summary>Gets a value indicating whether Pause can run.</summary>
    [Reactive]
    public partial bool CanPause { get; private set; }

    /// <summary>Gets a value indicating whether Stop and the skip buttons can run.</summary>
    [Reactive]
    public partial bool CanMove { get; private set; }

    /// <summary>Copies the latest picture.</summary>
    /// <param name="destination">Receives blue, green, red and alpha bytes.</param>
    /// <returns><see langword="true"/> when a picture was copied.</returns>
    public bool TryCopyFrame(Span<byte> destination) => _player?.TryCopyFrame(destination) ?? false;

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        Release();
    }

    /// <summary>
    /// Notes where the reader has moved the position bar to. A value close to the playing position is the bar following the
    /// player, not the reader, so it is ignored.
    /// </summary>
    /// <param name="seconds">The value of the bar, in seconds.</param>
    public void NoteSeek(double seconds)
    {
        if (_player is null || Math.Abs(seconds - PositionSeconds) <= FollowTolerance)
        {
            return;
        }

        _seeking = true;
        _seekTarget = seconds;
    }

    /// <summary>Goes to the position the reader moved the bar to, and lets the bar follow the player again.</summary>
    public void CompleteSeek()
    {
        if (!_seeking)
        {
            return;
        }

        _seeking = false;
        PositionSeconds = _seekTarget;
        _player?.Seek(TimeSpan.FromSeconds(_seekTarget));
    }

    /// <summary>Stops and forgets the loaded media; called when the document loads again.</summary>
    internal void Release()
    {
        _playerSubscription?.Dispose();
        _playerSubscription = null;
        _player?.Dispose();
        _player = null;
        _loadedIndex = -1;
        _seeking = false;
        HasPicture = false;
        Caption = string.Empty;
        Clock = string.Empty;
        PositionSeconds = 0;
        DurationSeconds = 0;
        State = MediaPlayerState.Idle;
        DeleteFolder();
        SelectionChanged();
    }

    /// <summary>Shows a time as minutes and seconds.</summary>
    /// <param name="time">The time.</param>
    /// <returns>The time, such as <c>1:05</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Format(TimeSpan time) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalMinutes}:{(int)time.TotalSeconds % SecondsPerMinute:00}");

    /// <summary>Shows a player snapshot.</summary>
    /// <param name="snapshot">The snapshot.</param>
    private void Apply(MediaSnapshot snapshot)
    {
        State = snapshot.State;

        // While the reader drags the position bar it stays where they put it.
        PositionSeconds = _seeking ? PositionSeconds : snapshot.Position.TotalSeconds;
        DurationSeconds = snapshot.Duration.TotalSeconds;
        Caption = snapshot.Caption;
        FrameVersion = snapshot.FrameVersion;
        Clock = $"{Format(snapshot.Position)} / {Format(snapshot.Duration)}";
        Status = snapshot.State switch
        {
            MediaPlayerState.Playing => "Playing",
            MediaPlayerState.Paused => "Paused",
            MediaPlayerState.Ended => "Finished. Press Play to watch or listen again.",
            MediaPlayerState.Failed => _player?.Problem ?? "This media could not be played.",
            _ => "Ready. Press Play.",
        };
        Refresh();
    }

    /// <summary>Works out which buttons can be used.</summary>
    private void Refresh()
    {
        var active = State is MediaPlayerState.Playing or MediaPlayerState.Paused;
        CanPlay = IsAvailable && _media.Selected is not null && State != MediaPlayerState.Playing;
        CanPause = State == MediaPlayerState.Playing;
        CanMove = active || State == MediaPlayerState.Ended;
    }

    /// <summary>Puts away the loaded media when the reader chooses another one.</summary>
    private void SelectionChanged()
    {
        if (_player is not null && _media.Selected?.Index != _loadedIndex)
        {
            Release();
            return;
        }

        if (_player is null)
        {
            Status = IdleStatus();
        }

        Refresh();
    }

    /// <summary>Says what the panel should say when nothing is loaded.</summary>
    /// <returns>The words.</returns>
    private string IdleStatus()
    {
        if (!IsAvailable)
        {
            return CannotPlay;
        }

        return _media.Selected is null ? ChooseSomething : "Ready. Press Play.";
    }

    /// <summary>Writes the chosen media to a temporary file and opens it in a new player.</summary>
    /// <param name="media">The media.</param>
    /// <returns><see langword="true"/> when the media can be played.</returns>
    private async Task<bool> LoadAsync(DocumentMedia media)
    {
        Release();
        Status = "Opening…";
        if (_factory.Create(_media.ReduceMotion) is not { } player)
        {
            Status = CannotPlay;
            return false;
        }

        var extracted = await _media.ExtractAsync(media).ConfigureAwait(true);
        if (extracted is null)
        {
            Status = "This media could not be read from the document.";
            player.Dispose();
            return false;
        }

        _folder = Path.GetDirectoryName(extracted);
        if (!await player.OpenAsync(extracted, _media.LifetimeToken).ConfigureAwait(true))
        {
            Status = player.Problem ?? "This media could not be played.";
            player.Dispose();
            DeleteFolder();
            return false;
        }

        _player = player;
        _loadedIndex = media.Index;
        HasPicture = !player.FrameSize.IsEmpty;
        _playerSubscription = player.Changes.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(Apply, ErrorTrace.Write);
        Apply(player.Snapshot);
        return true;
    }

    /// <summary>Deletes the temporary file of the loaded media.</summary>
    private void DeleteFolder()
    {
        if (_folder is null)
        {
            return;
        }

        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // A player that is still closing may hold the file; the operating system clears the temporary folder later.
        }
        catch (UnauthorizedAccessException)
        {
            // The same: the file stays until the temporary folder is cleared.
        }

        _folder = null;
    }

    /// <summary>Plays the chosen media, or carries on after a pause. Nothing plays until this runs.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canPlay))]
    private async Task PlayAsync()
    {
        if (!IsAvailable || _media.Selected is not { } selected)
        {
            return;
        }

        if ((_player is null || _loadedIndex != selected.Index) && !await LoadAsync(selected).ConfigureAwait(true))
        {
            return;
        }

        _player?.Play();
    }

    /// <summary>Pauses at the current position.</summary>
    [ReactiveCommand(CanExecute = nameof(_canPause))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Pause() => _player?.Pause();

    /// <summary>Stops and goes back to the start.</summary>
    [ReactiveCommand(CanExecute = nameof(_canMove))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Stop() => _player?.Stop();

    /// <summary>Goes back ten seconds.</summary>
    [ReactiveCommand(CanExecute = nameof(_canMove))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipBack() => _player?.Seek(TimeSpan.FromSeconds(PositionSeconds - SkipSeconds));

    /// <summary>Goes forward ten seconds.</summary>
    [ReactiveCommand(CanExecute = nameof(_canMove))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SkipForward() => _player?.Seek(TimeSpan.FromSeconds(PositionSeconds + SkipSeconds));
}
