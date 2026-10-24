// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.H264;
using HyperPdf.Codecs.H265;

namespace HyperPdf.Codecs;

/// <summary>Makes the decoder for a picture track.</summary>
public static class VideoDecoders
{
    /// <summary>Finds how many pictures a track lets come before another in decoding order and after it in output order.</summary>
    /// <param name="track">The picture track.</param>
    /// <returns>The number of pictures, or -1 when the track's coding or configuration cannot be read.</returns>
    public static int ReorderDepth(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        return track.Kind != MediaTrackKind.Video ? -1 : track.Codec switch
        {
            MediaCodec.H264 => H264Decoder.ReorderDepth(track),
            MediaCodec.H265 => H265Decoder.ReorderDepth(track),
            _ => -1,
        };
    }

    /// <summary>Makes a decoder.</summary>
    /// <param name="track">The picture track.</param>
    /// <returns>The decoder, or <see langword="null"/> when this library does not decode the track's coding.</returns>
    public static VideoDecoder? Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.Kind != MediaTrackKind.Video)
        {
            return null;
        }

        try
        {
            return track.Codec switch
            {
                MediaCodec.H264 => H264Decoder.Create(track),
                MediaCodec.H265 => H265Decoder.Create(track),
                _ => null,
            };
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
