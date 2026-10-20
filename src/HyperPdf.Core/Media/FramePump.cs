// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Media;

/// <summary>
/// Holds the latest decoded picture of a video. A picture is written into a back buffer and swapped to the front, so a view that
/// copies the front picture never sees one half written, and no picture allocates.
/// </summary>
/// <param name="size">The size of each picture.</param>
[DebuggerDisplay("FramePump: {Version} pictures")]
public sealed class FramePump(FrameSize size)
{
    /// <summary>Guards the swap of the front and back buffers.</summary>
    private readonly Lock _gate = new();

    /// <summary>The picture the view copies.</summary>
    private byte[] _front = new byte[size.ByteCount];

    /// <summary>The picture being written.</summary>
    private byte[] _back = new byte[size.ByteCount];

    /// <summary>The number of pictures shown so far.</summary>
    private int _version;

    /// <summary>Gets the number of pictures shown so far.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Copies the latest picture.</summary>
    /// <param name="destination">Receives the picture; it must be at least one picture long.</param>
    /// <returns><see langword="true"/> when a picture was copied.</returns>
    public bool TryCopy(Span<byte> destination)
    {
        lock (_gate)
        {
            if (_version == 0 || destination.Length < _front.Length)
            {
                return false;
            }

            _front.CopyTo(destination);
            return true;
        }
    }

    /// <summary>Shows a picture that a decoder made.</summary>
    /// <param name="picture">Blue, green, red and alpha bytes; it must be exactly one picture long.</param>
    /// <returns><see langword="true"/> when the picture was shown.</returns>
    public bool Show(ReadOnlySpan<byte> picture)
    {
        if (picture.Length != _back.Length)
        {
            return false;
        }

        picture.CopyTo(_back);
        Swap();
        return true;
    }

    /// <summary>Reads pictures from a stream until it ends or reading is cancelled.</summary>
    /// <param name="source">The stream of raw pictures.</param>
    /// <param name="shown">Called after each picture is shown.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    /// <returns>A task that completes when the stream ends.</returns>
    public async Task RunAsync(Stream source, Action shown, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(shown);
        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await source.ReadAtLeastAsync(_back.AsMemory(), _back.Length, false, cancellationToken).ConfigureAwait(false);
            if (read < _back.Length)
            {
                // The video ended in the middle of a picture or after the last one.
                return;
            }

            Swap();
            shown();
        }
    }

    /// <summary>Makes the picture that was written the one views copy.</summary>
    private void Swap()
    {
        lock (_gate)
        {
            var shownPicture = _back;
            _back = _front;
            _front = shownPicture;
            _version++;
        }
    }
}
