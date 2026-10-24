// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The motion direct prediction gives a 4x4 block.</summary>
/// <param name="Ref0">The list 0 reference index, or -1 when the block does not use list 0.</param>
/// <param name="Ref1">The list 1 reference index, or -1 when the block does not use list 1.</param>
/// <param name="Mv0">The list 0 motion vector.</param>
/// <param name="Mv1">The list 1 motion vector.</param>
internal readonly record struct H264DirectBlock(int Ref0, int Ref1, MotionVector Mv0, MotionVector Mv1);
