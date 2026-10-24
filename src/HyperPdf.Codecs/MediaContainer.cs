// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs;

/// <summary>A media file opened for reading: the tracks it holds, and its packets one at a time in the order the file stores them.</summary>
[DebuggerDisplay("MediaContainer: {Tracks.Count} tracks, {Duration}")]
public abstract class MediaContainer : IDisposable
{
    /// <summary>Gets the tracks, in the order of their numbers.</summary>
    public abstract IReadOnlyList<MediaTrack> Tracks { get; }

    /// <summary>Gets how long the file plays, or <see cref="TimeSpan.Zero"/> when it does not say.</summary>
    public abstract TimeSpan Duration { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Reads the next packet of any track.</summary>
    /// <param name="packet">Receives the packet.</param>
    /// <returns><see langword="false"/> at the end of the file or when the file cannot be read further.</returns>
    public abstract bool ReadPacket(MediaPacket packet);

    /// <summary>Moves to the packet at or just before a position, from which decoding can start.</summary>
    /// <param name="position">The position from the start of the file.</param>
    /// <returns><see langword="true"/> when the file moved; <see langword="false"/> when it cannot seek, in which case reading carries on where it was.</returns>
    public abstract bool Seek(TimeSpan position);

    /// <summary>Finds the first track of a kind that this library reads.</summary>
    /// <param name="kind">What the track holds.</param>
    /// <returns>The track's number, or -1 when the file has none.</returns>
    public int FindTrack(MediaTrackKind kind)
    {
        for (var index = 0; index < Tracks.Count; index++)
        {
            if (Tracks[index].Kind == kind && Tracks[index].Codec != MediaCodec.Unknown)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Lets go of the file.</summary>
    /// <param name="disposing"><see langword="true"/> when called from <see cref="Dispose()"/>.</param>
    protected abstract void Dispose(bool disposing);
}
