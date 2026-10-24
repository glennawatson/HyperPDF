// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>One block to interpolate from a reference plane.</summary>
/// <param name="Plane">The reference plane.</param>
/// <param name="PlaneWidth">The width of the plane in samples.</param>
/// <param name="PlaneHeight">The height of the plane in samples.</param>
/// <param name="Source">The block's position in the plane at whole sample precision, and its size.</param>
/// <param name="FracX">The horizontal fraction: quarter samples for luma, eighth samples for chroma.</param>
/// <param name="FracY">The vertical fraction.</param>
/// <param name="BitDepth">The bit depth of the plane.</param>
/// <param name="Chroma">Whether the block is chroma, which uses the four tap filter.</param>
internal readonly record struct H265InterpolationJob(ushort[] Plane, int PlaneWidth, int PlaneHeight, BlockRect Source, int FracX, int FracY, int BitDepth, bool Chroma);
