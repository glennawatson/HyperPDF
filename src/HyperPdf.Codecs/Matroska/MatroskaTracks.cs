// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdf.Codecs.Opus;

namespace HyperPdf.Codecs.Matroska;

/// <summary>Turns the track entries of a Matroska file into tracks the library can describe, and finds the coding of each.</summary>
internal static class MatroskaTracks
{
    /// <summary>The track type of pictures.</summary>
    private const int PictureType = 1;

    /// <summary>The track type of sound.</summary>
    private const int SoundType = 2;

    /// <summary>The track type of subtitles.</summary>
    private const int SubtitleType = 17;

    /// <summary>The nanoseconds in a second.</summary>
    private const double NanosecondsPerSecond = 1e9;

    /// <summary>The count that starts the Xiph laced list of the three Vorbis headers: the number of lengths, which is one fewer than the headers.</summary>
    private const int VorbisLacedHeaders = 2;

    /// <summary>The bytes of two lengths in the Vorbis configuration.</summary>
    private const int TwoLengths = sizeof(int) << 1;

    /// <summary>The byte value that says a length in a Xiph laced list continues in the next byte.</summary>
    private const int LacingContinues = 255;

    /// <summary>The bytes of the marker, block header and stream information that start the FLAC codec private data.</summary>
    private const int FlacMarkerAndHeader = 8;

    /// <summary>The bytes of a FLAC stream information block.</summary>
    private const int FlacStreamInfo = 34;

    /// <summary>Gets the name of the Opus codec.</summary>
    private static ReadOnlySpan<byte> OpusName => "A_OPUS"u8;

    /// <summary>Reads the tracks element.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="tracks">The tracks element.</param>
    /// <returns>The tracks the library can list, in file order.</returns>
    internal static List<MatroskaTrack> Read(ReadOnlySpan<byte> data, EbmlElement tracks)
    {
        var result = new List<MatroskaTrack>();
        var position = tracks.Start;
        while (Ebml.Next(data, ref position, tracks.End, out var element))
        {
            if (element.Id != MatroskaIds.TrackEntry)
            {
                continue;
            }

            var fields = MatroskaTrackFields.Read(data, element);
            if (fields.Type is PictureType or SoundType or SubtitleType)
            {
                result.Add(new(fields.Number, Describe(fields, result.Count), fields.DefaultDuration / NanosecondsPerSecond, fields.CodecDelay / NanosecondsPerSecond));
            }
        }

        return result;
    }

    /// <summary>Describes a track.</summary>
    /// <param name="fields">What the track entry says.</param>
    /// <param name="index">The track's number in the list.</param>
    /// <returns>The track.</returns>
    private static MediaTrack Describe(MatroskaTrackFields fields, int index)
    {
        if (fields.Type == SoundType)
        {
            var (codec, configuration) = DescribeSound(fields.CodecId, fields.Private.Span);
            var rate = codec == MediaCodec.Opus ? OpusHead.OutputRate : (int)fields.Rate;
            var bits = codec is MediaCodec.Pcm or MediaCodec.PcmFloat ? fields.BitDepth : 0;
            return new(index, MediaTrackKind.Audio, codec, rate, fields.Channels, bits, 0, 0, configuration);
        }

        if (fields.Type == PictureType)
        {
            var codec = MediaCodec.Unknown;
            if (!fields.Private.IsEmpty)
            {
                codec = fields.CodecId switch
                {
                    "V_MPEG4/ISO/AVC" => MediaCodec.H264,
                    "V_MPEGH/ISO/HEVC" => MediaCodec.H265,
                    _ => MediaCodec.Unknown,
                };
            }

            return new(index, MediaTrackKind.Video, codec, 0, 0, 0, fields.Width, fields.Height, fields.Private);
        }

        var isText = fields.CodecId is "S_TEXT/UTF8" or "S_TEXT/WEBVTT";
        return new(index, MediaTrackKind.Caption, isText ? MediaCodec.Text : MediaCodec.Unknown, 0, 0, 0, 0, 0, default);
    }

    /// <summary>Finds the coding of a sound track and the configuration its decoder takes.</summary>
    /// <param name="name">The codec name.</param>
    /// <param name="data">The codec private data.</param>
    /// <returns>The coding and its configuration.</returns>
    private static CodecSetup DescribeSound(string name, ReadOnlySpan<byte> data) =>
        name switch
        {
            "A_OPUS" when data.Length > OpusName.Length => new(MediaCodec.Opus, data.ToArray()),
            "A_VORBIS" => BuildVorbis(data),
            "A_AAC" when !data.IsEmpty => new(MediaCodec.Aac, PrefixSkip(data)),
            "A_FLAC" when data.Length >= FlacMarkerAndHeader + FlacStreamInfo => new(MediaCodec.Flac, data.Slice(FlacMarkerAndHeader, FlacStreamInfo).ToArray()),
            "A_MPEG/L3" => new(MediaCodec.Mp3, default),
            "A_PCM/INT/LIT" => new(MediaCodec.Pcm, default),
            "A_PCM/FLOAT/IEEE" => new(MediaCodec.PcmFloat, default),
            _ => new(MediaCodec.Unknown, default),
        };

    /// <summary>Puts the zero count of samples to skip in front of an audio specific config.</summary>
    /// <param name="config">The audio specific config.</param>
    /// <returns>The configuration the AAC decoder takes.</returns>
    private static byte[] PrefixSkip(ReadOnlySpan<byte> config)
    {
        var result = new byte[sizeof(int) + config.Length];
        config.CopyTo(result.AsSpan(sizeof(int)));
        return result;
    }

    /// <summary>Builds the Vorbis decoder's configuration from the three headers, which Matroska keeps in a Xiph laced list.</summary>
    /// <param name="data">The codec private data.</param>
    /// <returns>The coding and its configuration, or an unknown coding when the list is damaged.</returns>
    private static CodecSetup BuildVorbis(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || data[0] != VorbisLacedHeaders)
        {
            return new(MediaCodec.Unknown, default);
        }

        var position = 1;
        var identification = ReadLacedLength(data, ref position);
        var comment = ReadLacedLength(data, ref position);
        var setupStart = position + identification + comment;
        if (identification < 0 || comment < 0 || setupStart > data.Length)
        {
            return new(MediaCodec.Unknown, default);
        }

        var setup = data[setupStart..];
        var result = new byte[TwoLengths + identification + setup.Length];
        BinaryPrimitives.WriteInt32LittleEndian(result, identification);
        data.Slice(position, identification).CopyTo(result.AsSpan(sizeof(int)));
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(sizeof(int) + identification), setup.Length);
        setup.CopyTo(result.AsSpan(TwoLengths + identification));
        return new(MediaCodec.Vorbis, result);
    }

    /// <summary>Reads one length of a Xiph laced list: bytes of 255 add up until a smaller byte ends the number.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="position">The position of the length, which moves past it.</param>
    /// <returns>The length, or -1 when the bytes run out.</returns>
    private static int ReadLacedLength(ReadOnlySpan<byte> data, ref int position)
    {
        var length = 0;
        while (position < data.Length)
        {
            var next = data[position];
            position++;
            length += next;
            if (next != LacingContinues)
            {
                return length;
            }
        }

        return -1;
    }
}
