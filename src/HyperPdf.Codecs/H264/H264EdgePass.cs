// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>One pass of the deblocking filter over a plane of a macroblock.</summary>
/// <param name="Address">The macroblock.</param>
/// <param name="Origin">The index in the plane of the macroblock's first sample.</param>
/// <param name="Direction">0 for the vertical edges, 1 for the horizontal edges.</param>
internal readonly record struct H264EdgePass(int Address, int Origin, int Direction);
