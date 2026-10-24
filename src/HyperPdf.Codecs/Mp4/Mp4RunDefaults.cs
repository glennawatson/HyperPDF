// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>The track a run belongs to and the values its samples have when the run does not say.</summary>
/// <param name="Timing">How the track's ticks become seconds.</param>
/// <param name="Track">The track's index in the movie.</param>
/// <param name="Base">The file offset that the run's data offset counts from.</param>
/// <param name="Duration">The duration of a sample in ticks.</param>
/// <param name="Size">The size of a sample.</param>
/// <param name="Flags">The flags of a sample.</param>
internal readonly record struct Mp4RunDefaults(Mp4Timing Timing, int Track, long Base, int Duration, int Size, int Flags);
