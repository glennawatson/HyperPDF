// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperCodecs;
using HyperCodecs.Video;
using HyperPdf.Core.Media;

namespace HyperPdf.Media;

/// <summary>The pictures of a run: the track, its decoder, the scaler to the shown size, and where the pictures go.</summary>
/// <param name="Track">The number of the picture track.</param>
/// <param name="Decoder">Decodes the packets of the track.</param>
/// <param name="Scaler">Converts the decoded pictures to the shown size.</param>
/// <param name="Pump">Receives the shown pictures.</param>
/// <param name="Shown">Called after each picture is shown.</param>
[DebuggerDisplay("ManagedPicture: track {Track}")]
internal sealed record ManagedPicture(int Track, VideoDecoder Decoder, VideoScaler Scaler, FramePump Pump, Action Shown);
