// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Where a 4x4 sub-block lies in its residual block.</summary>
/// <param name="X">The column of the sub-block, in sub-blocks.</param>
/// <param name="Y">The row of the sub-block, in sub-blocks.</param>
/// <param name="Index">The sub-block's index in scan order.</param>
/// <param name="PreviousCoded">Bit 0 set when the sub-block to the right is coded, bit 1 when the one below is.</param>
internal readonly record struct H265SubBlockPosition(int X, int Y, int Index, int PreviousCoded);
