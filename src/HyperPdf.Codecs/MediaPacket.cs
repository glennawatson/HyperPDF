// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs;

/// <summary>
/// One coded unit of a track: a frame of sound, a picture, or a caption. A packet is filled by a container and handed to a decoder; the
/// same packet is filled again for the next unit, so reading a file allocates only as many buffers as packets are waiting at once.
/// </summary>
[DebuggerDisplay("MediaPacket: track {Track} at {Time} s, {Length} bytes")]
public sealed class MediaPacket
{
    /// <summary>How many times bigger the buffer becomes when a packet does not fit.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The bytes of the packet, which can be longer than the packet.</summary>
    private byte[] _buffer = [];

    /// <summary>Gets the number of the track the packet belongs to.</summary>
    public int Track { get; private set; }

    /// <summary>Gets when the packet's content starts, in seconds from the start of the file.</summary>
    public double Time { get; private set; }

    /// <summary>Gets how long the packet's content lasts in seconds, or 0 when the container does not say.</summary>
    public double Duration { get; private set; }

    /// <summary>Gets a value indicating whether decoding can start at this packet.</summary>
    public bool IsKey { get; private set; }

    /// <summary>Gets the packet's bytes.</summary>
    public ReadOnlySpan<byte> Data => _buffer.AsSpan(0, Length);

    /// <summary>Gets the number of bytes in the packet.</summary>
    public int Length { get; private set; }

    /// <summary>Gets the place for the packet's bytes, sized for <paramref name="length"/> bytes, and starts a packet with them.</summary>
    /// <param name="track">The track the packet belongs to.</param>
    /// <param name="length">The number of bytes the packet will hold.</param>
    /// <param name="time">When the content starts, in seconds from the start of the file.</param>
    /// <param name="duration">How long the content lasts in seconds, or 0.</param>
    /// <param name="isKey">Whether decoding can start at this packet.</param>
    /// <returns>The bytes to fill.</returns>
    public Span<byte> Begin(int track, int length, double time, double duration, bool isKey)
    {
        if (_buffer.Length < length)
        {
            _buffer = new byte[Math.Max(length, _buffer.Length * GrowthFactor)];
        }

        Track = track;
        Length = length;
        Time = time;
        Duration = duration;
        IsKey = isKey;
        return _buffer.AsSpan(0, length);
    }

    /// <summary>Changes the start time, for a container that finds it after the bytes are read.</summary>
    /// <param name="time">When the content starts, in seconds from the start of the file.</param>
    /// <param name="duration">How long the content lasts in seconds, or 0.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTime(double time, double duration)
    {
        Time = time;
        Duration = duration;
    }

    /// <summary>Empties the packet so it can be filled again.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => Length = 0;
}
