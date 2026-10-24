// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs;

/// <summary>One track of a media file, as the file describes it.</summary>
/// <param name="Index">The track's number in the file, which each packet names.</param>
/// <param name="Kind">What the track holds.</param>
/// <param name="Codec">How the track is coded.</param>
/// <param name="SampleRate">For sound, the samples a second for each channel; otherwise 0.</param>
/// <param name="Channels">For sound, the number of channels; otherwise 0.</param>
/// <param name="BitsPerSample">For uncompressed sound, the bits in each sample; otherwise 0.</param>
/// <param name="Width">For pictures, the width in pixels; otherwise 0.</param>
/// <param name="Height">For pictures, the height in pixels; otherwise 0.</param>
/// <param name="Configuration">The bytes the decoder is set up with, such as the FLAC stream information or the H.264 parameter sets; empty when the codec needs none.</param>
[DebuggerDisplay("MediaTrack: {Index} {Kind} {Codec}")]
public sealed record MediaTrack(
    int Index,
    MediaTrackKind Kind,
    MediaCodec Codec,
    int SampleRate,
    int Channels,
    int BitsPerSample,
    int Width,
    int Height,
    ReadOnlyMemory<byte> Configuration);
