// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>What a track fragment header says.</summary>
/// <param name="TrackIndex">The track's index in the movie.</param>
/// <param name="BaseOffset">The file offset that sample data offsets count from.</param>
/// <param name="Duration">The default duration of a sample in ticks.</param>
/// <param name="Size">The default size of a sample.</param>
/// <param name="Flags">The default flags of a sample.</param>
internal readonly record struct Mp4FragmentHeader(int TrackIndex, long BaseOffset, int Duration, int Size, int Flags);
