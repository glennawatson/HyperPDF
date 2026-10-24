// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Reads and resolves the angle that splits a CELT band between its two halves (RFC 6716 section 4.3.4.5).</summary>
internal static class CeltTheta
{
    /// <summary>A quarter turn, the largest angle.</summary>
    internal const int QuarterTurn = 0x4000;

    /// <summary>An eighth of a turn, the angle where the halves are equal.</summary>
    internal const int EighthTurn = 0x2000;

    /// <summary>The fractional bits of the bit counts.</summary>
    private const int FractionBits = CeltAllocation.FractionBits;

    /// <summary>The value of the largest gain, one below a full scale of 32768.</summary>
    private const int FullGain = 0x7FFF;

    /// <summary>The probability weight of the angles below the middle in a stereo band.</summary>
    private const int StereoStep = 3;

    /// <summary>The extra weight of the entries of a two entry band.</summary>
    private const int Pair = 2;

    /// <summary>The fractional bits of the shift that scales an angle step to a number of bits.</summary>
    private const int ScaleBits = 7;

    /// <summary>The bits kept back so the side of a stereo split can always code one pulse.</summary>
    private const int SideReserve = 4;

    /// <summary>The most bits of precision the angle gets.</summary>
    private const int MaxStepBits = 8;

    /// <summary>The mask of the eighths of a bit that choose a step size.</summary>
    private const int StepMask = 7;

    /// <summary>The shift that brings a step size to a whole number of steps.</summary>
    private const int StepShift = 14;

    /// <summary>The multiplier of the value under the square root that makes the root a whole number of steps.</summary>
    private const int RootScale = 8;

    /// <summary>Gets the sizes of the angle steps for each eighth of a bit.</summary>
    private static ReadOnlySpan<short> StepSizes => [0x4000, 0x45CA, 0x4C1B, 0x52FF, 0x5A82, 0x62B3, 0x6BA2, 0x7560];

    /// <summary>Works out how many steps the angle of a split is quantised to.</summary>
    /// <param name="size">The entries of each half.</param>
    /// <param name="bits">The bits of the band.</param>
    /// <param name="offset">The offset from the fair share.</param>
    /// <param name="pulseCap">The most pulse bits.</param>
    /// <param name="stereo">Whether the band is stereo.</param>
    /// <returns>The steps.</returns>
    internal static int ComputeSteps(int size, int bits, int offset, int pulseCap, bool stereo)
    {
        var n2 = (Pair * size) - 1;
        if (stereo && size == Pair)
        {
            n2--;
        }

        var qb = Math.Min(bits - pulseCap - (SideReserve << FractionBits), (bits + (n2 * offset)) / n2);
        qb = Math.Min(MaxStepBits << FractionBits, qb);
        if (qb < (1 << FractionBits >> 1))
        {
            return 1;
        }

        var qn = StepSizes[qb & StepMask] >> (StepShift - (qb >> FractionBits));
        return (qn + 1) >> 1 << 1;
    }

    /// <summary>Reads the index of the quantised angle with the distribution that suits the band.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="steps">The number of angle steps.</param>
    /// <param name="size">The entries of each half.</param>
    /// <param name="blocks">The short blocks the band started with.</param>
    /// <param name="stereo">Whether the band is stereo.</param>
    /// <returns>The index.</returns>
    internal static int ReadIndex(ref OpusRangeDecoder decoder, int steps, int size, int blocks, bool stereo)
    {
        if (stereo && size > Pair)
        {
            return ReadStereoIndex(ref decoder, steps);
        }

        return blocks > 1 || stereo ? (int)decoder.ReadUnsigned((uint)(steps + 1)) : ReadTriangularIndex(ref decoder, steps);
    }

    /// <summary>Works out the gains and bit split for an angle.</summary>
    /// <param name="angle">The angle.</param>
    /// <param name="size">The entries of each half.</param>
    /// <param name="fill">The fill mask.</param>
    /// <param name="blocks">The short blocks of each half.</param>
    /// <returns>The resolved angle.</returns>
    internal static CeltSplitAngle Resolve(int angle, int size, int fill, int blocks)
    {
        if (angle == 0)
        {
            return new(angle, FullGain, 0, -QuarterTurn, fill & ((1 << blocks) - 1));
        }

        if (angle == QuarterTurn)
        {
            return new(angle, 0, FullGain, QuarterTurn, fill & (((1 << blocks) - 1) << blocks));
        }

        var mid = CeltBandMath.BitExactCosine(angle);
        var side = CeltBandMath.BitExactCosine(QuarterTurn - angle);
        var delta = CeltBandMath.FractionMultiply((size - 1) << ScaleBits, CeltBandMath.BitExactLog2Tangent(side, mid));
        return new(angle, mid, side, delta, fill);
    }

    /// <summary>Reads the angle of a stereo band with a flat distribution up to the middle and a thin one after it.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="steps">The number of angle steps.</param>
    /// <returns>The index.</returns>
    private static int ReadStereoIndex(ref OpusRangeDecoder decoder, int steps)
    {
        var half = steps / Pair;
        var total = (StereoStep * (half + 1)) + half;
        var found = (int)decoder.Decode((uint)total);
        var index = found < (half + 1) * StereoStep ? found / StereoStep : half + 1 + (found - ((half + 1) * StereoStep));
        var low = index <= half ? StereoStep * index : index - 1 - half + ((half + 1) * StereoStep);
        var high = index <= half ? StereoStep * (index + 1) : index - half + ((half + 1) * StereoStep);
        decoder.Update((uint)low, (uint)high, (uint)total);
        return index;
    }

    /// <summary>Reads the angle of a mono band with a triangular distribution.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="steps">The number of angle steps.</param>
    /// <returns>The index.</returns>
    private static int ReadTriangularIndex(ref OpusRangeDecoder decoder, int steps)
    {
        var half = steps >> 1;
        var total = (half + 1) * (half + 1);
        var found = (int)decoder.Decode((uint)total);
        int index;
        int width;
        int start;
        if (found < ((half * (half + 1)) >> 1))
        {
            index = ((int)CeltBandMath.SquareRoot((uint)((RootScale * found) + 1)) - 1) >> 1;
            width = index + 1;
            start = (index * (index + 1)) >> 1;
        }
        else
        {
            index = ((Pair * (steps + 1)) - (int)CeltBandMath.SquareRoot((uint)((RootScale * (total - found - 1)) + 1))) >> 1;
            width = steps + 1 - index;
            start = total - (((steps + 1 - index) * (steps + Pair - index)) >> 1);
        }

        decoder.Update((uint)start, (uint)(start + width), (uint)total);
        return index;
    }
}
