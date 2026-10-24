// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How a colour plane is laid out for the deblocking filter.</summary>
/// <param name="Stride">The samples in a row of the plane.</param>
/// <param name="MbSamples">The samples along a macroblock's side in this plane.</param>
/// <param name="SegmentLength">The samples along one segment of an edge.</param>
/// <param name="EdgeStep">How many luma edges are skipped between the edges filtered in this plane: 1 for luma, 2 for 4:2:0 chroma.</param>
/// <param name="Chroma">Whether the plane is chroma, which is filtered more gently.</param>
/// <param name="QpOffset">The offset from the luma quantiser to the plane's quantiser; unused for luma.</param>
internal readonly record struct H264PlaneLayout(int Stride, int MbSamples, int SegmentLength, int EdgeStep, bool Chroma, int QpOffset);
