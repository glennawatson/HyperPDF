// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdf.Codecs.Containers;

/// <summary>A RIFF/WAVE file: one track of uncompressed, a-law or mu-law sound.</summary>
[DebuggerDisplay("WaveContainer: {Tracks[0].Codec} {Tracks[0].SampleRate} Hz")]
internal sealed class WaveContainer : MediaContainer
{
    /// <summary>The frames in each packet.</summary>
    private const int FramesPerPacket = 4_096;

    /// <summary>The length of the name of a chunk.</summary>
    private const int TagLength = 4;

    /// <summary>The size of a chunk header: a four character name and a length.</summary>
    private const int ChunkHeaderSize = 8;

    /// <summary>The offset of the length in a chunk header.</summary>
    private const int ChunkLengthOffset = 4;

    /// <summary>The size of the header at the start of the file: RIFF, a length and WAVE.</summary>
    private const int FileHeaderSize = 12;

    /// <summary>The offset of the form name in the header at the start of the file.</summary>
    private const int FormOffset = 8;

    /// <summary>The smallest <c>fmt </c> chunk, up to and including the bits in each sample.</summary>
    private const int MinimumFormatSize = 16;

    /// <summary>The longest format chunk read from the stack; longer ones are read into an array.</summary>
    private const int StackFormatLimit = 256;

    /// <summary>The offset of the number of channels in the format chunk.</summary>
    private const int ChannelsOffset = 2;

    /// <summary>The offset of the sample rate in the format chunk.</summary>
    private const int RateOffset = 4;

    /// <summary>The offset of the bits in each sample in the format chunk.</summary>
    private const int BitsOffset = 14;

    /// <summary>The offset in an extensible format of the sub-format code.</summary>
    private const int SubFormatOffset = 24;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The format code of uncompressed whole number sound.</summary>
    private const int PcmFormat = 1;

    /// <summary>The format code of floating point sound.</summary>
    private const int FloatFormat = 3;

    /// <summary>The format code of a-law sound.</summary>
    private const int ALawFormat = 6;

    /// <summary>The format code of mu-law sound.</summary>
    private const int MuLawFormat = 7;

    /// <summary>The format code that says the real code follows in an extension.</summary>
    private const int ExtensibleFormat = 0xFFFE;

    /// <summary>The length a streaming writer leaves in a chunk when it does not know the length.</summary>
    private const uint UnknownLength = uint.MaxValue;

    /// <summary>The file.</summary>
    private readonly Stream _stream;

    /// <summary>Where the sound starts in the file.</summary>
    private readonly long _dataStart;

    /// <summary>The number of frames in the sound.</summary>
    private readonly long _frameCount;

    /// <summary>The bytes in one frame, which holds a sample for every channel.</summary>
    private readonly int _frameSize;

    /// <summary>The frames read so far.</summary>
    private long _position;

    /// <summary>Initializes a new instance of the <see cref="WaveContainer"/> class.</summary>
    /// <param name="stream">The file, which the container owns.</param>
    /// <param name="track">The sound track.</param>
    /// <param name="dataStart">Where the sound starts in the file.</param>
    /// <param name="frameCount">The number of frames in the sound.</param>
    private WaveContainer(Stream stream, MediaTrack track, long dataStart, long frameCount)
    {
        _stream = stream;
        Tracks = [track];
        _dataStart = dataStart;
        _frameCount = frameCount;
        _frameSize = track.Channels * (track.BitsPerSample / BitsPerByte);
        Duration = TimeSpan.FromSeconds((double)frameCount / track.SampleRate);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks { get; }

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        var frames = (int)Math.Min(FramesPerPacket, _frameCount - _position);
        if (frames <= 0)
        {
            return false;
        }

        var track = Tracks[0];
        var buffer = packet.Begin(0, frames * _frameSize, (double)_position / track.SampleRate, (double)frames / track.SampleRate, true);
        var whole = _stream.ReadAtLeast(buffer, buffer.Length, false) / _frameSize;
        if (whole == 0)
        {
            _position = _frameCount;
            return false;
        }

        if (whole != frames)
        {
            // The file is shorter than its header says; what is there is played and the rest is not.
            _ = packet.Begin(0, whole * _frameSize, (double)_position / track.SampleRate, (double)whole / track.SampleRate, true);
            _position = _frameCount;
            return true;
        }

        _position += frames;
        return true;
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        var frame = (long)(position.TotalSeconds * Tracks[0].SampleRate);
        _position = Math.Clamp(frame, 0, _frameCount);
        _stream.Position = _dataStart + (_position * _frameSize);
        return true;
    }

    /// <summary>Opens a stream as a WAVE file.</summary>
    /// <param name="stream">The file; it is left where it was when it is not a WAVE file.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/> when the stream is not a WAVE file this library reads.</returns>
    internal static WaveContainer? TryOpen(Stream stream)
    {
        var start = stream.Position;
        Span<byte> header = stackalloc byte[FileHeaderSize];
        if (stream.Read(header) != FileHeaderSize || !header[..TagLength].SequenceEqual("RIFF"u8) || !header[FormOffset..].SequenceEqual("WAVE"u8))
        {
            stream.Position = start;
            return null;
        }

        var opened = Scan(stream);
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

    /// <summary>Reads the chunks to find the format and the sound.</summary>
    /// <param name="stream">The file, just after the file header.</param>
    /// <returns>The container, or <see langword="null"/> when the file has no format and sound chunks or a format this library cannot read.</returns>
    private static WaveContainer? Scan(Stream stream)
    {
        MediaTrack? track = null;
        Span<byte> chunk = stackalloc byte[ChunkHeaderSize];
        while (stream.Read(chunk) == ChunkHeaderSize)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(chunk[ChunkLengthOffset..]);
            var chunkName = chunk[..TagLength];
            if (chunkName.SequenceEqual("fmt "u8))
            {
                track = ReadFormat(stream, length);
            }
            else if (chunkName.SequenceEqual("data"u8))
            {
                return track is null ? null : Open(stream, track, length);
            }
            else if (!Skip(stream, length))
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>Makes the container once the sound chunk is found.</summary>
    /// <param name="stream">The file, at the start of the sound.</param>
    /// <param name="track">The sound track.</param>
    /// <param name="length">The length of the sound chunk, or the value a streaming writer leaves.</param>
    /// <returns>The container, or <see langword="null"/> when the track cannot be read.</returns>
    private static WaveContainer? Open(Stream stream, MediaTrack track, uint length)
    {
        if (track.Codec == MediaCodec.Unknown || track.Channels <= 0 || track.SampleRate <= 0 || track.BitsPerSample < BitsPerByte || (track.BitsPerSample % BitsPerByte) != 0)
        {
            return null;
        }

        var start = stream.Position;
        var available = stream.Length - start;
        var size = length is UnknownLength or 0 || length > available ? available : length;
        var frame = track.Channels * (track.BitsPerSample / BitsPerByte);
        return new(stream, track, start, size / frame);
    }

    /// <summary>Reads a format chunk.</summary>
    /// <param name="stream">The file, at the start of the chunk's content.</param>
    /// <param name="length">The length of the chunk.</param>
    /// <returns>The track, whose codec is <see cref="MediaCodec.Unknown"/> for a format this library cannot read; <see langword="null"/> when the chunk is too short.</returns>
    private static MediaTrack? ReadFormat(Stream stream, uint length)
    {
        if (length is < MinimumFormatSize or > ushort.MaxValue)
        {
            return null;
        }

        Span<byte> format = length <= StackFormatLimit ? stackalloc byte[(int)length] : new byte[length];
        if (stream.Read(format) != format.Length)
        {
            return null;
        }

        if ((length & 1) != 0)
        {
            _ = stream.Seek(1, SeekOrigin.Current);
        }

        var code = BinaryPrimitives.ReadUInt16LittleEndian(format);
        if (code == ExtensibleFormat && format.Length >= SubFormatOffset + ChannelsOffset)
        {
            code = BinaryPrimitives.ReadUInt16LittleEndian(format[SubFormatOffset..]);
        }

        var channels = BinaryPrimitives.ReadUInt16LittleEndian(format[ChannelsOffset..]);
        var rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(format[RateOffset..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(format[BitsOffset..]);
        return new(0, MediaTrackKind.Audio, CodecOf(code), rate, channels, bits, 0, 0, default);
    }

    /// <summary>Gets the codec a format code stands for.</summary>
    /// <param name="code">The format code.</param>
    /// <returns>The codec.</returns>
    private static MediaCodec CodecOf(int code) =>
        code switch
        {
            PcmFormat => MediaCodec.Pcm,
            FloatFormat => MediaCodec.PcmFloat,
            ALawFormat => MediaCodec.ALaw,
            MuLawFormat => MediaCodec.MuLaw,
            _ => MediaCodec.Unknown,
        };

    /// <summary>Moves past the content of a chunk; chunks are padded to an even length.</summary>
    /// <param name="stream">The file, at the start of the chunk's content.</param>
    /// <param name="length">The length of the chunk.</param>
    /// <returns><see langword="false"/> when the chunk runs past the end of the file.</returns>
    private static bool Skip(Stream stream, uint length)
    {
        var next = stream.Position + length + (length & 1);
        if (next > stream.Length)
        {
            return false;
        }

        stream.Position = next;
        return true;
    }
}
