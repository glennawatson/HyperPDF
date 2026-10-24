// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>One coding tree block of one colour component, to which sample adaptive offset is applied.</summary>
/// <param name="Component">The colour component.</param>
/// <param name="Ctb">The raster scan address of the coding tree block.</param>
/// <param name="Source">The deblocked samples of the whole plane, which the offsets are worked out from.</param>
/// <param name="Target">The plane the offset samples are written to.</param>
/// <param name="Area">The block in its colour plane.</param>
/// <param name="PlaneWidth">The width of the plane in samples.</param>
/// <param name="PlaneHeight">The height of the plane in samples.</param>
/// <param name="BitDepth">The bit depth of the plane.</param>
internal readonly record struct H265SaoBlock(int Component, int Ctb, ushort[] Source, ushort[] Target, BlockRect Area, int PlaneWidth, int PlaneHeight, int BitDepth);
