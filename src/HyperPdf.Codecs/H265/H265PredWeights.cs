// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The explicit weighted prediction parameters of a slice (7.3.6.3).</summary>
/// <param name="LumaLog2Denom">The base 2 logarithm of the denominator of the luma weights.</param>
/// <param name="ChromaLog2Denom">The base 2 logarithm of the denominator of the chroma weights.</param>
/// <param name="List0">The weights of the pictures in list 0.</param>
/// <param name="List1">The weights of the pictures in list 1, or <see langword="null"/> in a P slice.</param>
internal sealed record H265PredWeights(int LumaLog2Denom, int ChromaLog2Denom, H265WeightList List0, H265WeightList? List1);
