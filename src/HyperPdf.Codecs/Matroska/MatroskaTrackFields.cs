// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Matroska;

/// <summary>What a track entry says, collected while its elements are read and turned into a track afterwards.</summary>
[DebuggerDisplay("MatroskaTrackFields: track {Number}, {CodecId}")]
internal sealed class MatroskaTrackFields
{
    /// <summary>Gets or sets the track's number.</summary>
    internal int Number { get; set; }

    /// <summary>Gets or sets the track's kind: 1 for pictures, 2 for sound, 17 for subtitles.</summary>
    internal int Type { get; set; }

    /// <summary>Gets or sets the codec name.</summary>
    internal string CodecId { get; set; } = string.Empty;

    /// <summary>Gets or sets the bytes the track's decoder is set up with.</summary>
    internal ReadOnlyMemory<byte> Private { get; set; }

    /// <summary>Gets or sets the default length of a frame in nanoseconds.</summary>
    internal long DefaultDuration { get; set; }

    /// <summary>Gets or sets the time the codec needs before the first sound, in nanoseconds.</summary>
    internal long CodecDelay { get; set; }

    /// <summary>Gets or sets the width of the pictures.</summary>
    internal int Width { get; set; }

    /// <summary>Gets or sets the height of the pictures.</summary>
    internal int Height { get; set; }

    /// <summary>Gets or sets the samples a second.</summary>
    internal double Rate { get; set; }

    /// <summary>Gets or sets the number of channels.</summary>
    internal int Channels { get; set; }

    /// <summary>Gets or sets the bits in a sample.</summary>
    internal int BitDepth { get; set; }

    /// <summary>Reads the elements of a track entry.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="entry">The track entry element.</param>
    /// <returns>The fields.</returns>
    internal static MatroskaTrackFields Read(ReadOnlySpan<byte> data, EbmlElement entry)
    {
        var fields = new MatroskaTrackFields();
        var position = entry.Start;
        while (Ebml.Next(data, ref position, entry.End, out var element))
        {
            fields.Apply(data, element);
        }

        return fields;
    }

    /// <summary>Reads one element of a track entry.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="element">The element.</param>
    private void Apply(ReadOnlySpan<byte> data, EbmlElement element)
    {
        var content = data[element.Start..element.End];
        switch (element.Id)
        {
            case MatroskaIds.TrackNumber:
            {
                Number = (int)Ebml.ReadUnsigned(content);
                break;
            }

            case MatroskaIds.TrackType:
            {
                Type = (int)Ebml.ReadUnsigned(content);
                break;
            }

            case MatroskaIds.CodecId:
            {
                CodecId = Ebml.ReadString(content);
                break;
            }

            case MatroskaIds.CodecPrivate:
            {
                Private = content.ToArray();
                break;
            }

            case MatroskaIds.DefaultDuration:
            {
                DefaultDuration = (long)Ebml.ReadUnsigned(content);
                break;
            }

            case MatroskaIds.CodecDelay:
            {
                CodecDelay = (long)Ebml.ReadUnsigned(content);
                break;
            }

            case MatroskaIds.Video:
            {
                ReadVideo(data, element);
                break;
            }

            case MatroskaIds.Audio:
            {
                ReadAudio(data, element);
                break;
            }
        }
    }

    /// <summary>Reads the picture settings.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="video">The picture settings element.</param>
    private void ReadVideo(ReadOnlySpan<byte> data, EbmlElement video)
    {
        var position = video.Start;
        while (Ebml.Next(data, ref position, video.End, out var element))
        {
            var content = data[element.Start..element.End];
            Width = element.Id == MatroskaIds.PixelWidth ? (int)Ebml.ReadUnsigned(content) : Width;
            Height = element.Id == MatroskaIds.PixelHeight ? (int)Ebml.ReadUnsigned(content) : Height;
        }
    }

    /// <summary>Reads the sound settings.</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="audio">The sound settings element.</param>
    private void ReadAudio(ReadOnlySpan<byte> data, EbmlElement audio)
    {
        var position = audio.Start;
        while (Ebml.Next(data, ref position, audio.End, out var element))
        {
            var content = data[element.Start..element.End];
            Rate = element.Id == MatroskaIds.SamplingFrequency ? Ebml.ReadFloat(content) : Rate;
            Channels = element.Id == MatroskaIds.Channels ? (int)Ebml.ReadUnsigned(content) : Channels;
            BitDepth = element.Id == MatroskaIds.BitDepth ? (int)Ebml.ReadUnsigned(content) : BitDepth;
        }
    }
}
