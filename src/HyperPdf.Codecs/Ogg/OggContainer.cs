// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using HyperPdf.Codecs.Containers;
using HyperPdf.Codecs.Opus;

namespace HyperPdf.Codecs.Ogg;

/// <summary>
/// An Ogg file holding one Vorbis stream. The three header packets are read when the file is opened and handed to the decoder as the
/// track's configuration; each later packet is one block of sound. A packet's time is the granule position at the end of the page
/// before it, which is where the sound of the page starts.
/// </summary>
[DebuggerDisplay("OggContainer: {Duration}")]
internal sealed class OggContainer : MediaContainer
{
    /// <summary>The header packets of a Vorbis stream.</summary>
    private const int VorbisHeaders = 3;

    /// <summary>The bytes of the packet type and the word "vorbis".</summary>
    private const int VorbisMarker = 7;

    /// <summary>The index of the sample rate in a Vorbis identification header.</summary>
    private const int RateIndex = 12;

    /// <summary>The index of the channel count in a Vorbis identification header.</summary>
    private const int ChannelIndex = 11;

    /// <summary>The bytes at the end of the file searched for the last page.</summary>
    private const int TailSearch = 65_536;

    /// <summary>The range of the file below which the search for a page to seek to turns to reading page after page.</summary>
    private const int ScanRange = 65_536;

    /// <summary>The bytes of a length in the configuration.</summary>
    private const int LengthBytes = 4;

    /// <summary>The headers in the configuration, each after its length.</summary>
    private const int ConfigurationParts = 2;

    /// <summary>The file.</summary>
    private readonly FileWindow _window;

    /// <summary>Reads the packets.</summary>
    private readonly OggPacketReader _reader;

    /// <summary>The file offset where the sound packets start.</summary>
    private readonly long _dataStart;

    /// <summary>The samples a second.</summary>
    private readonly int _rate;

    /// <summary>The samples at the start of the granule positions that are not sound.</summary>
    private readonly int _preSkip;

    /// <summary>Initializes a new instance of the <see cref="OggContainer"/> class.</summary>
    /// <param name="window">The file.</param>
    /// <param name="reader">The packet reader, positioned at the first sound packet.</param>
    /// <param name="track">The track.</param>
    /// <param name="dataStart">The file offset of the first sound page.</param>
    /// <param name="preSkip">The samples at the start of the stream's granule positions that are not sound, which Opus counts; zero for Vorbis.</param>
    private OggContainer(FileWindow window, OggPacketReader reader, MediaTrack track, long dataStart, int preSkip)
    {
        _window = window;
        _reader = reader;
        _dataStart = dataStart;
        _preSkip = preSkip;
        _rate = track.SampleRate;
        Tracks = [track];
        Duration = TimeSpan.FromSeconds((double)Math.Max(0, FindLastGranule(window, reader.Serial) - preSkip) / _rate);
    }

    /// <inheritdoc/>
    public override IReadOnlyList<MediaTrack> Tracks { get; }

    /// <inheritdoc/>
    public override TimeSpan Duration { get; }

    /// <summary>Gets the marker that starts every page.</summary>
    private static ReadOnlySpan<byte> PageMarker => "OggS"u8;

    /// <inheritdoc/>
    public override bool ReadPacket(MediaPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        var time = (double)(_reader.PreviousGranule - _preSkip) / _rate;
        if (!_reader.ReadPacket())
        {
            return false;
        }

        _reader.Packet.CopyTo(packet.Begin(0, _reader.Packet.Length, Math.Max(0, time), 0, true));
        return true;
    }

    /// <inheritdoc/>
    public override bool Seek(TimeSpan position)
    {
        var target = (long)(position.TotalSeconds * _rate) + _preSkip;
        var found = Bisect(target);
        var offset = found.Offset;
        while (_reader.TryReadHeader(offset, out var page) && page.Granule < target)
        {
            offset = page.Offset + page.HeaderLength + page.BodyLength;
            found = page.Serial == _reader.Serial && page.Granule >= 0 ? new(offset, page.Granule) : found;
        }

        _reader.Seek(found.Offset, found.Granule);
        return true;
    }

    /// <summary>Opens a stream as an Ogg file holding Vorbis.</summary>
    /// <param name="stream">The file; it is left where it was when it is not one.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/> when the stream is not an Ogg file of a kind this reader plays.</returns>
    internal static OggContainer? TryOpen(Stream stream)
    {
        var start = stream.Position;
        var window = new FileWindow(stream);
        var reader = new OggPacketReader(window, start);
        var headers = ReadVorbisHeaders(reader);
        if (headers is null)
        {
            stream.Position = start;
            return TryOpenOpus(stream);
        }

        var (identification, setup) = headers;
        var fixedLength = (LengthBytes * ConfigurationParts) + identification.Length + setup.Length;
        var configuration = new byte[fixedLength + sizeof(long)];
        BinaryPrimitives.WriteInt32LittleEndian(configuration, identification.Length);
        identification.CopyTo(configuration, LengthBytes);
        BinaryPrimitives.WriteInt32LittleEndian(configuration.AsSpan(LengthBytes + identification.Length), setup.Length);
        setup.CopyTo(configuration, (LengthBytes * ConfigurationParts) + identification.Length);

        // The sound ends at the granule position of the last page, which may be before the end of the last block.
        BinaryPrimitives.WriteInt64LittleEndian(configuration.AsSpan(fixedLength), FindLastGranule(window, reader.Serial));
        var rate = BinaryPrimitives.ReadInt32LittleEndian(identification.AsSpan(RateIndex));
        var track = new MediaTrack(0, MediaTrackKind.Audio, MediaCodec.Vorbis, rate, identification[ChannelIndex], 0, 0, 0, configuration);
        return new(window, reader, track, reader.NextPageOffset, 0);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // The window does not own the stream; the container does, through it.
    }

    /// <summary>Opens a stream as an Ogg file holding Opus.</summary>
    /// <param name="stream">The file; it is left where it was when it is not one.</param>
    /// <returns>The container, or <see langword="null"/> when the stream is not an Ogg file holding Opus.</returns>
    private static OggContainer? TryOpenOpus(Stream stream)
    {
        var start = stream.Position;
        var window = new FileWindow(stream);
        var reader = new OggPacketReader(window, start);
        if (!reader.ReadPacket() || !OpusHead.TryRead(reader.Packet, out var head) || !reader.ReadPacket() || !reader.Packet.StartsWith("OpusTags"u8))
        {
            stream.Position = start;
            return null;
        }

        var track = new MediaTrack(0, MediaTrackKind.Audio, MediaCodec.Opus, OpusHead.OutputRate, head.Channels, 0, 0, 0, head.Packet);
        return new(window, reader, track, reader.NextPageOffset, head.PreSkip);
    }

    /// <summary>Reads the identification and setup packets of a Vorbis stream.</summary>
    /// <param name="reader">The packet reader at the start of the file.</param>
    /// <returns>The two packets, or <see langword="null"/> when the first stream is not Vorbis.</returns>
    private static OggHeaders? ReadVorbisHeaders(OggPacketReader reader)
    {
        byte[]? identification = null;
        byte[]? setup = null;
        for (var index = 0; index < VorbisHeaders; index++)
        {
            if (!IsVorbisHeader(reader, (byte)((index * ConfigurationParts) + 1)))
            {
                return null;
            }

            identification = index == 0 ? reader.Packet.ToArray() : identification;
            setup = index == VorbisHeaders - 1 ? reader.Packet.ToArray() : setup;
        }

        return identification is null || setup is null ? null : new(identification, setup);
    }

    /// <summary>Reads the next packet and checks that it is a Vorbis header of a type.</summary>
    /// <param name="reader">The packet reader.</param>
    /// <param name="type">The packet type byte the header must have.</param>
    /// <returns>Whether the packet is that header.</returns>
    private static bool IsVorbisHeader(OggPacketReader reader, byte type) =>
        reader.ReadPacket()
        && reader.Packet.Length >= VorbisMarker
        && reader.Packet[0] == type
        && reader.Packet.Slice(1, VorbisMarker - 1).SequenceEqual("vorbis"u8);

    /// <summary>Finds the granule position of the last page of a stream.</summary>
    /// <param name="window">The file.</param>
    /// <param name="serial">The stream number.</param>
    /// <returns>The position, or 0 when no page of the stream near the end of the file has one.</returns>
    private static long FindLastGranule(FileWindow window, uint serial)
    {
        var from = Math.Max(0, window.Length - TailSearch);
        var bytes = window.Get(from, (int)Math.Min(TailSearch, window.Length - from));
        var end = bytes.Length;
        while (end > 0)
        {
            var at = bytes[..end].LastIndexOf(PageMarker);
            if (at < 0)
            {
                return 0;
            }

            if (bytes.Length >= at + OggPacketReader.HeaderLength
                && BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + OggPacketReader.SerialAt)..]) == serial
                && BinaryPrimitives.ReadInt64LittleEndian(bytes[(at + OggPacketReader.GranuleAt)..]) is var granule and >= 0)
            {
                return granule;
            }

            end = at;
        }

        return 0;
    }

    /// <summary>Halves the range of the file to find where the pages before a position end.</summary>
    /// <param name="target">The granule position to reach.</param>
    /// <returns>The offset just after the last page found that ends before the target, and its granule position.</returns>
    private PageStart Bisect(long target)
    {
        var low = _dataStart;
        var high = _window.Length;
        var found = new PageStart(_dataStart, 0);
        while (high - low > ScanRange)
        {
            var middle = low + ((high - low) >> 1);
            if (!TryFindPage(middle, out var header))
            {
                high = middle;
            }
            else if (header.Granule >= 0 && header.Granule < target)
            {
                low = header.Offset + header.HeaderLength + header.BodyLength;
                found = new(low, header.Granule);
            }
            else
            {
                high = header.Offset;
            }
        }

        return found;
    }

    /// <summary>Finds the first page that starts at or after an offset.</summary>
    /// <param name="offset">The file offset to start looking at.</param>
    /// <param name="header">Receives the page header.</param>
    /// <returns>Whether a page of the stream was found.</returns>
    private bool TryFindPage(long offset, out OggPacketReader.PageHeader header)
    {
        header = default;
        var at = offset;
        while (at < _window.Length)
        {
            var bytes = _window.Get(at, TailSearch);
            var found = bytes.IndexOf(PageMarker);
            if (found < 0)
            {
                at += Math.Max(1, bytes.Length - PageMarker.Length);
                continue;
            }

            if (_reader.TryReadHeader(at + found, out header) && header.Serial == _reader.Serial)
            {
                return true;
            }

            at += found + 1;
        }

        return false;
    }

    /// <summary>A place to start reading pages from.</summary>
    /// <param name="Offset">The file offset of a page.</param>
    /// <param name="Granule">The granule position of the page before it.</param>
    private readonly record struct PageStart(long Offset, long Granule);

    /// <summary>The first and third header packets of a Vorbis stream.</summary>
    /// <param name="Identification">The identification packet.</param>
    /// <param name="Setup">The setup packet.</param>
    private sealed record OggHeaders(byte[] Identification, byte[] Setup);
}
