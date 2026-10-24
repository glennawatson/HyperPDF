// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Reads a block of transform coefficient levels coded with CABAC (clause 7.3.5.3.3 and 9.3.3.1.3 of ITU-T H.264).</summary>
internal static class H264CabacResidual
{
    /// <summary>The block category of the DC levels of an Intra 16x16 macroblock.</summary>
    internal const int LumaDc = 0;

    /// <summary>The block category of the AC levels of an Intra 16x16 macroblock.</summary>
    internal const int LumaAc = 1;

    /// <summary>The block category of a luma block of 16 levels.</summary>
    internal const int Luma4x4 = 2;

    /// <summary>The block category of chroma DC levels.</summary>
    internal const int ChromaDc = 3;

    /// <summary>The block category of chroma AC levels.</summary>
    internal const int ChromaAc = 4;

    /// <summary>The block category of a luma block of 64 levels.</summary>
    internal const int Luma8x8 = 5;

    /// <summary>The levels in an 8x8 block.</summary>
    private const int LargeBlock = 64;

    /// <summary>The first context of significant_coeff_flag for 8x8 blocks in frame coded macroblocks.</summary>
    private const int Significant8x8Base = 402;

    /// <summary>The first context of last_significant_coeff_flag for 8x8 blocks in frame coded macroblocks.</summary>
    private const int Last8x8Base = 417;

    /// <summary>The first context of coeff_abs_level_minus1 for 8x8 blocks.</summary>
    private const int AbsLevel8x8Base = 426;

    /// <summary>The first context of coded_block_flag.</summary>
    private const int CodedBlockFlagBase = 85;

    /// <summary>The first context of significant_coeff_flag in frame coded macroblocks.</summary>
    private const int SignificantBase = 105;

    /// <summary>The first context of last_significant_coeff_flag in frame coded macroblocks.</summary>
    private const int LastBase = 166;

    /// <summary>The first context of coeff_abs_level_minus1.</summary>
    private const int AbsLevelBase = 227;

    /// <summary>The largest context increment of the significance flags of chroma DC levels.</summary>
    private const int ChromaDcMaxInc = 2;

    /// <summary>The most levels a block holds.</summary>
    private const int MaxLevels = 16;

    /// <summary>The longest prefix of coeff_abs_level_minus1 (cMax of the truncated unary code).</summary>
    private const int PrefixMax = 14;

    /// <summary>The offset of the contexts for the bins after the first of coeff_abs_level_minus1.</summary>
    private const int FurtherBinsOffset = 5;

    /// <summary>The most the count of levels above one adds to the context of the later bins.</summary>
    private const int MostGreaterThanOne = 4;

    /// <summary>The most the count of levels of one adds to the context of the first bin.</summary>
    private const int MostEqualToOne = 4;

    /// <summary>The longest Exp-Golomb suffix a valid stream has.</summary>
    private const int MaxSuffixBits = 24;

    /// <summary>Gets the context increment of significant_coeff_flag for each scan position of an 8x8 block in a frame coded macroblock (Table 9-43).</summary>
    private static ReadOnlySpan<byte> SignificantInc8x8 =>
    [
        0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x05, 0x04, 0x04, 0x03, 0x03, 0x04, 0x04, 0x04, 0x05, 0x05,
        0x04, 0x04, 0x04, 0x04, 0x03, 0x03, 0x06, 0x07, 0x07, 0x07, 0x08, 0x09, 0x0A, 0x09, 0x08, 0x07,
        0x07, 0x06, 0x0B, 0x0C, 0x0D, 0x0B, 0x06, 0x07, 0x08, 0x09, 0x0E, 0x0A, 0x09, 0x08, 0x06, 0x0B,
        0x0C, 0x0D, 0x0B, 0x06, 0x09, 0x0E, 0x0A, 0x09, 0x0B, 0x0C, 0x0D, 0x0B, 0x0E, 0x0A, 0x0C,
    ];

    /// <summary>Gets the context increment of last_significant_coeff_flag for each scan position of an 8x8 block (Table 9-43).</summary>
    private static ReadOnlySpan<byte> LastInc8x8 =>
    [
        0x00, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01,
        0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02, 0x02,
        0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04,
        0x05, 0x05, 0x05, 0x05, 0x06, 0x06, 0x06, 0x06, 0x07, 0x07, 0x07, 0x07, 0x08, 0x08, 0x08,
    ];

    /// <summary>Gets the context offset of coded_block_flag for each block category.</summary>
    private static ReadOnlySpan<byte> CodedBlockFlagOffset => [0x00, 0x04, 0x08, 0x0C, 0x10];

    /// <summary>Gets the context offset of significant_coeff_flag and last_significant_coeff_flag for each block category.</summary>
    private static ReadOnlySpan<byte> SignificantOffset => [0x00, 0x0F, 0x1D, 0x2C, 0x2F];

    /// <summary>Gets the context offset of coeff_abs_level_minus1 for each block category.</summary>
    private static ReadOnlySpan<byte> AbsLevelOffset => [0x00, 0x0A, 0x14, 0x1E, 0x27];

    /// <summary>Reads one block of levels.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="category">The block category, 0 to 4.</param>
    /// <param name="flagInc">The context increment of coded_block_flag, from the neighbouring blocks.</param>
    /// <param name="maxCoefficients">The most levels the block holds.</param>
    /// <param name="levels">Receives the levels in scan order; the first <paramref name="maxCoefficients"/> are set.</param>
    /// <returns>The number of non-zero levels, 0 when coded_block_flag is 0.</returns>
    /// <exception cref="InvalidDataException">The levels are damaged.</exception>
    internal static int Read(ref CabacEngine engine, int category, int flagInc, int maxCoefficients, scoped Span<int> levels)
    {
        levels[..maxCoefficients].Clear();
        if (engine.DecodeDecision(CodedBlockFlagBase + CodedBlockFlagOffset[category] + flagInc) == 0)
        {
            return 0;
        }

        Span<int> positions = stackalloc int[MaxLevels];
        var count = ReadSignificance(ref engine, category, maxCoefficients, positions);
        ReadLevels(ref engine, category, positions[..count], levels);
        return count;
    }

    /// <summary>Reads one block of 64 levels of a macroblock that uses the 8x8 transform. coded_block_flag is not sent for these blocks.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="levels">Receives the 64 levels in scan order.</param>
    /// <returns>The number of non-zero levels.</returns>
    /// <exception cref="InvalidDataException">The levels are damaged.</exception>
    internal static int Read8x8(ref CabacEngine engine, scoped Span<int> levels)
    {
        levels[..LargeBlock].Clear();
        Span<int> positions = stackalloc int[LargeBlock];
        var count = ReadSignificance8x8(ref engine, positions);
        ReadLevels(ref engine, Luma8x8, positions[..count], levels);
        return count;
    }

    /// <summary>Reads which levels of an 8x8 block are non-zero.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="positions">Receives the scan positions of the non-zero levels.</param>
    /// <returns>The number of non-zero levels.</returns>
    private static int ReadSignificance8x8(ref CabacEngine engine, scoped Span<int> positions)
    {
        var count = 0;
        for (var index = 0; index < LargeBlock - 1; index++)
        {
            if (engine.DecodeDecision(Significant8x8Base + SignificantInc8x8[index]) == 0)
            {
                continue;
            }

            positions[count] = index;
            count++;
            if (engine.DecodeDecision(Last8x8Base + LastInc8x8[index]) != 0)
            {
                return count;
            }
        }

        positions[count] = LargeBlock - 1;
        return count + 1;
    }

    /// <summary>Reads which levels are non-zero.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="category">The block category.</param>
    /// <param name="maxCoefficients">The most levels the block holds.</param>
    /// <param name="positions">Receives the scan positions of the non-zero levels.</param>
    /// <returns>The number of non-zero levels.</returns>
    private static int ReadSignificance(ref CabacEngine engine, int category, int maxCoefficients, scoped Span<int> positions)
    {
        var significantBase = SignificantBase + SignificantOffset[category];
        var lastBase = LastBase + SignificantOffset[category];
        var count = 0;
        for (var index = 0; index < maxCoefficients - 1; index++)
        {
            var inc = category == ChromaDc ? Math.Min(index, ChromaDcMaxInc) : index;
            if (engine.DecodeDecision(significantBase + inc) == 0)
            {
                continue;
            }

            positions[count] = index;
            count++;
            if (engine.DecodeDecision(lastBase + inc) != 0)
            {
                return count;
            }
        }

        positions[count] = maxCoefficients - 1;
        return count + 1;
    }

    /// <summary>Reads the values of the non-zero levels, last first.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="category">The block category.</param>
    /// <param name="positions">The scan positions of the non-zero levels.</param>
    /// <param name="levels">Receives the levels.</param>
    /// <exception cref="InvalidDataException">A level is too large.</exception>
    private static void ReadLevels(ref CabacEngine engine, int category, scoped ReadOnlySpan<int> positions, scoped Span<int> levels)
    {
        var absBase = category == Luma8x8 ? AbsLevel8x8Base : AbsLevelBase + AbsLevelOffset[category];
        var maxGreater = category == ChromaDc ? MostGreaterThanOne - 1 : MostGreaterThanOne;
        var equalToOne = 0;
        var greaterThanOne = 0;
        for (var k = positions.Length - 1; k >= 0; k--)
        {
            var firstInc = greaterThanOne != 0 ? 0 : Math.Min(MostEqualToOne, 1 + equalToOne);
            var magnitude = 1;
            if (engine.DecodeDecision(absBase + firstInc) != 0)
            {
                magnitude = ReadLargeMagnitude(ref engine, absBase + FurtherBinsOffset + Math.Min(maxGreater, greaterThanOne));
            }

            if (magnitude == 1)
            {
                equalToOne++;
            }
            else
            {
                greaterThanOne++;
            }

            levels[positions[k]] = engine.DecodeBypass() != 0 ? -magnitude : magnitude;
        }
    }

    /// <summary>Reads the rest of a coeff_abs_level_minus1 whose first bin was 1: the other prefix bins, then the Exp-Golomb suffix.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The context of the later prefix bins.</param>
    /// <returns>The magnitude of the level (coeff_abs_level_minus1 plus 1).</returns>
    /// <exception cref="InvalidDataException">The suffix is too long.</exception>
    private static int ReadLargeMagnitude(ref CabacEngine engine, int context)
    {
        var minus1 = 1;
        while (minus1 < PrefixMax && engine.DecodeDecision(context) != 0)
        {
            minus1++;
        }

        if (minus1 == PrefixMax)
        {
            minus1 += ReadExpGolombSuffix(ref engine);
        }

        return minus1 + 1;
    }

    /// <summary>Reads a zero-order Exp-Golomb code with bypass bins.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The code is too long.</exception>
    private static int ReadExpGolombSuffix(ref CabacEngine engine)
    {
        var value = 0;
        var bits = 0;
        while (engine.DecodeBypass() != 0)
        {
            value += 1 << bits;
            bits++;
            if (bits > MaxSuffixBits)
            {
                throw new InvalidDataException("An H.264 coefficient level is too large.");
            }
        }

        while (bits > 0)
        {
            bits--;
            value += engine.DecodeBypass() << bits;
        }

        return value;
    }
}
