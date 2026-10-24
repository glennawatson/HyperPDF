// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The motion of the co-located block in the first picture of list 1, which direct prediction works from.</summary>
/// <param name="Intra">Whether the block is intra coded, or otherwise has no motion.</param>
/// <param name="RefIdx">The reference index the block used in its own picture.</param>
/// <param name="RefUid">The identity of the frame the block referred to.</param>
/// <param name="Vector">The block's motion vector.</param>
internal readonly record struct H264ColBlock(bool Intra, int RefIdx, int RefUid, MotionVector Vector);
