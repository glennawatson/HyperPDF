// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Four samples on one side of an edge, nearest the edge first.</summary>
/// <param name="S0">The sample next to the edge.</param>
/// <param name="S1">The second sample.</param>
/// <param name="S2">The third sample.</param>
/// <param name="S3">The fourth sample.</param>
internal readonly record struct H264EdgeSide(int S0, int S1, int S2, int S3);
