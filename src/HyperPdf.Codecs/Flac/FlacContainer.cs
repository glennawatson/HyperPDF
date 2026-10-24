// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Codecs.Checksums;

namespace HyperPdf.Codecs.Flac;

/// <summary>
/// A native FLAC file: the metadata blocks, then frames. A FLAC file has no index of its frames, so the container finds the end of a
/// frame by looking for the next valid frame header and proving the bytes between with the frame's own check, which also means a
/// damaged frame is never handed to the decoder.
/// </summary>
[DebuggerDisplay("FlacContainer: {_info.SampleRate} Hz, {_info.TotalSamples} samples")]
internal sealed class FlacContainer : MediaContainer
{
    /// <summary>The bytes read from the file at a time.</summary>
    private const int ReadSize = 65_536;

    /// <summary>The size of the buffer to start with: room for two reads.</summary>
    private const int InitialBufferSize = ReadSize << 1;

    /// <summary>The bits in the first two bytes of a metadata block length.</summary>
    private const int LengthHighShift = 16;

    /// <summary>The bytes of the "ID3" marker.</summary>
    private const int Id3MarkerLength = 3;

    /// <summary>The bytes of the marker at the start of a stream.</summary>
    private const int MarkerLength = 4;

    /// <summary>The bytes of a metadata block header.</summary>
    private const int BlockHeaderLength = 4;

    /// <summary>The bytes of the check at the end of a frame.</summary>
    private const int FooterLength = 2;

    /// <summary>The number of bytes of an ID3v2 header.</summary>
    private const int Id3HeaderLength = 10;

    /// <summary>The metadata block type of the stream information.</summary>
    private const int StreamInfoType = 0;

    /// <summary>The metadata block type of the seek table.</summary>
    private const int SeekTableType = 3;

    /// <summary>The bytes of a seek point: the first sample, the offset and the samples in the frame.</summary>
    private const int SeekPointLength = 18;

    /// <summary>The bytes of the first sample and the offset in a seek point.</summary>
    private const int SeekPointFields = 8;

    /// <summary>The seek point sample number that marks a placeholder.</summary>
    private const ulong PlaceholderSample = ulong.MaxValue;

    /// <summary>The mask of the block type in a block header byte.</summary>
    private const int BlockTypeMask = 0x7F;

    /// <summary>The flag in a block header byte that says the block is the last.</summary>
    private const int LastBlockFlag = 0x80;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits that carry a number in a byte of an ID3 size.</summary>
    private const int SynchsafeBits = 7;

    /// <summary>The first sync byte of a frame.</summary>
    private const byte SyncFirst = 0xFF;

    /// <summary>The frame size above which a header found is taken to be a false one, when the stream does not say its largest frame.</summary>
    private const int LargestFrame = 1 << 24;

    /// <summary>The file.</summary>
    private readonly Stream _stream;

    /// <summary>The stream information.</summary>
    private readonly FlacStreamInfo _info;

    /// <summary>The seek points, in order of their samples.</summary>
    private readonly List<SeekPoint> _seekPoints;

    /// <summary>Where the first frame starts in the file.</summary>
    private readonly long _audioStart;

    /// <summary>Bytes read from the file, starting at <see cref="_bufferStart"/>.</summary>
    private byte[] _buffer = new byte[InitialBufferSize];

    /// <summary>The file offset of the first byte in the buffer.</summary>
    private long _bufferStart;

    /// <summary>The number of bytes in the buffer.</summary>
    private int _bufferLength;

    /// <summary>The file offset of the next frame to read.</summary>
    private long _position;

    /// <summary>Whether the file has been read to its end.</summary>
    private bool _atEnd;

    /// <summary>Initializes a new instance of the <see cref="FlacContainer"/> class.</summary>
    /// <param name="stream">The file, which the container owns.</param>
    /// <param name="info">The stream information.</param>
    /// <param name="configuration">The bytes of the stream information block.</param>
    /// <param name="seekPoints">The seek points.</param>
    /// <param name="audioStart">Where the first frame starts.</param>
    private FlacContainer(Stream stream, FlacStreamInfo info, byte[] configuration, List<SeekPoint> seekPoints, long audioStart)
    {
        _stream = stream;
        _info = info;
        _seekPoints = seekPoints;
        _audioStart = audioStart;
        _position = audioStart;
        Tracks = [new(0, MediaTrackKind.Audio, MediaCodec.Flac, info.SampleRate, info.Channels, info.BitsPerSample, 0, 0, configuration)];
        Duration = info.TotalSamples > 0 ? TimeSpan.FromSeconds((double)info.TotalSamples / info.SampleRate) : TimeSpan.Zero;
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks { get; }

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        while (true)
        {
            var length = FrameLengthAt(_position, out var header);
            if (length < 0)
            {
                return false;
            }

            if (length == 0)
            {
                // What is here is not a frame; carry on from the next possible start.
                _position = NextSync(_position + 1);
                if (_position < 0)
                {
                    return false;
                }

                continue;
            }

            var seconds = (double)header.FirstSample(_info.MinBlockSize) / _info.SampleRate;
            var window = Window(_position);
            window[..length].CopyTo(packet.Begin(0, length, seconds, (double)header.BlockSize / _info.SampleRate, true));
            _position += length;
            return true;
        }
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        var target = (long)(position.TotalSeconds * _info.SampleRate);
        var found = _seekPoints.Count > 0 ? FromSeekTable(target) : Bisect(target);
        _position = found;
        return true;
    }

    /// <summary>Opens a stream as a FLAC file.</summary>
    /// <param name="stream">The file; it is left where it was when it is not a FLAC file.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/> when the stream is not a FLAC file.</returns>
    internal static FlacContainer? TryOpen(Stream stream)
    {
        var start = stream.Position;
        var opened = ReadMetadata(stream);
        if (opened is null)
        {
            stream.Position = start;
        }

        return opened;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
    }

    /// <summary>Reads the marker and the metadata blocks.</summary>
    /// <param name="stream">The file.</param>
    /// <returns>The container, or <see langword="null"/> when the file is not FLAC.</returns>
    private static FlacContainer? ReadMetadata(Stream stream)
    {
        Span<byte> marker = stackalloc byte[MarkerLength];
        SkipId3(stream);
        if (stream.Read(marker) != MarkerLength || !marker.SequenceEqual("fLaC"u8))
        {
            return null;
        }

        FlacStreamInfo? info = null;
        byte[]? configuration = null;
        List<SeekPoint> points = [];
        bool last;
        do
        {
            var content = ReadBlock(stream, out var type, out last);
            if (content is null)
            {
                return null;
            }

            if (type == StreamInfoType && info is null)
            {
                info = FlacStreamInfo.Parse(content);
                configuration = content;
            }
            else if (type == SeekTableType)
            {
                ReadSeekTable(content, points);
            }
        }
        while (!last);

        return info is null || configuration is null ? null : new(stream, info, configuration, points, stream.Position);
    }

    /// <summary>Reads one metadata block.</summary>
    /// <param name="stream">The file.</param>
    /// <param name="type">Receives the block type.</param>
    /// <param name="last">Receives whether this is the last block.</param>
    /// <returns>The block's content, or <see langword="null"/> when the file ends inside the block.</returns>
    private static byte[]? ReadBlock(Stream stream, out int type, out bool last)
    {
        Span<byte> header = stackalloc byte[BlockHeaderLength];
        type = 0;
        last = true;
        if (stream.Read(header) != BlockHeaderLength)
        {
            return null;
        }

        last = (header[0] & LastBlockFlag) != 0;
        type = header[0] & BlockTypeMask;
        var length = (header[1] << LengthHighShift) | (header[2] << ByteBits) | header[3];
        var content = new byte[length];
        return stream.Read(content) == length ? content : null;
    }

    /// <summary>Moves past an ID3v2 tag at the start of the file, which some taggers put in front of FLAC.</summary>
    /// <param name="stream">The file.</param>
    private static void SkipId3(Stream stream)
    {
        var start = stream.Position;
        Span<byte> header = stackalloc byte[Id3HeaderLength];
        if (stream.Read(header) == Id3HeaderLength && header[..Id3MarkerLength].SequenceEqual("ID3"u8))
        {
            var size = 0;
            for (var index = Id3HeaderLength - BlockHeaderLength; index < Id3HeaderLength; index++)
            {
                size = (size << SynchsafeBits) | (header[index] & BlockTypeMask);
            }

            stream.Position = start + Id3HeaderLength + size;
            return;
        }

        stream.Position = start;
    }

    /// <summary>Reads the seek points of a seek table block.</summary>
    /// <param name="content">The block's content.</param>
    /// <param name="points">Receives the points.</param>
    private static void ReadSeekTable(ReadOnlySpan<byte> content, List<SeekPoint> points)
    {
        for (var at = 0; at + SeekPointLength <= content.Length; at += SeekPointLength)
        {
            var sample = BinaryPrimitives.ReadUInt64BigEndian(content[at..]);
            if (sample != PlaceholderSample)
            {
                points.Add(new((long)sample, (long)BinaryPrimitives.ReadUInt64BigEndian(content[(at + SeekPointFields)..])));
            }
        }
    }

    /// <summary>Tells whether the bytes up to a length are a frame whose check at the end is right.</summary>
    /// <param name="window">The bytes from the start of the frame.</param>
    /// <param name="length">The length of the frame including its check.</param>
    /// <returns>Whether the check matches.</returns>
    private static bool FrameChecks(ReadOnlySpan<byte> window, int length) =>
        length > FooterLength && Crc16.Compute(window[..(length - FooterLength)]) == BinaryPrimitives.ReadUInt16BigEndian(window[(length - FooterLength)..]);

    /// <summary>Gets the bytes of the buffer from a file offset.</summary>
    /// <param name="offset">The file offset, inside the buffer.</param>
    /// <returns>The bytes from that offset to the end of the buffer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ReadOnlySpan<byte> Window(long offset) => _buffer.AsSpan((int)(offset - _bufferStart), _bufferLength - (int)(offset - _bufferStart));

    /// <summary>Makes sure the buffer starts at or before an offset, reading from the file when it does not hold it.</summary>
    /// <param name="offset">The file offset that will be read.</param>
    private void Position(long offset)
    {
        if (offset >= _bufferStart && offset <= _bufferStart + _bufferLength)
        {
            return;
        }

        _bufferStart = offset;
        _bufferLength = 0;
        _atEnd = false;
    }

    /// <summary>Reads more of the file onto the end of the buffer.</summary>
    /// <returns><see langword="false"/> at the end of the file.</returns>
    private bool Extend()
    {
        if (_atEnd)
        {
            return false;
        }

        if (_buffer.Length - _bufferLength < ReadSize)
        {
            Array.Resize(ref _buffer, _buffer.Length << 1);
        }

        _stream.Position = _bufferStart + _bufferLength;
        var read = _stream.Read(_buffer, _bufferLength, ReadSize);
        _bufferLength += read;
        _atEnd = read == 0;
        return read > 0;
    }

    /// <summary>Finds the length of the frame that starts at an offset.</summary>
    /// <param name="offset">The file offset.</param>
    /// <param name="header">Receives the frame's header.</param>
    /// <returns>The length in bytes, 0 when there is no valid frame here, or -1 at the end of the file.</returns>
    private int FrameLengthAt(long offset, out FlacFrameHeader header)
    {
        header = default;
        Position(offset);
        while (_bufferLength - (int)(offset - _bufferStart) < FlacFrameHeader.MaximumLength && Extend())
        {
            // Read until the whole header is held.
        }

        var start = Window(offset);
        if (start.Length < FlacFrameHeader.MinimumLength)
        {
            return -1;
        }

        return FlacFrameHeader.TryParse(start, _info, out header) ? FindEnd(offset, header) : 0;
    }

    /// <summary>Finds where a frame ends: the next valid frame header whose position makes the frame's own check come out right.</summary>
    /// <param name="offset">The file offset of the frame.</param>
    /// <param name="header">The frame's header.</param>
    /// <returns>The length of the frame, or 0 when the check never matches.</returns>
    private int FindEnd(long offset, in FlacFrameHeader header)
    {
        var from = header.Length + FooterLength;
        var limit = _info.MaxFrameSize > 0 ? _info.MaxFrameSize : LargestFrame;
        while (true)
        {
            var window = Window(offset);
            var candidate = FindHeader(window, from);
            if (candidate < 0)
            {
                if (Extend())
                {
                    continue;
                }

                // The last frame ends with the file.
                return window.Length > header.Length + FooterLength && FrameChecks(window, window.Length) ? window.Length : 0;
            }

            if (candidate > limit)
            {
                return 0;
            }

            if (FrameChecks(window, candidate))
            {
                return candidate;
            }

            from = candidate + 1;
        }
    }

    /// <summary>Finds the next valid frame header in some bytes.</summary>
    /// <param name="window">The bytes.</param>
    /// <param name="from">The index to start looking at.</param>
    /// <returns>The index of the header, or -1 when none is found in the bytes held.</returns>
    private int FindHeader(ReadOnlySpan<byte> window, int from)
    {
        while (from < window.Length)
        {
            var at = window[from..].IndexOf(SyncFirst);
            if (at < 0)
            {
                return -1;
            }

            from += at;
            if (from + FlacFrameHeader.MaximumLength > window.Length && !_atEnd)
            {
                // The header may be cut off by the end of what is held; ask for more before judging it.
                return -1;
            }

            if (FlacFrameHeader.TryParse(window[from..], _info, out _))
            {
                return from;
            }

            from++;
        }

        return -1;
    }

    /// <summary>Finds the offset of the next sync code at or after an offset.</summary>
    /// <param name="offset">The file offset.</param>
    /// <returns>The offset of a valid frame header, or -1 at the end of the file.</returns>
    private long NextSync(long offset)
    {
        Position(offset);
        while (true)
        {
            var found = FindHeader(Window(offset), 0);
            if (found >= 0)
            {
                return offset + found;
            }

            var held = Window(offset).Length;
            if (!Extend())
            {
                return -1;
            }

            // Keep the last bytes held so a header cut off at the end of the buffer is found with the next read.
            offset += Math.Max(0, held - FlacFrameHeader.MaximumLength);
        }
    }

    /// <summary>Finds the frame to start from using the seek table.</summary>
    /// <param name="target">The sample to reach.</param>
    /// <returns>The file offset of the frame that holds the target or the last one before it.</returns>
    private long FromSeekTable(long target)
    {
        var offset = _audioStart;
        foreach (var point in _seekPoints)
        {
            if (point.Sample > target)
            {
                break;
            }

            offset = _audioStart + point.Offset;
        }

        return offset;
    }

    /// <summary>Finds the frame that holds a sample by halving the range of the file, reading the sample number of the frame found at each halfway point.</summary>
    /// <param name="target">The sample to reach.</param>
    /// <returns>The file offset of a frame at or before the target.</returns>
    private long Bisect(long target)
    {
        var low = _audioStart;
        var high = _stream.Length;
        var best = _audioStart;
        while (high - low > ReadSize)
        {
            var middle = low + ((high - low) >> 1);
            var at = NextSync(middle);
            if (at < 0 || FrameLengthAt(at, out var header) <= 0)
            {
                high = middle;
                continue;
            }

            if (header.FirstSample(_info.MinBlockSize) <= target)
            {
                best = at;
                low = at;
                continue;
            }

            high = middle;
        }

        return best;
    }

    /// <summary>A point in the seek table.</summary>
    /// <param name="Sample">The first sample of the frame.</param>
    /// <param name="Offset">The offset of the frame from the first frame.</param>
    private readonly record struct SeekPoint(long Sample, long Offset);
}
