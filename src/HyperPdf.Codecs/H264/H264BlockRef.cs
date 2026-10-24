// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>A 4x4 luma block of some macroblock, and whether its samples can be used.</summary>
/// <param name="Address">The address of the macroblock the block is in.</param>
/// <param name="Row">The block's row in that macroblock.</param>
/// <param name="Column">The block's column in that macroblock.</param>
/// <param name="Available">Whether the block can be used.</param>
internal readonly record struct H264BlockRef(int Address, int Row, int Column, bool Available);
