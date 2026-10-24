// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The sizes of the blocks a picture is divided into, as base 2 logarithms of their sides.</summary>
/// <param name="MinCbLog2">The smallest coding block.</param>
/// <param name="CtbLog2">The coding tree block, the largest coding block.</param>
/// <param name="MinTbLog2">The smallest transform block.</param>
/// <param name="MaxTbLog2">The largest transform block.</param>
/// <param name="MaxDepthInter">The most levels a transform tree may split below an inter coding block.</param>
/// <param name="MaxDepthIntra">The most levels a transform tree may split below an intra coding block.</param>
internal readonly record struct H265BlockSizes(int MinCbLog2, int CtbLog2, int MinTbLog2, int MaxTbLog2, int MaxDepthInter, int MaxDepthIntra);
