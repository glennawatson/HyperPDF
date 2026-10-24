// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Containers;

namespace HyperPdf.Codecs.Mp3;

/// <summary>
/// A native MP3 file: an optional ID3 tag, then frames that each carry their own header. The first frame may be a Xing or Info tag with
/// the frame count and the encoder's delay, which the container reads and does not hand on as sound. Sync is trusted only when two
/// more frames follow it in the same stream, so junk and tags in the middle of the file are stepped over.
/// </summary>
[DebuggerDisplay("Mp3Container: {_first.SampleRate} Hz, {Duration}")]
internal sealed class Mp3Container : MediaContainer
{
    /// <summary>The bytes of an ID3v2 header.</summary>
    private const int Id3HeaderLength = 10;

    /// <summary>The bytes of the "ID3" marker.</summary>
    private const int Id3MarkerLength = 3;

    /// <summary>The index of the flags byte in an ID3v2 header.</summary>
    private const int Id3FlagsIndex = 5;

    /// <summary>The flag in an ID3v2 header that says a footer follows the tag.</summary>
    private const int Id3FooterFlag = 0x10;

    /// <summary>The bits that carry a number in each byte of an ID3 size.</summary>
    private const int SynchsafeBits = 7;

    /// <summary>The mask of the bits that carry a number in a byte of an ID3 size.</summary>
    private const int SynchsafeMask = 0x7F;

    /// <summary>The most bytes the search for the first frame goes through.</summary>
    private const int SearchLimit = 1 << 20;

    /// <summary>The frames that must follow a header, in the same stream, for it to count as sync.</summary>
    private const int ConfirmingFrames = 2;

    /// <summary>The frames to start decoding before a place the container seeks to, so the bit reservoir has what it needs.</summary>
    private const int PreRoll = 4;

    /// <summary>The first sync byte.</summary>
    private const byte SyncFirst = 0xFF;

    /// <summary>The file.</summary>
    private readonly FileWindow _window;

    /// <summary>The header of the first sound frame, which names the stream.</summary>
    private readonly Mp3FrameHeader _first;

    /// <summary>Where the first sound frame starts.</summary>
    private readonly long _audioStart;

    /// <summary>The offset of every frame, built when a seek or the duration needs it.</summary>
    private List<long>? _index;

    /// <summary>The file offset of the next frame.</summary>
    private long _position;

    /// <summary>The number of frames read since the start of the sound.</summary>
    private int _frameNumber;

    /// <summary>Initializes a new instance of the <see cref="Mp3Container"/> class.</summary>
    /// <param name="window">The file, which the container owns.</param>
    /// <param name="first">The header of the first sound frame.</param>
    /// <param name="audioStart">Where the first sound frame starts.</param>
    /// <param name="tag">The tag in front of the sound.</param>
    private Mp3Container(FileWindow window, in Mp3FrameHeader first, long audioStart, in Mp3StreamTag tag)
    {
        _window = window;
        _first = first;
        _audioStart = audioStart;
        _position = audioStart;
        Tracks = [new(0, MediaTrackKind.Audio, MediaCodec.Mp3, first.SampleRate, first.Channels, 0, 0, 0, tag.ToConfiguration())];
        var frames = tag.Frames > 0 ? tag.Frames : EnsureIndex().Count;
        var samples = Math.Max(0L, ((long)frames * first.SamplesPerFrame) - tag.EncoderDelay - tag.EncoderPadding);
        Duration = TimeSpan.FromSeconds((double)samples / first.SampleRate);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks { get; }

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var offset = _position;
        if (!TryHeaderAt(_window, offset, out var header) || !header.IsSameStream(_first))
        {
            offset = FindFrame(_window, offset + 1, SearchLimit);
            if (offset < 0 || !TryHeaderAt(_window, offset, out header))
            {
                return false;
            }
        }

        var frame = _window.Get(offset, header.FrameLength);
        if (frame.Length < header.FrameLength)
        {
            return false;
        }

        var seconds = (double)_frameNumber * _first.SamplesPerFrame / _first.SampleRate;
        frame[..header.FrameLength].CopyTo(packet.Begin(0, header.FrameLength, seconds, (double)header.SamplesPerFrame / header.SampleRate, true));
        _position = offset + header.FrameLength;
        _frameNumber++;
        return true;
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        var index = EnsureIndex();
        var target = (int)Math.Clamp(position.TotalSeconds * _first.SampleRate / _first.SamplesPerFrame, 0, Math.Max(0, index.Count - 1));
        _frameNumber = Math.Max(0, target - PreRoll);
        _position = index.Count == 0 ? _audioStart : index[_frameNumber];
        return true;
    }

    /// <summary>Opens a stream as an MP3 file.</summary>
    /// <param name="stream">The file; it is left where it was when it is not an MP3 file.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/> when the stream is not an MP3 file or holds layer 1 or 2.</returns>
    internal static Mp3Container? TryOpen(Stream stream)
    {
        var start = stream.Position;
        var window = new FileWindow(stream);
        var searchFrom = SkipTags(window, start);
        var offset = FindFrame(window, searchFrom, SearchLimit);
        if (offset < 0 || !TryHeaderAt(window, offset, out var header) || header.Layer != Mp3FrameHeader.Layer3)
        {
            stream.Position = start;
            return null;
        }

        var tag = default(Mp3StreamTag);
        var audioStart = offset;
        var frame = window.Get(offset, header.FrameLength);
        if (frame.Length >= header.FrameLength && Mp3StreamTag.TryParse(frame[..header.FrameLength], header, out tag))
        {
            audioStart = offset + header.FrameLength;
            if (!TryHeaderAt(window, audioStart, out var after) || !after.IsSameStream(header))
            {
                stream.Position = start;
                return null;
            }

            header = after;
        }

        return new(window, header, audioStart, tag);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // The window does not own the stream; the container does, through it.
    }

    /// <summary>Moves past the ID3v2 tags at the start of the file.</summary>
    /// <param name="window">The file.</param>
    /// <param name="start">Where to look.</param>
    /// <returns>The offset after the tags.</returns>
    private static long SkipTags(FileWindow window, long start)
    {
        var offset = start;
        while (true)
        {
            var header = window.Get(offset, Id3HeaderLength);
            if (header.Length < Id3HeaderLength || !header[..Id3MarkerLength].SequenceEqual("ID3"u8))
            {
                return offset;
            }

            var size = 0;
            for (var index = Id3HeaderLength - Mp3FrameHeader.Length; index < Id3HeaderLength; index++)
            {
                size = (size << SynchsafeBits) | (header[index] & SynchsafeMask);
            }

            var footer = (header[Id3FlagsIndex] & Id3FooterFlag) != 0 ? Id3HeaderLength : 0;
            offset += Id3HeaderLength + size + footer;
        }
    }

    /// <summary>Reads the header at an offset.</summary>
    /// <param name="window">The file.</param>
    /// <param name="offset">The file offset.</param>
    /// <param name="header">Receives the header.</param>
    /// <returns>Whether a header is there.</returns>
    private static bool TryHeaderAt(FileWindow window, long offset, out Mp3FrameHeader header)
    {
        var bytes = window.Get(offset, Mp3FrameHeader.Length);
        header = default;
        return bytes.Length >= Mp3FrameHeader.Length && Mp3FrameHeader.TryParse(bytes, out header);
    }

    /// <summary>Finds the next frame that is followed by two more in the same stream.</summary>
    /// <param name="window">The file.</param>
    /// <param name="from">Where to start looking.</param>
    /// <param name="limit">How many bytes to look through.</param>
    /// <returns>The offset of the frame, or -1.</returns>
    private static long FindFrame(FileWindow window, long from, int limit)
    {
        var end = Math.Min(window.Length, from + limit);
        var offset = from;
        while (offset < end)
        {
            var bytes = window.Get(offset, Mp3FrameHeader.Length);
            if (bytes.Length < Mp3FrameHeader.Length)
            {
                return -1;
            }

            var at = bytes.IndexOf(SyncFirst);
            if (at < 0)
            {
                offset += bytes.Length;
                continue;
            }

            offset += at;
            if (IsConfirmed(window, offset))
            {
                return offset;
            }

            offset++;
        }

        return -1;
    }

    /// <summary>Tells whether a header is followed by more frames of the same stream, or by the end of the file.</summary>
    /// <param name="window">The file.</param>
    /// <param name="offset">The offset of the header.</param>
    /// <returns>Whether the frame is real.</returns>
    private static bool IsConfirmed(FileWindow window, long offset)
    {
        if (!TryHeaderAt(window, offset, out var header))
        {
            return false;
        }

        var next = offset;
        for (var step = 0; step < ConfirmingFrames; step++)
        {
            next += header.FrameLength;
            if (next >= window.Length)
            {
                return true;
            }

            if (!TryHeaderAt(window, next, out var following) || !following.IsSameStream(header))
            {
                return false;
            }

            header = following;
        }

        return true;
    }

    /// <summary>Gets the offset of every frame, scanning the file once.</summary>
    /// <returns>The offsets, in order.</returns>
    private List<long> EnsureIndex()
    {
        if (_index is not null)
        {
            return _index;
        }

        var index = new List<long>();
        var offset = _audioStart;
        while (offset >= 0 && TryHeaderAt(_window, offset, out var header) && header.IsSameStream(_first) && offset + header.FrameLength <= _window.Length)
        {
            index.Add(offset);
            var next = offset + header.FrameLength;
            offset = TryHeaderAt(_window, next, out var following) && following.IsSameStream(_first) ? next : FindFrame(_window, next, SearchLimit);
        }

        _index = index;
        return index;
    }
}
