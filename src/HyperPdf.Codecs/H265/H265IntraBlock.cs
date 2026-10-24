// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A block to predict with intra prediction.</summary>
/// <param name="CIdx">The colour component.</param>
/// <param name="X">The column of the block's first sample in its colour plane.</param>
/// <param name="Y">The row of the block's first sample in its colour plane.</param>
/// <param name="Log2Size">The base 2 logarithm of the block side.</param>
/// <param name="Mode">The intra prediction mode.</param>
/// <param name="Bypass">Whether the coding unit bypasses transform and quantisation.</param>
internal readonly record struct H265IntraBlock(int CIdx, int X, int Y, int Log2Size, int Mode, bool Bypass);
