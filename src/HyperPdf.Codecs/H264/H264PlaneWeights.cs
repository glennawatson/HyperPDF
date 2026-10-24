// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The weights that combine two predictions of one colour plane.</summary>
/// <param name="First">The weight of the list 0 prediction.</param>
/// <param name="Second">The weight of the list 1 prediction.</param>
/// <param name="Offset">The sum of the two offsets.</param>
/// <param name="LogDenom">The log2 of the denominator.</param>
/// <param name="Weighted">Whether the weights apply; when not, the two predictions are averaged.</param>
internal readonly record struct H264PlaneWeights(int First, int Second, int Offset, int LogDenom, bool Weighted);
