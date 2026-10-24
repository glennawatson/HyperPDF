// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The thresholds that decide whether and how strongly an edge is filtered.</summary>
/// <param name="Alpha">The largest step across the edge that is still filtered.</param>
/// <param name="Beta">The largest step along either side of the edge that is still filtered.</param>
/// <param name="IndexA">The index that selects the clipping value tC0.</param>
internal readonly record struct H264EdgeLimits(int Alpha, int Beta, int IndexA);
