// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What is known about a CELT band when it is read.</summary>
/// <param name="Band">The band number.</param>
/// <param name="Size">The entries in the band.</param>
/// <param name="Bits">The bits for the band, in eighths.</param>
/// <param name="Blocks">The short blocks the band is made of.</param>
/// <param name="TimeFrequency">The time-frequency change of the band.</param>
/// <param name="Level">How deep in splits the band is.</param>
/// <param name="Gain">The gain the band is scaled to.</param>
/// <param name="FrameSize">The log2 of the block size.</param>
/// <param name="Fill">The mask of blocks that may be filled by folding.</param>
internal readonly record struct CeltBandArgs(int Band, int Size, int Bits, int Blocks, int TimeFrequency, int Level, float Gain, int FrameSize, int Fill);
