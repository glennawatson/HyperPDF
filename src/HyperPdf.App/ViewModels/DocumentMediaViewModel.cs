// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Media;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// A tab's sound and video. The media panel only appears for documents that have some. Media never plays by itself: the page
/// shows the annotation's poster picture, and the reader can press Play to play it in the panel (when FFmpeg is installed),
/// save it, or hand it to the system player after confirming.
/// </summary>
[DebuggerDisplay("DocumentMediaViewModel: {Items.Count} items")]
public sealed partial class DocumentMediaViewModel : ReactiveObject, IDisposable
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
    public MediaPlayerViewModel Player => _player ??= new(this, _playback);

    /// <summary>Gets a value indicating whether the document has sound or video.</summary>
    [Reactive]
    public partial bool HasMedia { get; private set; }

    /// <summary>Gets or sets the selected item.</summary>
    [Reactive]
    public partial DocumentMedia? Selected { get; set; }

    /// <summary>Gets the interaction asking where to save a file; the input is the suggested name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

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
    public async Task<bool> SaveToAsync(DocumentMedia media, string path)
    {
        ArgumentNullException.ThrowIfNull(media);
        var saved = await WriteAsync(media, path).ConfigureAwait(true);
        if (saved)
        {
            _owner.Notice = $"Saved {media.FileName}.";
        }

        return saved;
    }

    /// <summary>
    /// Writes a piece of media to a temporary file and hands it to the system player. The file opens only after the reader
    /// confirms, and a program or script is never started.
    /// </summary>
    /// <param name="media">The media.</param>
    /// <returns><see langword="true"/> when the file was handed to the system.</returns>
    public async Task<bool> OpenAsync(DocumentMedia media)
    {
        ArgumentNullException.ThrowIfNull(media);
        return await ExtractAsync(media).ConfigureAwait(true) is { } path && await _owner.Links.OpenFileAsync(path, 0).ConfigureAwait(true);
    }

    /// <summary>Releases the player and its temporary file.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _player?.Dispose();

    /// <summary>Writes a piece of media to a new temporary file.</summary>
    /// <param name="media">The media.</param>
    /// <returns>The full path of the file, or <see langword="null"/> when it could not be written; the notice says why.</returns>
    internal async Task<string?> ExtractAsync(DocumentMedia media)
    {
        // Each extraction gets its own folder, so files with the same name never replace one another.
        var folder = Path.Combine(Path.GetTempPath(), TempFolderName, Guid.NewGuid().ToString("N"));
        try
        {
            _ = Directory.CreateDirectory(folder);
        }
        catch (IOException ex)
        {
            _owner.Notice = $"Could not open {media.FileName}: {ex.Message}";
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.Notice = $"Could not open {media.FileName}: {ex.Message}";
            return null;
        }

        var path = Path.Combine(folder, media.FileName);
        return await WriteAsync(media, path).ConfigureAwait(true) ? path : null;
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
    /// <returns><see langword="true"/> when written.</returns>
    private async Task<bool> WriteAsync(DocumentMedia media, string path)
    {
        if (DocumentFeature<IDocumentMediaSource>.Get(_owner.TryGetDocument()) is not { } source)
        {
            return false;
        }

        try
        {
            // Extracting and writing the file is synchronous, so it runs on the pool and a large clip does not freeze the window.
            var index = media.Index;
            var written = await Task.Run(() => WriteFile(source, index, path), _owner.LifetimeToken).ConfigureAwait(true);
            _owner.Notice = written ? _owner.Notice : $"Could not read {media.FileName} from the document.";
            return written;
        }
        catch (IOException ex)
        {
            _owner.Notice = $"Could not save {media.FileName}: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _owner.Notice = $"Could not save {media.FileName}: {ex.Message}";
            return false;
        }
        catch (InvalidOperationException)
        {
            _owner.Notice = $"Could not save {media.FileName}: the document was closed.";
            return false;
        }
    }

    /// <summary>Hands the selected media to the system player, after the reader confirms.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canAct))]
    private async Task OpenSelectedAsync()
    {
        if (Selected is { } media)
        {
            _ = await OpenAsync(media).ConfigureAwait(true);
        }
    }

    /// <summary>Asks where to save the selected media, then saves it.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canAct))]
    private async Task SaveAsync()
    {
        if (Selected is not { } media)
        {
            return;
        }

        var path = await SaveInteraction.Handle(media.FileName).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            _ = await SaveToAsync(media, path).ConfigureAwait(true);
        }
    }
}
