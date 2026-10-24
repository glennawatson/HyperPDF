// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Where the samples of a predicted block go.</summary>
/// <param name="Plane">The plane.</param>
/// <param name="Stride">The samples in a row of the plane.</param>
/// <param name="X">The column of the block's first sample in the plane.</param>
/// <param name="Y">The row of the block's first sample in the plane.</param>
internal readonly record struct H264PlaneTarget(byte[] Plane, int Stride, int X, int Y);
