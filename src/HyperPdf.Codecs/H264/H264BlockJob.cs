// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>One 8x8 luma block to be scaled, transformed and added to the picture.</summary>
/// <param name="Group">The block's number in the macroblock, 0 to 3 in coding order.</param>
/// <param name="Offset">The index in the luma plane of the block's first sample.</param>
/// <param name="Qp">The luma quantiser.</param>
/// <param name="List">The 8x8 scaling list: 0 for intra, 1 for inter.</param>
internal readonly record struct H264BlockJob(int Group, int Offset, int Qp, int List);
