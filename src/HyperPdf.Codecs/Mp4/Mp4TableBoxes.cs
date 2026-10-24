// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>The boxes of a sample table that give a track's sample sizes, places and times.</summary>
/// <param name="Sizes">The sample size box.</param>
/// <param name="Runs">The sample to chunk box.</param>
/// <param name="Offsets">The chunk offset box.</param>
/// <param name="IsWide">Whether the chunk offsets are 64 bits.</param>
/// <param name="Times">The time to sample box.</param>
internal readonly record struct Mp4TableBoxes(Mp4Box Sizes, Mp4Box Runs, Mp4Box Offsets, bool IsWide, Mp4Box Times);
