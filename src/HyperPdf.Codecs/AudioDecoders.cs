// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Aac;
using HyperPdf.Codecs.Audio;
using HyperPdf.Codecs.Flac;
using HyperPdf.Codecs.Mp3;
using HyperPdf.Codecs.Opus;
using HyperPdf.Codecs.Vorbis;

namespace HyperPdf.Codecs;

/// <summary>Makes the decoder for a sound track.</summary>
public static class AudioDecoders
{
    /// <summary>Makes a decoder.</summary>
    /// <param name="track">The sound track.</param>
    /// <returns>The decoder, or <see langword="null"/> when this library does not decode the track's coding.</returns>
    public static AudioDecoder? Create(MediaTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (track.Kind != MediaTrackKind.Audio)
        {
            return null;
        }

        try
        {
            return track.Codec switch
            {
                MediaCodec.Pcm or MediaCodec.PcmFloat or MediaCodec.ALaw or MediaCodec.MuLaw => new PcmDecoder(track),
                MediaCodec.Flac => FlacDecoder.Create(track),
                MediaCodec.Mp3 => Mp3Decoder.Create(track),
                MediaCodec.Vorbis => VorbisDecoder.Create(track),
                MediaCodec.Opus => OpusDecoder.Create(track),
                MediaCodec.Aac => AacDecoder.Create(track),
                _ => null,
            };
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
