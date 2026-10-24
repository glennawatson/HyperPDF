// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>What reading a residual block found besides the coefficients.</summary>
/// <param name="Extent">The columns and rows that hold non-zero coefficients.</param>
/// <param name="TransformSkip">Whether the block skips the transform.</param>
/// <param name="Rdpcm">Whether residual DPCM applies, and in which direction.</param>
internal readonly record struct H265ResidualResult(H265BlockExtent Extent, bool TransformSkip, H265RdpcmMode Rdpcm);
