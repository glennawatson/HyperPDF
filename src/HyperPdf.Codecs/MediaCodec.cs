// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>The ways sound, pictures and captions are coded.</summary>
public enum MediaCodec
{
    /// <summary>A coding this library does not read.</summary>
    Unknown = 0,

    /// <summary>Uncompressed sound as whole numbers, a-law or mu-law.</summary>
    Pcm = 1,

    /// <summary>Uncompressed sound as floating point numbers.</summary>
    PcmFloat = 2,

    /// <summary>MPEG-1, MPEG-2 and MPEG-2.5 Layer III sound.</summary>
    Mp3 = 3,

    /// <summary>AAC sound, Low Complexity.</summary>
    Aac = 4,

    /// <summary>FLAC lossless sound.</summary>
    Flac = 5,

    /// <summary>Vorbis sound.</summary>
    Vorbis = 6,

    /// <summary>Opus sound.</summary>
    Opus = 7,

    /// <summary>A-law companded sound.</summary>
    ALaw = 8,

    /// <summary>Mu-law companded sound.</summary>
    MuLaw = 9,

    /// <summary>H.264 pictures.</summary>
    H264 = 10,

    /// <summary>Plain timed text.</summary>
    Text = 11,

    /// <summary>H.265 pictures.</summary>
    H265 = 12,
}
