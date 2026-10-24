// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>One frame of a block: where its bytes are in the file and when it plays.</summary>
/// <param name="Offset">The file offset of the frame's bytes.</param>
/// <param name="Length">The number of bytes.</param>
/// <param name="Track">The track's index in the container's list.</param>
/// <param name="Time">When the frame starts, in seconds.</param>
/// <param name="Duration">How long the frame lasts in seconds, or 0.</param>
/// <param name="IsKey">Whether decoding can start at the frame.</param>
internal readonly record struct MatroskaFrame(long Offset, int Length, int Track, double Time, double Duration, bool IsKey);
