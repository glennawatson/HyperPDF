// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>
/// Chooses how media plays on this computer: with the FFmpeg libraries that ship with the app, where the sound clock keeps the
/// picture in step and any position can be reached at once; otherwise with the FFmpeg programs if they are installed; otherwise not at all.
/// </summary>
[DebuggerDisplay("MediaPlaybackFactory: {IsAvailable}")]
public sealed class MediaPlaybackFactory
{
    /// <summary>Makes a sound output for a run.</summary>
    private readonly Func<IStreamingAudioOutput> _soundFactory;

    /// <summary>Tells whether media can be played, without loading anything.</summary>
    private readonly Func<bool> _available;

    /// <summary>Makes a player, or <see langword="null"/> when media cannot be played.</summary>
    private readonly Func<bool, IMediaPlayback?> _create;

    /// <summary>Initializes a new instance of the <see cref="MediaPlaybackFactory"/> class that chooses between the libraries and the programs.</summary>
    /// <param name="soundFactory">Makes a sound output for a run; the desktop provides it.</param>
    public MediaPlaybackFactory(Func<IStreamingAudioOutput> soundFactory)
    {
        ArgumentNullException.ThrowIfNull(soundFactory);
        _soundFactory = soundFactory;
        _available = static () => FfmpegLibraries.IsPresent || MediaToolLocator.Find() is not null;
        _create = CreateDefault;
    }

    /// <summary>Initializes a new instance of the <see cref="MediaPlaybackFactory"/> class with its own way of making players, for tests.</summary>
    /// <param name="available">Tells whether media can be played.</param>
    /// <param name="create">Makes a player; its argument says whether to show a picture only once a second.</param>
    public MediaPlaybackFactory(Func<bool> available, Func<bool, IMediaPlayback?> create)
    {
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(create);
        _soundFactory = static () => NullStreamingAudioOutput.Instance;
        _available = available;
        _create = create;
    }

    /// <summary>Gets a value indicating whether media can be played on this computer. It checks for files only and loads nothing.</summary>
    public bool IsAvailable => _available();

    /// <summary>Makes a player.</summary>
    /// <param name="reduceMotion">Whether to show a picture only once a second, for readers who asked for less motion.</param>
    /// <returns>The player, or <see langword="null"/> when media cannot be played.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMediaPlayback? Create(bool reduceMotion) => _create(reduceMotion);

    /// <summary>Makes a player from the libraries or, failing that, the programs.</summary>
    /// <param name="reduceMotion">Whether to show a picture only once a second.</param>
    /// <returns>The player, or <see langword="null"/> when neither is available.</returns>
    private IMediaPlayback? CreateDefault(bool reduceMotion)
    {
        if (FfmpegLibraries.IsAvailable)
        {
            return new LibavMediaPlayer(_soundFactory, reduceMotion);
        }

        return MediaToolLocator.Find() is { } tools ? new FfmpegMediaPlayer(tools, reduceMotion) : null;
    }
}
