// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The reference picture lists of one slice, as the picture order counts of their pictures and whether each is a long-term picture.</summary>
/// <param name="Poc0">The order count of each picture in list 0.</param>
/// <param name="LongTerm0">Whether each picture in list 0 is long-term.</param>
/// <param name="Poc1">The order count of each picture in list 1.</param>
/// <param name="LongTerm1">Whether each picture in list 1 is long-term.</param>
internal sealed record H265SliceRefs(int[] Poc0, bool[] LongTerm0, int[] Poc1, bool[] LongTerm1);
