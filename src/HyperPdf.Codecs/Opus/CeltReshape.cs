// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What was done to a band before it was read, so it can be undone afterwards.</summary>
/// <param name="Blocks">The short blocks of the band as it was read.</param>
/// <param name="SizeBlock">The entries in each block as it was read.</param>
/// <param name="Recombine">How many times blocks were recombined.</param>
/// <param name="TimeDivide">How many times blocks were split.</param>
/// <param name="LongBlocks">Whether the band was made of one block.</param>
internal readonly record struct CeltReshape(int Blocks, int SizeBlock, int Recombine, int TimeDivide, bool LongBlocks);
