// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>What a neighbouring block holds for motion vector prediction.</summary>
/// <param name="Available">Whether the block is in the picture, in the slice and already decoded.</param>
/// <param name="RefIdx">The block's reference index, or -1 when it is intra coded or not available.</param>
/// <param name="X">The block's horizontal motion vector.</param>
/// <param name="Y">The block's vertical motion vector.</param>
internal readonly record struct H264MotionNeighbour(bool Available, int RefIdx, int X, int Y);
