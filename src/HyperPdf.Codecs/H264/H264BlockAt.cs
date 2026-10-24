// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>A 4x4 luma block, named by its macroblock and its place in it.</summary>
/// <param name="Address">The macroblock's address.</param>
/// <param name="Column">The block's column in the macroblock, 0 to 3.</param>
/// <param name="Row">The block's row in the macroblock, 0 to 3.</param>
internal readonly record struct H264BlockAt(int Address, int Column, int Row);
