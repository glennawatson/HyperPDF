// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Media;

/// <summary>Chooses how media plays on this computer: with the readers and decoders written in C# for this app, where the sound clock keeps the picture in step.</summary>
[DebuggerDisplay("MediaPlaybackFactory: {IsAvailable}")]
public sealed class MediaPlaybackFactory
{
    /// <summary>Tells whether media can be played.</summary>
    private readonly Func<bool> _available;

    /// <summary>Makes a player, or <see langword="null"/> when media cannot be played.</summary>
    private readonly Func<bool, IMediaPlayback?> _create;

    /// <summary>Initializes a new instance of the <see cref="MediaPlaybackFactory"/> class that makes the built-in player.</summary>
    /// <param name="soundFactory">Makes a sound output for a run; the desktop provides it.</param>
    public MediaPlaybackFactory(Func<IStreamingAudioOutput> soundFactory)
    {
        ArgumentNullException.ThrowIfNull(soundFactory);
        _available = static () => true;
        _create = reduceMotion => new ManagedMediaPlayer(soundFactory, reduceMotion);
    }

    /// <summary>Initializes a new instance of the <see cref="MediaPlaybackFactory"/> class with its own way of making players, for tests.</summary>
    /// <param name="available">Tells whether media can be played.</param>
    /// <param name="create">Makes a player; its argument says whether to show a picture only once a second.</param>
    public MediaPlaybackFactory(Func<bool> available, Func<bool, IMediaPlayback?> create)
    {
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(create);
        _available = available;
        _create = create;
    }

    /// <summary>Gets a value indicating whether media can be played on this computer.</summary>
    public bool IsAvailable => _available();

    /// <summary>Makes a player.</summary>
    /// <param name="reduceMotion">Whether to show a picture only once a second, for readers who asked for less motion.</param>
    /// <returns>The player, or <see langword="null"/> when media cannot be played.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IMediaPlayback? Create(bool reduceMotion) => _create(reduceMotion);
}
