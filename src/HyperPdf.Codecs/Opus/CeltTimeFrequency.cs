// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// Changes the time and frequency resolution of a band (RFC 6716 section 4.3.4.5): neighbouring short blocks are recombined for
/// finer frequency, or a long block is split for finer time. The folded band below is reshaped the same way before it is used,
/// and the band is put back afterwards.
/// </summary>
internal static class CeltTimeFrequency
{
    /// <summary>The mask of the low four bits of a fill mask.</summary>
    private const int NibbleMask = 0xF;

    /// <summary>The shift to the high four bits of a fill mask.</summary>
    private const int NibbleShift = 4;

    /// <summary>The shift that puts the interleaved high nibble beside the low one.</summary>
    private const int NibblePair = 2;

    /// <summary>Gets the fill bits of one nibble after two blocks are recombined into one.</summary>
    private static ReadOnlySpan<byte> BitInterleave => [0x00, 0x01, 0x01, 0x01, 0x02, 0x03, 0x03, 0x03, 0x02, 0x03, 0x03, 0x03, 0x02, 0x03, 0x03, 0x03];

    /// <summary>Gets the collapse mask after one recombination is undone.</summary>
    private static ReadOnlySpan<byte> BitDeinterleave => [0x00, 0x03, 0x0C, 0x0F, 0x30, 0x33, 0x3C, 0x3F, 0xC0, 0xC3, 0xCC, 0xCF, 0xF0, 0xF3, 0xFC, 0xFF];

    /// <summary>Reshapes the folded band below, ready for the band to be read in the resolution it asks for.</summary>
    /// <param name="size">The entries in the band.</param>
    /// <param name="blocks">The short blocks the band is made of.</param>
    /// <param name="change">The time-frequency change: positive recombines blocks, negative splits them.</param>
    /// <param name="fill">The fill mask, updated.</param>
    /// <param name="lowband">The folded band; replaced by a reshaped copy in <paramref name="scratch"/> when it needs one.</param>
    /// <param name="scratch">Space for the reshaped copy.</param>
    /// <returns>What was done, so <see cref="Finish"/> can undo it.</returns>
    internal static CeltReshape Prepare(int size, int blocks, int change, ref int fill, ref Span<float> lowband, Span<float> scratch)
    {
        var sizeBlock = size / blocks;
        var longBlocks = blocks == 1;
        var recombine = Math.Max(change, 0);
        if (!lowband.IsEmpty && (blocks > 1 || recombine != 0 || ((sizeBlock & 1) == 0 && change < 0)))
        {
            lowband[..size].CopyTo(scratch);
            lowband = scratch;
        }

        fill = Recombine(lowband, size, recombine, fill);
        blocks >>= recombine;
        sizeBlock <<= recombine;
        var timeDivide = SplitBlocks(lowband, ref blocks, ref sizeBlock, ref fill, change);
        if (blocks > 1 && !lowband.IsEmpty)
        {
            CeltBandMath.Deinterleave(lowband, sizeBlock >> recombine, blocks << recombine, longBlocks);
        }

        return new(blocks, sizeBlock, recombine, timeDivide, longBlocks);
    }

    /// <summary>Puts a band back after it was read: undoes the reshaping and saves the band for later bands to fold from.</summary>
    /// <param name="shape">The band.</param>
    /// <param name="size">The entries in the band.</param>
    /// <param name="collapse">The collapse mask so far.</param>
    /// <param name="reshape">What <see cref="Prepare"/> did.</param>
    /// <param name="lowbandOut">Receives the scaled band for folding, or empty.</param>
    /// <returns>The collapse mask: a bit for each short block that has a pulse.</returns>
    internal static uint Finish(Span<float> shape, int size, uint collapse, CeltReshape reshape, Span<float> lowbandOut)
    {
        var blocks = reshape.Blocks;
        var sizeBlock = reshape.SizeBlock;
        if (blocks > 1)
        {
            CeltBandMath.Interleave(shape, sizeBlock >> reshape.Recombine, blocks << reshape.Recombine, reshape.LongBlocks);
        }

        for (var k = 0; k < reshape.TimeDivide; k++)
        {
            blocks >>= 1;
            sizeBlock <<= 1;
            collapse |= collapse >> blocks;
            CeltBandMath.Haar(shape, sizeBlock, blocks);
        }

        for (var k = 0; k < reshape.Recombine; k++)
        {
            collapse = BitDeinterleave[(int)collapse];
            CeltBandMath.Haar(shape, size >> k, 1 << k);
        }

        SaveForFolding(shape, size, lowbandOut);
        return collapse & ((1U << (blocks << reshape.Recombine)) - 1);
    }

    /// <summary>Saves a band, scaled to the size the folding expects, for later bands to fold from.</summary>
    /// <param name="shape">The band.</param>
    /// <param name="size">The entries in the band.</param>
    /// <param name="lowbandOut">Receives the scaled band, or empty.</param>
    private static void SaveForFolding(Span<float> shape, int size, Span<float> lowbandOut)
    {
        if (lowbandOut.IsEmpty)
        {
            return;
        }

        var scale = MathF.Sqrt(size);
        for (var j = 0; j < size; j++)
        {
            lowbandOut[j] = scale * shape[j];
        }
    }

    /// <summary>Recombines the blocks of the folded band the number of times asked for.</summary>
    /// <param name="lowband">The folded band, or empty.</param>
    /// <param name="size">The entries in the band.</param>
    /// <param name="recombine">The times to recombine.</param>
    /// <param name="fill">The fill mask.</param>
    /// <returns>The fill mask after recombining.</returns>
    private static int Recombine(Span<float> lowband, int size, int recombine, int fill)
    {
        for (var k = 0; k < recombine; k++)
        {
            if (!lowband.IsEmpty)
            {
                CeltBandMath.Haar(lowband, size >> k, 1 << k);
            }

            fill = BitInterleave[fill & NibbleMask] | (BitInterleave[fill >> NibbleShift] << NibblePair);
        }

        return fill;
    }

    /// <summary>Splits the blocks of the folded band for finer time resolution while the change asks for it.</summary>
    /// <param name="lowband">The folded band, or empty.</param>
    /// <param name="blocks">The blocks, updated.</param>
    /// <param name="sizeBlock">The entries in each block, updated.</param>
    /// <param name="fill">The fill mask, updated.</param>
    /// <param name="change">The time-frequency change.</param>
    /// <returns>How many times the blocks were split.</returns>
    private static int SplitBlocks(Span<float> lowband, ref int blocks, ref int sizeBlock, ref int fill, int change)
    {
        var divide = 0;
        while ((sizeBlock & 1) == 0 && change < 0)
        {
            if (!lowband.IsEmpty)
            {
                CeltBandMath.Haar(lowband, sizeBlock, blocks);
            }

            fill |= fill << blocks;
            blocks <<= 1;
            sizeBlock >>= 1;
            divide++;
            change++;
        }

        return divide;
    }
}
