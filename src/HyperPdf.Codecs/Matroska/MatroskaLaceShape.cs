// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>What the frames of a laced block share.</summary>
/// <param name="Track">The track's index in the container's list.</param>
/// <param name="Start">When the first frame starts, in seconds.</param>
/// <param name="IsKey">Whether decoding can start at the frames.</param>
/// <param name="FrameSeconds">The length of a frame in seconds when the track says, otherwise 0.</param>
/// <param name="BlockDuration">The length of the whole block in seconds when the block group says, otherwise 0.</param>
internal readonly record struct MatroskaLaceShape(int Track, double Start, bool IsKey, double FrameSeconds, double BlockDuration);
