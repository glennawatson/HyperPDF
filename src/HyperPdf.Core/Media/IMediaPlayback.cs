// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Advanced;

namespace HyperPdf.Core.Media;

/// <summary>Plays one sound or video file. Nothing plays until <see cref="Play"/> is called.</summary>
public interface IMediaPlayback : IDisposable
{
    /// <summary>Gets the changes to the snapshot, raised on a thread pool thread.</summary>
    AsObservableSignal<MediaSnapshot> Changes { get; }

    /// <summary>Gets what went wrong, in plain words, or <see langword="null"/>.</summary>
    string? Problem { get; }

    /// <summary>Gets what the open file holds.</summary>
    MediaInfo Info { get; }

    /// <summary>Gets the size of the pictures, or an empty size for a file without a picture.</summary>
    FrameSize FrameSize { get; }

    /// <summary>Gets the current snapshot.</summary>
    MediaSnapshot Snapshot { get; }

    /// <summary>Opens a file and reads its length, size and captions. Nothing plays.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="cancellationToken">Stops opening.</param>
    /// <returns><see langword="true"/> when the file has sound or a picture that can be played.</returns>
    Task<bool> OpenAsync(string path, CancellationToken cancellationToken);

    /// <summary>Copies the latest picture.</summary>
    /// <param name="destination">Receives blue, green, red and alpha bytes; it must hold at least <c>FrameSize.ByteCount</c> bytes.</param>
    /// <returns><see langword="true"/> when a picture was copied.</returns>
    bool TryCopyFrame(Span<byte> destination);

    /// <summary>Starts playing, or carries on after a pause. Starts again from the beginning after the end.</summary>
    void Play();

    /// <summary>Pauses at the current position.</summary>
    void Pause();

    /// <summary>Stops and goes back to the start.</summary>
    void Stop();

    /// <summary>Moves to a position. A file that is playing carries on from there.</summary>
    /// <param name="position">The position; it is kept within the file.</param>
    void Seek(TimeSpan position);
}
