// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Media;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Sidebar;

/// <summary>
/// A tab's sound and video. The media panel only appears for documents that have some. Media never plays by itself: the page
/// shows the annotation's poster picture, and the reader can press Play to play it in the panel (when the app has a decoder for it),
/// save it, or hand it to the system player after confirming.
/// </summary>
[DebuggerDisplay("DocumentMediaViewModel: {Items.Count} items")]
public sealed partial class DocumentMediaViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The temporary folder opened media is written to.</summary>
    private const string TempFolderName = "hyperpdf-media";

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Whether an item is selected, so it can be saved or opened.</summary>
    private readonly IObservable<bool> _canAct;

    /// <summary>How media plays in the panel.</summary>
    private readonly MediaPlaybackFactory _playback;

    /// <summary>The player, made when first needed.</summary>
    private MediaPlayerViewModel? _player;

    /// <summary>Initializes a new instance of the <see cref="DocumentMediaViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public DocumentMediaViewModel(DocumentTabViewModel owner)
        : this(owner, owner.MediaPlayback)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DocumentMediaViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="playback">How media plays in the panel.</param>
    public DocumentMediaViewModel(DocumentTabViewModel owner, MediaPlaybackFactory playback)
    {
        _owner = owner;
        _canAct = this.WhenChanged(static vm => vm.Selected).Select(static item => item is not null);
        _playback = playback;
    }

    /// <summary>Gets the document's media.</summary>
    public ObservableCollection<DocumentMedia> Items { get; } = [];

    /// <summary>Gets the player that plays the chosen media in the panel.</summary>
    public MediaPlayerViewModel Player
    {
        get
        {
            if (_player is null)
            {
                _player = new(this, _playback);
                HasPlayer = true;
            }

            return _player;
        }
    }

    /// <summary>Gets a value indicating whether the player was made, which is when media was first played.</summary>
    [Reactive]
    public partial bool HasPlayer { get; private set; }

    /// <summary>Gets a value indicating whether the document has sound or video.</summary>
    [Reactive]
    public partial bool HasMedia { get; private set; }

    /// <summary>Gets or sets the selected item.</summary>
    [Reactive]
    public partial DocumentMedia? Selected { get; set; }

    /// <summary>Gets the interaction asking where to save a file; the input is the suggested name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets the question that shows the window where the chosen media plays. It is answered when the window is put away.</summary>
    public Interaction<MediaPlayerViewModel, RxVoid> ShowPlayerInteraction { get; } = new();

    /// <summary>Gets a value indicating whether the reader asked for less motion, which makes video show a picture only once a second.</summary>
    internal bool ReduceMotion => _owner.ReduceMotion;

    /// <summary>Gets the token that is cancelled when the tab closes.</summary>
    internal CancellationToken LifetimeToken => _owner.LifetimeToken;

    /// <summary>Lists the document's media; called when the document loads.</summary>
    public void Refresh()
    {
        _player?.Release();
        Items.Clear();
        Selected = null;
        if (DocumentFeature<IDocumentMediaSource>.Get(_owner.TryGetDocument()) is { } source)
        {
            foreach (var media in source.GetMedia())
            {
                Items.Add(media);
            }
        }

        HasMedia = Items.Count > 0;
        if (!HasMedia && _owner.IsMediaMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>Saves a piece of media to a path.</summary>
    /// <param name="media">The media.</param>
    /// <param name="path">Where to save it.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public IObservable<bool> SaveTo(DocumentMedia media, string path)
    {
        ArgumentNullException.ThrowIfNull(media);
        return Write(media, path).Do(saved => Saved(media, saved));
    }

    /// <summary>
    /// Writes a piece of media to a temporary file and hands it to the system player. The file opens only after the reader
    /// confirms, and a program or script is never started.
    /// </summary>
    /// <param name="media">The media.</param>
    /// <returns>A cold signal that emits <see langword="true"/> when the file was handed to the system, then completes.</returns>
    public IObservable<bool> Open(DocumentMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return Extract(media).SwitchMap(path => path is null ? Signal.Return(false) : _owner.Links.OpenFile(path, 0));
    }

    /// <summary>Releases the player and its temporary file.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _player?.Dispose();

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        LocalisationRefresh.RefreshCore(_owner, visited);
        LocalisationRefresh.RefreshCore(_player, visited);
    }

    /// <summary>Writes a piece of media to a new temporary file.</summary>
    /// <param name="media">The media.</param>
    /// <returns>A cold signal giving the full path of the file, or <see langword="null"/> when it could not be written; the notice says why.</returns>
    internal IObservable<string?> Extract(DocumentMedia media)
    {
        // Each extraction gets its own folder, so files with the same name never replace one another.
        var folder = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString("N"));
        try
        {
            _ = Directory.CreateDirectory(folder);
        }
        catch (IOException ex)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Could not open {0}: {1}", media.FileName, ex.Message));
            return Signal.Return<string?>(null);
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Could not open {0}: {1}", media.FileName, ex.Message));
            return Signal.Return<string?>(null);
        }

        var path = Path.Combine(folder, media.FileName);
        return Write(media, path).Select(written => written ? path : null);
    }

    /// <summary>Writes media into a new file.</summary>
    /// <param name="source">The document's media.</param>
    /// <param name="index">The media's index.</param>
    /// <param name="path">Where to write it.</param>
    /// <returns><see langword="true"/> when written.</returns>
    private static bool WriteFile(IDocumentMediaSource source, int index, string path)
    {
        using var stream = File.Create(path);
        return source.SaveMedia(index, stream);
    }

    /// <summary>Writes media to a path, saying what went wrong when it cannot.</summary>
    /// <param name="media">The media.</param>
    /// <param name="path">Where to write it.</param>
    /// <returns>A cold signal that extracts on the pool, so a large clip does not freeze the window, and emits <see langword="true"/> when written.</returns>
    private IObservable<bool> Write(DocumentMedia media, string path)
    {
        if (DocumentFeature<IDocumentMediaSource>.Get(_owner.TryGetDocument()) is not { } source)
        {
            return Signal.Return(false);
        }

        var index = media.Index;
        return Signal.Start(() => WriteFile(source, index, path), TaskPoolSequencer.Default)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Do(written => Read(media, written))
            .Catch<bool, Exception>(error => Failed(media, error));
    }

    /// <summary>Says the media could not be read from the document when it was not written.</summary>
    /// <param name="media">The media.</param>
    /// <param name="written">Whether it was written.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Read(DocumentMedia media, bool written) =>
        _owner.SetNotice(written ? LocalizedMessage.Literal(_owner.Notice) : LocalizedMessage.Format(UiText, "Could not read {0} from the document.", media.FileName));

    /// <summary>Says the media was saved, when it was.</summary>
    /// <param name="media">The media.</param>
    /// <param name="saved">Whether it was saved.</param>
    private void Saved(DocumentMedia media, bool saved)
    {
        if (saved)
        {
            _owner.SetNotice(LocalizedMessage.Format(UiText, "Saved {0}.", media.FileName));
        }
    }

    /// <summary>Says why the media was not written, or passes on a failure that is not expected.</summary>
    /// <param name="media">The media.</param>
    /// <param name="error">The failure.</param>
    /// <returns>A signal giving <see langword="false"/> for an expected failure, or failing with the same error.</returns>
    private IObservable<bool> Failed(DocumentMedia media, Exception error)
    {
        var expected = error is IOException or UnauthorizedAccessException or InvalidOperationException;
        if (expected)
        {
            _owner.SetNotice(error is InvalidOperationException
                ? LocalizedMessage.Format(UiText, "Could not save {0}: the document was closed.", media.FileName)
                : LocalizedMessage.Format(UiText, "Could not save {0}: {1}", media.FileName, error.Message));
        }

        return expected ? Signal.Return(false) : Signal.Throw<bool>(error);
    }

    /// <summary>Shows the window where the selected media plays, and ends when the window is put away.</summary>
    /// <returns>A signal that ends when the window is put away.</returns>
    [ReactiveCommand(CanExecute = nameof(_canAct))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> ShowPlayer() => ShowPlayerInteraction.Handle(Player).ToSignal();

    /// <summary>Hands the selected media to the system player, after the reader confirms.</summary>
    /// <returns>A signal that ends when the media was handed over or refused.</returns>
    [ReactiveCommand(CanExecute = nameof(_canAct))]
    private IObservable<RxVoid> OpenSelected() =>
        Selected is { } media ? Open(media).Select(static _ => RxVoid.Default) : Signal.Return(RxVoid.Default);

    /// <summary>Asks where to save the selected media, then saves it.</summary>
    /// <returns>A signal that ends when the media is saved or the choice is given up.</returns>
    [ReactiveCommand(CanExecute = nameof(_canAct))]
    private IObservable<RxVoid> Save() =>
        Selected is not { } media
            ? Signal.Return(RxVoid.Default)
            : SaveInteraction.Handle(media.FileName).ToSignal()
                .SwitchMap(path => string.IsNullOrEmpty(path) ? Signal.Return(RxVoid.Default) : SaveTo(media, path).Select(static _ => RxVoid.Default));
}
