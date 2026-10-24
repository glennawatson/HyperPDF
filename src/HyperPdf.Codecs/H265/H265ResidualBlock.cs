// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Describes the transform block whose residual is read.</summary>
/// <param name="Log2Size">The base 2 logarithm of the block side.</param>
/// <param name="CIdx">The colour component: 0 luma, 1 Cb, 2 Cr.</param>
/// <param name="Intra">Whether the block belongs to an intra coding unit.</param>
/// <param name="Bypass">Whether the coding unit bypasses transform and quantisation.</param>
/// <param name="PredMode">The intra prediction mode of the block, when it is intra predicted.</param>
internal readonly record struct H265ResidualBlock(int Log2Size, int CIdx, bool Intra, bool Bypass, int PredMode);
