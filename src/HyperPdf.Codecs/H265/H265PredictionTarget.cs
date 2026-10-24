// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>Where a prediction is written: a plane, its row length and the offset of the block's first sample.</summary>
/// <param name="Plane">The plane.</param>
/// <param name="Stride">The samples in a row of the plane.</param>
/// <param name="Offset">The index of the block's first sample.</param>
internal readonly record struct H265PredictionTarget(ushort[] Plane, int Stride, int Offset);
