// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>A track of a Matroska file: the track as the library describes it, and what blocks need to find it and time its frames.</summary>
/// <param name="Number">The number the blocks name the track by.</param>
/// <param name="Track">The track as the library describes it.</param>
/// <param name="FrameSeconds">The length of a frame in seconds when the file says, otherwise 0.</param>
/// <param name="DelaySeconds">The seconds the codec needs before the first sound, which the times of the frames include.</param>
internal sealed record MatroskaTrack(int Number, MediaTrack Track, double FrameSeconds, double DelaySeconds);
