// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.Codecs.Mp3;

namespace HyperPdf.Codecs.Mp4;

/// <summary>Reads the first sample entry of a track's sample description and turns it into a <see cref="MediaTrack"/>.</summary>
internal static class Mp4SampleEntries
{
    /// <summary>The bytes of a sample entry before its audio fields: reserved bytes and the data reference index.</summary>
    private const int EntryLead = 8;

    /// <summary>The offset of the version in an audio sample entry.</summary>
    private const int AudioVersionAt = 8;

    /// <summary>The offset of the channel count in an audio sample entry.</summary>
    private const int AudioChannelsAt = 16;

    /// <summary>The offset of the sample rate in an audio sample entry.</summary>
    private const int AudioRateAt = 24;

    /// <summary>The offset of the boxes in a version 0 audio sample entry.</summary>
    private const int AudioBoxesAt = 28;

    /// <summary>The extra bytes of a version 1 audio sample entry.</summary>
    private const int AudioVersionOneExtra = 16;

    /// <summary>The offset of the width in a visual sample entry.</summary>
    private const int VideoWidthAt = 24;

    /// <summary>The offset of the boxes in a visual sample entry.</summary>
    private const int VideoBoxesAt = 78;

    /// <summary>The bits the sample rate of an audio sample entry is shifted by, being a 16.16 number.</summary>
    private const int RateShift = 16;

    /// <summary>The object type indication of MPEG-4 audio, which is AAC.</summary>
    private const int ObjectAac = 0x40;

    /// <summary>The first object type indication of MPEG-2 AAC.</summary>
    private const int ObjectAacMpeg2First = 0x66;

    /// <summary>The last object type indication of MPEG-2 AAC.</summary>
    private const int ObjectAacMpeg2Last = 0x68;

    /// <summary>The object type indication of MPEG-2 audio, which is MP3.</summary>
    private const int ObjectMpeg2Audio = 0x69;

    /// <summary>The object type indication of MPEG-1 audio, which is MP3.</summary>
    private const int ObjectMpeg1Audio = 0x6B;

    /// <summary>The tag of an elementary stream descriptor.</summary>
    private const int TagElementaryStream = 0x03;

    /// <summary>The tag of a decoder configuration descriptor.</summary>
    private const int TagDecoderConfig = 0x04;

    /// <summary>The tag of a decoder specific information descriptor.</summary>
    private const int TagSpecificInfo = 0x05;

    /// <summary>The flag in an elementary stream descriptor that says a stream dependence follows.</summary>
    private const int FlagDependence = 0x80;

    /// <summary>The flag in an elementary stream descriptor that says a URL follows.</summary>
    private const int FlagUrl = 0x40;

    /// <summary>The flag in an elementary stream descriptor that says an OCR stream follows.</summary>
    private const int FlagOcr = 0x20;

    /// <summary>The bits of a length in a descriptor that carry the number.</summary>
    private const int LengthBits = 0x7F;

    /// <summary>The bit in a descriptor length byte that says another length byte follows.</summary>
    private const int LengthContinue = 0x80;

    /// <summary>The bytes of a descriptor length at most.</summary>
    private const int LengthBytesMax = 4;

    /// <summary>The bits each length byte of a descriptor adds.</summary>
    private const int LengthShift = 7;

    /// <summary>The bytes of the decoder configuration descriptor before its specific info.</summary>
    private const int DecoderConfigFixed = 13;

    /// <summary>The bytes of the version, channel count and flags of an Opus specific box before the pre-skip.</summary>
    private const int OpusSpecificLead = 2;

    /// <summary>The offset of the channel mapping family in the Opus specific box.</summary>
    private const int OpusSpecificFamilyAt = 10;

    /// <summary>The bytes of the Opus identification header before any channel mapping.</summary>
    private const int OpusHeadFixed = 19;

    /// <summary>The offset of the pre-skip in the Opus identification header.</summary>
    private const int OpusHeadPreSkipAt = 10;

    /// <summary>The offset of the input rate in the Opus identification header.</summary>
    private const int OpusHeadRateAt = 12;

    /// <summary>The offset of the output gain in the Opus identification header.</summary>
    private const int OpusHeadGainAt = 16;

    /// <summary>The offset of the channel mapping family in the Opus identification header.</summary>
    private const int OpusHeadFamilyAt = 18;

    /// <summary>The bytes of the header of a FLAC metadata block.</summary>
    private const int FlacBlockHeader = 4;

    /// <summary>The bytes of a FLAC stream information block.</summary>
    private const int FlacStreamInfoLength = 34;

    /// <summary>Gets the identification header's magic text.</summary>
    private static ReadOnlySpan<byte> OpusMagic => "OpusHead"u8;

    /// <summary>Describes the first sample entry of a sample description.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="description">The sample description box.</param>
    /// <param name="handler">The handler type of the track.</param>
    /// <param name="index">The track's number.</param>
    /// <param name="skipSamples">For sound coded in frames, the samples at the start that are not sound.</param>
    /// <returns>The track.</returns>
    internal static MediaTrack Describe(ReadOnlySpan<byte> data, Mp4Box description, uint handler, int index, int skipSamples)
    {
        var position = description.Start + Mp4Boxes.FullBoxSize + sizeof(uint);
        if (!Mp4Boxes.Next(data, ref position, description.End, out var entry))
        {
            return new(index, MediaTrackKind.Audio, MediaCodec.Unknown, 0, 0, 0, 0, 0, default);
        }

        if (handler == Mp4Handlers.Sound)
        {
            return DescribeAudio(data, entry, index, skipSamples);
        }

        if (handler == Mp4Handlers.Video)
        {
            return DescribeVideo(data, entry, index);
        }

        var isText = handler == Mp4Handlers.Text || handler == Mp4Handlers.Subtitle || handler == Mp4Handlers.SubtitleAlt;
        var codec = isText && entry.Type == Mp4Boxes.Type("tx3g"u8) ? MediaCodec.Text : MediaCodec.Unknown;
        return new(index, MediaTrackKind.Caption, codec, 0, 0, 0, 0, 0, default);
    }

    /// <summary>Describes an audio sample entry.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="entry">The sample entry.</param>
    /// <param name="index">The track's number.</param>
    /// <param name="skipSamples">The samples at the start that are not sound.</param>
    /// <returns>The track.</returns>
    private static MediaTrack DescribeAudio(ReadOnlySpan<byte> data, Mp4Box entry, int index, int skipSamples)
    {
        var start = entry.Start;
        if (entry.End - start < AudioBoxesAt)
        {
            return new(index, MediaTrackKind.Audio, MediaCodec.Unknown, 0, 0, 0, 0, 0, default);
        }

        var version = BinaryPrimitives.ReadUInt16BigEndian(data[(start + AudioVersionAt)..]);
        var channels = BinaryPrimitives.ReadUInt16BigEndian(data[(start + AudioChannelsAt)..]);
        var rate = (int)(BinaryPrimitives.ReadUInt32BigEndian(data[(start + AudioRateAt)..]) >> RateShift);
        var boxes = start + AudioBoxesAt + (version == 1 ? AudioVersionOneExtra : 0);
        if (version > 1)
        {
            return new(index, MediaTrackKind.Audio, MediaCodec.Unknown, rate, channels, 0, 0, 0, default);
        }

        var codec = MediaCodec.Unknown;
        ReadOnlyMemory<byte> configuration = default;
        if (entry.Type == Mp4Boxes.Type("mp4a"u8))
        {
            (codec, configuration) = DescribeElementaryStream(data, boxes, entry.End, skipSamples);
        }
        else if (entry.Type == Mp4Boxes.Type("Opus"u8))
        {
            codec = MediaCodec.Opus;
            configuration = BuildOpusHead(data, boxes, entry.End);
        }
        else if (entry.Type == Mp4Boxes.Type("fLaC"u8))
        {
            codec = MediaCodec.Flac;
            configuration = FindFlacInfo(data, boxes, entry.End);
        }

        if (codec is MediaCodec.Opus or MediaCodec.Flac && configuration.IsEmpty)
        {
            codec = MediaCodec.Unknown;
        }

        return new(index, MediaTrackKind.Audio, codec, rate, channels, 0, 0, 0, configuration);
    }

    /// <summary>Describes a visual sample entry.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="entry">The sample entry.</param>
    /// <param name="index">The track's number.</param>
    /// <returns>The track.</returns>
    private static MediaTrack DescribeVideo(ReadOnlySpan<byte> data, Mp4Box entry, int index)
    {
        var start = entry.Start;
        if (entry.End - start < VideoBoxesAt)
        {
            return new(index, MediaTrackKind.Video, MediaCodec.Unknown, 0, 0, 0, 0, 0, default);
        }

        var width = BinaryPrimitives.ReadUInt16BigEndian(data[(start + VideoWidthAt)..]);
        var height = BinaryPrimitives.ReadUInt16BigEndian(data[(start + VideoWidthAt + sizeof(ushort))..]);
        var isAvc = entry.Type == Mp4Boxes.Type("avc1"u8) || entry.Type == Mp4Boxes.Type("avc3"u8);
        var isHevc = entry.Type == Mp4Boxes.Type("hvc1"u8) || entry.Type == Mp4Boxes.Type("hev1"u8);
        ReadOnlyMemory<byte> configuration = default;
        if (isAvc && Mp4Boxes.TryFind(data, start + VideoBoxesAt, entry.End, Mp4Boxes.Type("avcC"u8), out var avcC))
        {
            configuration = data[avcC.Start..avcC.End].ToArray();
        }
        else if (isHevc && Mp4Boxes.TryFind(data, start + VideoBoxesAt, entry.End, Mp4Boxes.Type("hvcC"u8), out var hvcC))
        {
            configuration = data[hvcC.Start..hvcC.End].ToArray();
        }

        var codec = MediaCodec.Unknown;
        if (!configuration.IsEmpty)
        {
            codec = isAvc ? MediaCodec.H264 : MediaCodec.H265;
        }

        return new(index, MediaTrackKind.Video, codec, 0, 0, 0, width, height, configuration);
    }

    /// <summary>Reads the elementary stream descriptor of an MPEG-4 audio sample entry.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="start">The start of the boxes of the sample entry.</param>
    /// <param name="end">The end of the sample entry.</param>
    /// <param name="skipSamples">The samples at the start that are not sound.</param>
    /// <returns>The codec and its configuration.</returns>
    private static CodecSetup DescribeElementaryStream(ReadOnlySpan<byte> data, int start, int end, int skipSamples)
    {
        if (!Mp4Boxes.TryFind(data, start, end, Mp4Boxes.Type("esds"u8), out var esds))
        {
            return new(MediaCodec.Unknown, default);
        }

        if (!FindDecoderConfig(data, esds, out var position, out var configEnd))
        {
            return new(MediaCodec.Unknown, default);
        }

        var objectType = data[position];
        position += DecoderConfigFixed;
        if (objectType is ObjectMpeg1Audio or ObjectMpeg2Audio)
        {
            return new(MediaCodec.Mp3, Mp3StreamTag.FromStartSkip(skipSamples).ToConfiguration());
        }

        var isAac = objectType is ObjectAac or (>= ObjectAacMpeg2First and <= ObjectAacMpeg2Last);
        if (!isAac || !ReadDescriptor(data, ref position, configEnd, TagSpecificInfo, out var infoEnd))
        {
            return new(MediaCodec.Unknown, default);
        }

        var configuration = new byte[sizeof(int) + (infoEnd - position)];
        BinaryPrimitives.WriteInt32LittleEndian(configuration, skipSamples);
        data[position..infoEnd].CopyTo(configuration.AsSpan(sizeof(int)));
        return new(MediaCodec.Aac, configuration);
    }

    /// <summary>Finds the decoder configuration descriptor inside an elementary stream descriptor.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="esds">The elementary stream descriptor box.</param>
    /// <param name="position">Receives the start of the decoder configuration's content.</param>
    /// <param name="end">Receives the end of the decoder configuration.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool FindDecoderConfig(ReadOnlySpan<byte> data, Mp4Box esds, out int position, out int end)
    {
        position = esds.Start + Mp4Boxes.FullBoxSize;
        end = 0;
        if (!ReadDescriptor(data, ref position, esds.End, TagElementaryStream, out var streamEnd) || position + sizeof(ushort) + 1 > streamEnd)
        {
            return false;
        }

        position += sizeof(ushort);
        var flags = data[position];
        position++;
        position += (flags & FlagDependence) != 0 ? sizeof(ushort) : 0;
        position += (flags & FlagUrl) != 0 && position < streamEnd ? 1 + data[position] : 0;
        position += (flags & FlagOcr) != 0 ? sizeof(ushort) : 0;
        return ReadDescriptor(data, ref position, streamEnd, TagDecoderConfig, out end) && end - position >= DecoderConfigFixed;
    }

    /// <summary>Reads the tag and length of a descriptor and checks the tag.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the descriptor, which moves to its content.</param>
    /// <param name="limit">The end of the bytes that may hold the descriptor.</param>
    /// <param name="tag">The tag wanted.</param>
    /// <param name="end">Receives the end of the descriptor's content.</param>
    /// <returns><see langword="true"/> when the descriptor has the tag and fits.</returns>
    private static bool ReadDescriptor(ReadOnlySpan<byte> data, ref int position, int limit, int tag, out int end)
    {
        end = 0;
        if (position >= limit || data[position] != tag)
        {
            return false;
        }

        position++;
        var length = 0;
        for (var step = 0; step < LengthBytesMax && position < limit; step++)
        {
            var next = data[position];
            position++;
            length = (length << LengthShift) | (next & LengthBits);
            if ((next & LengthContinue) == 0)
            {
                break;
            }
        }

        end = Math.Min(position + length, limit);
        return true;
    }

    /// <summary>Builds the Opus identification header from the Opus specific box.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="start">The start of the boxes of the sample entry.</param>
    /// <param name="end">The end of the sample entry.</param>
    /// <returns>The header, or nothing when the box is missing.</returns>
    private static ReadOnlyMemory<byte> BuildOpusHead(ReadOnlySpan<byte> data, int start, int end)
    {
        if (!Mp4Boxes.TryFind(data, start, end, Mp4Boxes.Type("dOps"u8), out var box) || box.End - box.Start <= OpusSpecificFamilyAt)
        {
            return default;
        }

        var source = data[box.Start..box.End];
        var family = source[OpusSpecificFamilyAt];
        var tail = family == 0 ? 0 : source.Length - OpusSpecificFamilyAt - 1;
        var head = new byte[OpusHeadFixed + tail];
        OpusMagic.CopyTo(head);
        head[OpusMagic.Length] = 1;
        head[OpusMagic.Length + 1] = source[1];
        BinaryPrimitives.WriteUInt16LittleEndian(head.AsSpan(OpusHeadPreSkipAt), BinaryPrimitives.ReadUInt16BigEndian(source[OpusSpecificLead..]));
        BinaryPrimitives.WriteUInt32LittleEndian(head.AsSpan(OpusHeadRateAt), BinaryPrimitives.ReadUInt32BigEndian(source[(OpusSpecificLead + sizeof(ushort))..]));
        BinaryPrimitives.WriteInt16LittleEndian(head.AsSpan(OpusHeadGainAt), BinaryPrimitives.ReadInt16BigEndian(source[(OpusSpecificLead + sizeof(ushort) + sizeof(uint))..]));
        head[OpusHeadFamilyAt] = family;
        if (tail > 0)
        {
            source[(OpusSpecificFamilyAt + 1)..].CopyTo(head.AsSpan(OpusHeadFixed));
        }

        return head;
    }

    /// <summary>Finds the stream information in a FLAC specific box.</summary>
    /// <param name="data">The bytes of the movie box.</param>
    /// <param name="start">The start of the boxes of the sample entry.</param>
    /// <param name="end">The end of the sample entry.</param>
    /// <returns>The 34 bytes of stream information, or nothing when the box is missing.</returns>
    private static ReadOnlyMemory<byte> FindFlacInfo(ReadOnlySpan<byte> data, int start, int end)
    {
        if (!Mp4Boxes.TryFind(data, start, end, Mp4Boxes.Type("dfLa"u8), out var box))
        {
            return default;
        }

        var first = box.Start + Mp4Boxes.FullBoxSize + FlacBlockHeader;
        return first + FlacStreamInfoLength <= box.End ? data.Slice(first, FlacStreamInfoLength).ToArray() : default;
    }
}
