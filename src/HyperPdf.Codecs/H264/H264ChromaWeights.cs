// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The chroma weights and offsets for one reference picture.</summary>
/// <param name="CbWeight">The Cb weight.</param>
/// <param name="CbOffset">The Cb offset.</param>
/// <param name="CrWeight">The Cr weight.</param>
/// <param name="CrOffset">The Cr offset.</param>
internal readonly record struct H264ChromaWeights(int CbWeight, int CbOffset, int CrWeight, int CrOffset)
{
    /// <summary>Gets the weights of a picture that has none sent: a weight of 1 and no offset.</summary>
    /// <param name="log2Denom">The log2 of the denominator.</param>
    /// <returns>The weights.</returns>
    internal static H264ChromaWeights Default(int log2Denom) => new(1 << log2Denom, 0, 1 << log2Denom, 0);
}
