// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The explicit prediction weights of a slice.</summary>
/// <param name="LumaLog2Denom">The log2 of the denominator of the luma weights.</param>
/// <param name="ChromaLog2Denom">The log2 of the denominator of the chroma weights.</param>
/// <param name="L0">The weights of reference list 0.</param>
/// <param name="L1">The weights of reference list 1.</param>
internal sealed record H264WeightTable(int LumaLog2Denom, int ChromaLog2Denom, H264Weight[] L0, H264Weight[] L1);
