// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How a sequence works out the picture order count of its pictures.</summary>
/// <param name="Type">The picture order count type: 0 codes the low bits of the count, 1 derives it from the frame number and a cycle of offsets, 2 follows the decoding order.</param>
/// <param name="Log2MaxLsb">For type 0, the bits of the coded low part of the count.</param>
/// <param name="DeltaAlwaysZero">For type 1, whether the slice headers carry no offsets.</param>
/// <param name="OffsetForNonRef">For type 1, the offset of a picture nothing refers to.</param>
/// <param name="OffsetForTopToBottom">For type 1, the offset from the top field to the bottom field.</param>
/// <param name="OffsetForRefFrame">For type 1, the offsets of the cycle of reference frames.</param>
internal sealed record H264PocSettings(int Type, int Log2MaxLsb, bool DeltaAlwaysZero, int OffsetForNonRef, int OffsetForTopToBottom, int[] OffsetForRefFrame);
