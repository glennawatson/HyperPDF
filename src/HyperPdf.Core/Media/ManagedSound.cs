// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperCodecs;
using HyperCodecs.Audio;

namespace HyperPdf.Core.Media;

/// <summary>The sound of a run: the track, its decoder, the converter to the output's rate, and the output.</summary>
/// <param name="Track">The number of the sound track.</param>
/// <param name="Decoder">Decodes the packets of the track.</param>
/// <param name="Resampler">Brings the sound to the output's rate and stereo.</param>
/// <param name="Output">The sound output.</param>
[DebuggerDisplay("ManagedSound: track {Track}")]
internal sealed record ManagedSound(int Track, AudioDecoder Decoder, AudioResampler Resampler, IStreamingAudioOutput Output);
