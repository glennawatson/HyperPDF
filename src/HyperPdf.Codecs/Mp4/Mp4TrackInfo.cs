// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.Mp4;

/// <summary>What a track says about itself that fragments need: its number in the file, its timing and its default sample values.</summary>
/// <param name="Track">The track as the library describes it.</param>
/// <param name="TrackId">The number the file's fragments name the track by.</param>
/// <param name="Timing">How the track's ticks become seconds.</param>
/// <param name="DefaultDuration">The duration of a sample in ticks when a fragment does not say.</param>
/// <param name="DefaultSize">The size of a sample when a fragment does not say.</param>
/// <param name="DefaultFlags">The flags of a sample when a fragment does not say.</param>
[DebuggerDisplay("Mp4TrackInfo: track {TrackId} {Track.Codec}")]
internal sealed record Mp4TrackInfo(MediaTrack Track, int TrackId, Mp4Timing Timing, int DefaultDuration, int DefaultSize, int DefaultFlags);
