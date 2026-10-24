// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>One transform block of a colour component, with the intra mode that predicts it.</summary>
/// <param name="CIdx">The colour component.</param>
/// <param name="X">The column of its first sample in its colour plane.</param>
/// <param name="Y">The row of its first sample in its colour plane.</param>
/// <param name="Log2Size">The base 2 logarithm of its side.</param>
/// <param name="Mode">The intra prediction mode.</param>
internal readonly record struct H265TuBlock(int CIdx, int X, int Y, int Log2Size, int Mode);
