// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Reads the CABAC syntax elements of inter macroblocks in P slices (binarizations of Tables 9-37 and 9-38, and clause 9.3.2).</summary>
internal static class H264CabacInter
{
    /// <summary>The mb_type that says the macroblock has a single 16x16 partition.</summary>
    internal const int Type16x16 = 0;

    /// <summary>The mb_type that says the macroblock has two 16x8 partitions.</summary>
    internal const int Type16x8 = 1;

    /// <summary>The mb_type that says the macroblock has two 8x16 partitions.</summary>
    internal const int Type8x16 = 2;

    /// <summary>The mb_type that says the macroblock has four 8x8 partitions.</summary>
    internal const int Type8x8 = 3;

    /// <summary>The value ReadMbType returns for an intra macroblock.</summary>
    internal const int IntraType = -1;

    /// <summary>The sub_mb_type of an unsplit 8x8 sub-macroblock.</summary>
    private const int Sub8x8 = 0;

    /// <summary>The sub_mb_type that splits a sub-macroblock into two 8x4 blocks.</summary>
    private const int Sub8x4 = 1;

    /// <summary>The sub_mb_type that splits a sub-macroblock into two 4x8 blocks.</summary>
    private const int Sub4x8 = 2;

    /// <summary>The sub_mb_type that splits a sub-macroblock into four 4x4 blocks.</summary>
    private const int Sub4x4 = 3;

    /// <summary>The context of mb_skip_flag in a P slice when no neighbour is coded.</summary>
    private const int SkipBase = 11;

    /// <summary>The context of mb_skip_flag in a B slice when no neighbour is coded.</summary>
    private const int SkipBaseB = 24;

    /// <summary>The first context of mb_type for B slices.</summary>
    private const int BMbTypeBase = 27;

    /// <summary>The context offset of the second bin of mb_type in a B slice.</summary>
    private const int BSecond = 3;

    /// <summary>The context offset of the third bin of mb_type in a B slice when the second was 0.</summary>
    private const int BAfterZero = 5;

    /// <summary>The context offset of the third bin of mb_type in a B slice when the second was 1.</summary>
    private const int BAfterOne = 4;

    /// <summary>The context offset of the later bins of mb_type in a B slice.</summary>
    private const int BLater = 5;

    /// <summary>The first mb_type of the B types coded as "1 1 0" followed by three bins.</summary>
    private const int BiFirst = 3;

    /// <summary>The first mb_type of the B types coded as "1 1 1 0" followed by three bins.</summary>
    private const int LongFirst = 12;

    /// <summary>The first mb_type of the B types coded as "1 1 1 1 0 0" followed by one bin.</summary>
    private const int BiBiFirst = 20;

    /// <summary>The mb_type coded as "1 1 1 1 1 0".</summary>
    private const int BEleven = 11;

    /// <summary>The mb_type of a B macroblock of four 8x8 partitions.</summary>
    private const int BEightByEight = 22;

    /// <summary>The first context of sub_mb_type in a B slice.</summary>
    private const int BSubBase = 36;

    /// <summary>The context offset of the later bins of sub_mb_type in a B slice.</summary>
    private const int BSubThird = 3;

    /// <summary>The first sub_mb_type of those coded as "1 1 0" followed by two bins.</summary>
    private const int BSubBiFirst = 3;

    /// <summary>The first sub_mb_type of those coded as "1 1 1 0" followed by two bins.</summary>
    private const int BSubLongFirst = 7;

    /// <summary>The first sub_mb_type of those coded as "1 1 1 1" followed by one bin.</summary>
    private const int BSubLast = 11;

    /// <summary>The context offset of the second bin of sub_mb_type in a B slice.</summary>
    private const int BSubSecond = 1;

    /// <summary>The context offset of the third bin of sub_mb_type in a B slice when the second was 1.</summary>
    private const int BSubFourth = 2;

    /// <summary>The context of the later bins of mb_type in a B slice.</summary>
    private const int BLaterContext = BMbTypeBase + BLater;

    /// <summary>The context of the later bins of sub_mb_type in a B slice.</summary>
    private const int BSubLaterContext = BSubBase + BSubThird;

    /// <summary>The two bins after "1 1 1 1" are both 1.</summary>
    private const int HighAndLow = 3;

    /// <summary>The two bins after "1 1 1 1" are 1 then 0.</summary>
    private const int HighOnly = 2;

    /// <summary>The two bins after "1 1 1 1" are 0 then 1.</summary>
    private const int LowOnly = 1;

    /// <summary>Three context coded bins.</summary>
    private const int ThreeBins = 3;

    /// <summary>Two context coded bins.</summary>
    private const int TwoBins = 2;

    /// <summary>The first context of mb_type for P slices.</summary>
    private const int MbTypeBase = 14;

    /// <summary>The context of the second bin of mb_type.</summary>
    private const int MbTypeSecond = 15;

    /// <summary>The context of the third bin of mb_type when the second bin was 0.</summary>
    private const int MbTypeThirdAfterZero = 16;

    /// <summary>The context of the third bin of mb_type when the second bin was 1.</summary>
    private const int MbTypeThirdAfterOne = 17;

    /// <summary>The first context of sub_mb_type.</summary>
    private const int SubTypeBase = 21;

    /// <summary>The context of the second bin of sub_mb_type.</summary>
    private const int SubTypeSecond = 22;

    /// <summary>The context of the third bin of sub_mb_type.</summary>
    private const int SubTypeThird = 23;

    /// <summary>The first context of ref_idx_l0.</summary>
    private const int RefIdxBase = 54;

    /// <summary>The context of the second bin of ref_idx_l0.</summary>
    private const int RefIdxSecond = 58;

    /// <summary>The context of the later bins of ref_idx_l0.</summary>
    private const int RefIdxLater = 59;

    /// <summary>The first context of mvd_l0 for the horizontal component.</summary>
    private const int MvdBaseX = 40;

    /// <summary>The first context of mvd_l0 for the vertical component.</summary>
    private const int MvdBaseY = 47;

    /// <summary>The prefix length at which the Exp-Golomb suffix of a motion vector difference starts.</summary>
    private const int MvdPrefixMax = 9;

    /// <summary>The sum of neighbouring differences below which the first bin of mvd uses its first context.</summary>
    private const int MvdLowSum = 3;

    /// <summary>The sum of neighbouring differences above which the first bin of mvd uses its last context.</summary>
    private const int MvdHighSum = 32;

    /// <summary>The context offset of the second bin of mvd.</summary>
    private const int MvdSecondOffset = 3;

    /// <summary>The context offset of the last distinct bin of mvd.</summary>
    private const int MvdLastOffset = 6;

    /// <summary>The order of the Exp-Golomb suffix of mvd.</summary>
    private const int MvdSuffixOrder = 3;

    /// <summary>The longest Exp-Golomb suffix a valid stream has.</summary>
    private const int MaxSuffixBits = 24;

    /// <summary>The most bins a valid ref_idx has.</summary>
    private const int MaxRefIdxBins = 32;

    /// <summary>The weight of the macroblock above in a context increment.</summary>
    private const int TopWeight = 2;

    /// <summary>Reads mb_skip_flag.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>Whether the macroblock is skipped.</returns>
    internal static bool ReadSkip(ref CabacEngine engine, H264Picture picture, int left, int top)
    {
        var inc = (left >= 0 && !picture.Skipped[left] ? 1 : 0) + (top >= 0 && !picture.Skipped[top] ? 1 : 0);
        return engine.DecodeDecision(SkipBase + inc) != 0;
    }

    /// <summary>Reads mb_skip_flag of a B slice.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>Whether the macroblock is skipped.</returns>
    internal static bool ReadSkipB(ref CabacEngine engine, H264Picture picture, int left, int top)
    {
        var inc = (left >= 0 && !picture.Skipped[left] ? 1 : 0) + (top >= 0 && !picture.Skipped[top] ? 1 : 0);
        return engine.DecodeDecision(SkipBaseB + inc) != 0;
    }

    /// <summary>Reads mb_type of a B slice (Table 9-37).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="picture">The picture.</param>
    /// <param name="left">The address of the left macroblock, or -1.</param>
    /// <param name="top">The address of the macroblock above, or -1.</param>
    /// <returns>0 to 22 for the inter types, or <see cref="IntraType"/> when the macroblock is intra coded and the rest of mb_type follows.</returns>
    internal static int ReadBMbType(ref CabacEngine engine, H264Picture picture, int left, int top)
    {
        var inc = (left >= 0 && !picture.DirectOnly[left] ? 1 : 0) + (top >= 0 && !picture.DirectOnly[top] ? 1 : 0);
        if (engine.DecodeDecision(BMbTypeBase + inc) == 0)
        {
            return 0;
        }

        if (engine.DecodeDecision(BMbTypeBase + BSecond) == 0)
        {
            return 1 + engine.DecodeDecision(BMbTypeBase + BAfterZero);
        }

        return engine.DecodeDecision(BMbTypeBase + BAfterOne) == 0
            ? BiFirst + ReadBits(ref engine, BLaterContext, ThreeBins)
            : ReadLongBType(ref engine);
    }

    /// <summary>Reads sub_mb_type of a B macroblock (Table 9-38).</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>0 to 12.</returns>
    internal static int ReadBSubType(ref CabacEngine engine)
    {
        if (engine.DecodeDecision(BSubBase) == 0)
        {
            return 0;
        }

        if (engine.DecodeDecision(BSubBase + BSubSecond) == 0)
        {
            return 1 + engine.DecodeDecision(BSubLaterContext);
        }

        if (engine.DecodeDecision(BSubBase + BSubFourth) == 0)
        {
            return BSubBiFirst + ReadBits(ref engine, BSubLaterContext, TwoBins);
        }

        return engine.DecodeDecision(BSubLaterContext) != 0
            ? BSubLast + engine.DecodeDecision(BSubLaterContext)
            : BSubLongFirst + ReadBits(ref engine, BSubLaterContext, TwoBins);
    }

    /// <summary>Reads mb_type of a P slice.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>0 to 3 for the inter shapes, or <see cref="IntraType"/> when the macroblock is intra coded and the rest of mb_type follows.</returns>
    internal static int ReadMbType(ref CabacEngine engine)
    {
        if (engine.DecodeDecision(MbTypeBase) != 0)
        {
            return IntraType;
        }

        if (engine.DecodeDecision(MbTypeSecond) == 0)
        {
            return engine.DecodeDecision(MbTypeThirdAfterZero) == 0 ? Type16x16 : Type8x8;
        }

        return engine.DecodeDecision(MbTypeThirdAfterOne) == 0 ? Type8x16 : Type16x8;
    }

    /// <summary>Reads sub_mb_type of a P macroblock.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>0 for 8x8, 1 for 8x4, 2 for 4x8, 3 for 4x4.</returns>
    internal static int ReadSubType(ref CabacEngine engine)
    {
        if (engine.DecodeDecision(SubTypeBase) != 0)
        {
            return Sub8x8;
        }

        if (engine.DecodeDecision(SubTypeSecond) == 0)
        {
            return Sub8x4;
        }

        return engine.DecodeDecision(SubTypeThird) != 0 ? Sub4x8 : Sub4x4;
    }

    /// <summary>Reads ref_idx_l0.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="inc">The context increment from the neighbouring partitions: 0 to 3.</param>
    /// <returns>The reference index.</returns>
    /// <exception cref="InvalidDataException">The index is too large.</exception>
    internal static int ReadRefIdx(ref CabacEngine engine, int inc)
    {
        if (engine.DecodeDecision(RefIdxBase + inc) == 0)
        {
            return 0;
        }

        var value = 1;
        var context = RefIdxSecond;
        while (engine.DecodeDecision(context) != 0)
        {
            value++;
            context = RefIdxLater;
            if (value > MaxRefIdxBins)
            {
                throw new InvalidDataException("An H.264 reference index is not valid.");
            }
        }

        return value;
    }

    /// <summary>Gets the context increment of ref_idx_l0 from the reference indices of the partitions to the left and above.</summary>
    /// <param name="left">The reference index of the partition to the left; anything but a positive index counts as 0.</param>
    /// <param name="top">The reference index of the partition above.</param>
    /// <returns>0 to 3.</returns>
    internal static int RefIdxInc(int left, int top) => (left > 0 ? 1 : 0) + (top > 0 ? TopWeight : 0);

    /// <summary>Reads a motion vector difference component.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="vertical">Whether this is the vertical component.</param>
    /// <param name="neighbourSum">The sum of the sizes of the differences of the blocks to the left and above.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="InvalidDataException">The value is too large.</exception>
    internal static int ReadMvd(ref CabacEngine engine, bool vertical, int neighbourSum)
    {
        var baseContext = vertical ? MvdBaseY : MvdBaseX;
        var inc = 1;
        if (neighbourSum < MvdLowSum)
        {
            inc = 0;
        }
        else if (neighbourSum > MvdHighSum)
        {
            inc = TopWeight;
        }

        if (engine.DecodeDecision(baseContext + inc) == 0)
        {
            return 0;
        }

        var value = 1;
        var context = baseContext + MvdSecondOffset;
        while (value < MvdPrefixMax && engine.DecodeDecision(context) != 0)
        {
            value++;
            context = Math.Min(context + 1, baseContext + MvdLastOffset);
        }

        if (value >= MvdPrefixMax)
        {
            value += ReadSuffix(ref engine);
        }

        return engine.DecodeBypass() != 0 ? -value : value;
    }

    /// <summary>Reads a fixed number of context coded bins and joins them into a number, first bin most significant.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <param name="context">The context of every bin.</param>
    /// <param name="count">The number of bins.</param>
    /// <returns>The number.</returns>
    private static int ReadBits(ref CabacEngine engine, int context, int count)
    {
        var value = 0;
        for (var index = 0; index < count; index++)
        {
            value = (value << 1) | engine.DecodeDecision(context);
        }

        return value;
    }

    /// <summary>Reads mb_type of a B slice after the bins "1 1 1" have been read.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>11 to 22, or <see cref="IntraType"/>.</returns>
    private static int ReadLongBType(ref CabacEngine engine)
    {
        if (engine.DecodeDecision(BLaterContext) == 0)
        {
            return LongFirst + ReadBits(ref engine, BLaterContext, ThreeBins);
        }

        var high = engine.DecodeDecision(BLaterContext);
        var low = engine.DecodeDecision(BLaterContext);
        return ((high << 1) | low) switch
        {
            HighAndLow => BEightByEight,
            HighOnly => BEleven,
            LowOnly => IntraType,
            _ => BiBiFirst + engine.DecodeDecision(BLaterContext),
        };
    }

    /// <summary>Reads the order-3 Exp-Golomb suffix of a motion vector difference with bypass bins.</summary>
    /// <param name="engine">The arithmetic decoder.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The code is too long.</exception>
    private static int ReadSuffix(ref CabacEngine engine)
    {
        var order = MvdSuffixOrder;
        var value = 0;
        while (engine.DecodeBypass() != 0)
        {
            value += 1 << order;
            order++;
            if (order > MaxSuffixBits)
            {
                throw new InvalidDataException("An H.264 motion vector difference is too large.");
            }
        }

        while (order > 0)
        {
            order--;
            value += engine.DecodeBypass() << order;
        }

        return value;
    }
}
