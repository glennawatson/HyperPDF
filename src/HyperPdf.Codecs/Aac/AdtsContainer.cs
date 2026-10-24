// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Containers;

namespace HyperPdf.Codecs.Aac;

/// <summary>
/// An AAC stream in ADTS frames, as a bare .aac file holds it. Every frame starts with a header that says the sample rate and channels;
/// the frames are listed when the file opens, which gives the length of the sound and exact seeking.
/// </summary>
[DebuggerDisplay("AdtsContainer: {_offsets.Count} frames, {Duration}")]
internal sealed class AdtsContainer : MediaContainer
{
    /// <summary>The bytes of an ID3 tag header.</summary>
    private const int Id3Header = 10;

    /// <summary>The bytes of the size at the end of an ID3 tag header.</summary>
    private const int Id3SizeBytes = 4;

    /// <summary>The samples in a frame.</summary>
    private const int FrameSamples = 1024;

    /// <summary>The most frames listed, which keeps a damaged file from using all the memory.</summary>
    private const int MaxFrames = 20_000_000;

    /// <summary>The frames a file must hold in a row from its start to count as ADTS.</summary>
    private const int MinimumRun = 3;

    /// <summary>The bits of the part of an audio specific config after the object type, sample rate index and channels.</summary>
    private const int ConfigTailBits = 3;

    /// <summary>The bits of a sample rate index in the audio specific config.</summary>
    private const int RateBits = 4;

    /// <summary>The bits of the channel configuration in the audio specific config.</summary>
    private const int ChannelBits = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of an ID3 size byte that carry the number.</summary>
    private const int SizeBits = 7;

    /// <summary>The mask of those bits.</summary>
    private const int SizeMask = 0x7F;

    /// <summary>The file.</summary>
    private readonly FileWindow _window;

    /// <summary>The stream.</summary>
    private readonly Stream _stream;

    /// <summary>The file offset of each frame.</summary>
    private readonly List<long> _offsets;

    /// <summary>The samples a second.</summary>
    private readonly int _rate;

    /// <summary>The index of the next frame to read.</summary>
    private int _next;

    /// <summary>Initializes a new instance of the <see cref="AdtsContainer"/> class.</summary>
    /// <param name="stream">The file.</param>
    /// <param name="window">A window over the file.</param>
    /// <param name="offsets">The offset of each frame.</param>
    /// <param name="track">The track.</param>
    private AdtsContainer(Stream stream, FileWindow window, List<long> offsets, MediaTrack track)
    {
        _stream = stream;
        _window = window;
        _offsets = offsets;
        _rate = track.SampleRate;
        Tracks = [track];
        Duration = TimeSpan.FromSeconds((double)offsets.Count * FrameSamples / _rate);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks { get; }

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <summary>Gets the marker that starts an ID3 tag.</summary>
    private static ReadOnlySpan<byte> Id3Magic => "ID3"u8;

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (_next >= _offsets.Count || !AdtsHeader.TryRead(_window.Get(_offsets[_next], AdtsHeader.BaseSize), out var header))
        {
            return false;
        }

        var data = packet.Begin(0, header.FrameLength - header.HeaderLength, (double)_next * FrameSamples / _rate, (double)FrameSamples / _rate, true);
        _stream.Position = _offsets[_next] + header.HeaderLength;
        _next++;
        return _stream.ReadAtLeast(data, data.Length, false) == data.Length;
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        _next = Math.Clamp((int)(position.TotalSeconds * _rate / FrameSamples), 0, _offsets.Count);
        return true;
    }

    /// <summary>Opens a stream as an ADTS file.</summary>
    /// <param name="stream">The file; it is left where it was when it is not one.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/>.</returns>
    internal static AdtsContainer? TryOpen(Stream stream)
    {
        var start = stream.Position;
        var window = new FileWindow(stream);
        var offsets = new List<long>();
        var rates = AacBands.SampleRates;
        if (!ListFrames(window, SkipTag(window, start), offsets)
            || offsets.Count < MinimumRun
            || !AdtsHeader.TryRead(window.Get(offsets[0], AdtsHeader.BaseSize), out var header)
            || header.RateIndex >= rates.Length
            || header.Channels == 0)
        {
            stream.Position = start;
            return null;
        }

        var configuration = BuildConfiguration(header);
        var track = new MediaTrack(0, MediaTrackKind.Audio, MediaCodec.Aac, rates[header.RateIndex], header.Channels, 0, 0, 0, configuration);
        return new(stream, window, offsets, track);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stream.Dispose();
        }
    }

    /// <summary>Builds the decoder's configuration: no samples to skip, then the two byte audio specific config.</summary>
    /// <param name="header">The first frame's header.</param>
    /// <returns>The configuration.</returns>
    private static byte[] BuildConfiguration(AdtsHeader header)
    {
        var bits = (header.ObjectType << (RateBits + ChannelBits + ConfigTailBits))
            | (header.RateIndex << (ChannelBits + ConfigTailBits))
            | (header.Channels << ConfigTailBits);
        var configuration = new byte[sizeof(int) + sizeof(ushort)];
        configuration[sizeof(int)] = (byte)(bits >> ByteBits);
        configuration[sizeof(int) + 1] = (byte)bits;
        return configuration;
    }

    /// <summary>Skips an ID3 tag at the start of the file.</summary>
    /// <param name="window">The file.</param>
    /// <param name="start">Where the file starts.</param>
    /// <returns>The offset of the first byte after the tag.</returns>
    private static long SkipTag(FileWindow window, long start)
    {
        var bytes = window.Get(start, Id3Header);
        if (bytes.Length < Id3Header || !bytes[..Id3Magic.Length].SequenceEqual(Id3Magic))
        {
            return start;
        }

        var size = 0;
        for (var index = Id3Header - Id3SizeBytes; index < Id3Header; index++)
        {
            size = (size << SizeBits) | (bytes[index] & SizeMask);
        }

        return start + Id3Header + size;
    }

    /// <summary>Lists the frames from an offset.</summary>
    /// <param name="window">The file.</param>
    /// <param name="first">The offset of the first frame.</param>
    /// <param name="offsets">Receives the offset of each frame.</param>
    /// <returns><see langword="true"/> when the file starts with a frame.</returns>
    private static bool ListFrames(FileWindow window, long first, List<long> offsets)
    {
        var position = first;
        AdtsHeader? firstHeader = null;
        while (offsets.Count < MaxFrames && AdtsHeader.TryRead(window.Get(position, AdtsHeader.BaseSize), out var header))
        {
            firstHeader ??= header;
            if (header.RateIndex != firstHeader.Value.RateIndex || header.Channels != firstHeader.Value.Channels || position + header.FrameLength > window.Length)
            {
                break;
            }

            offsets.Add(position);
            position += header.FrameLength;
        }

        return offsets.Count > 0;
    }
}
