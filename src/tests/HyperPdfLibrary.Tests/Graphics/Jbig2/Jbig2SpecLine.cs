// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>One line of a standard Huffman table, as annex B of the JBIG2 standard lists it.</summary>
/// <param name="Low">RANGELOW: the first value of the line.</param>
/// <param name="PrefixLength">PREFLEN: the length of the line's prefix code.</param>
/// <param name="RangeLength">RANGELEN: the bits that follow the prefix.</param>
/// <param name="Kind">The kind of line.</param>
internal readonly record struct Jbig2SpecLine(int Low, int PrefixLength, int RangeLength, Jbig2SpecLineKind Kind);
