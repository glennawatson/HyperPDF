// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>How the samples around one segment of an edge are laid out in a plane.</summary>
/// <param name="Across">The distance between neighbouring samples across the edge.</param>
/// <param name="Along">The distance between the sets of samples along the edge.</param>
/// <param name="Length">The sets of samples in the segment.</param>
internal readonly record struct H264EdgeLine(int Across, int Along, int Length);
