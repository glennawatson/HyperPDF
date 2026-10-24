// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>
/// The stereo state of the SILK layer (RFC 6716 section 4.2.8): the two channels are coded as mid and side, with the side predicted from
/// the mid by two filters whose weights change from frame to frame, and are turned back into left and right.
/// </summary>
internal sealed class SilkStereo
{
    /// <summary>The bits of every table's total.</summary>
    private const int TotalBits = 8;

    /// <summary>The share of the prediction step of each sub step, 0.1 in Q16.</summary>
    private const int SubStepSize = 6554;

    /// <summary>The number of weights joined into one symbol.</summary>
    private const int JointWidth = 5;

    /// <summary>The coarse levels of each predictor weight.</summary>
    private const int CoarseLevels = 3;

    /// <summary>The bits the mid signal is shifted up by in the prediction sum.</summary>
    private const int MidSumBits = 9;

    /// <summary>The bits the side signal is shifted up by in the prediction sum.</summary>
    private const int SideSumBits = 8;

    /// <summary>The bits the mid signal is shifted up by in the second term of the prediction sum.</summary>
    private const int MidTermBits = 11;

    /// <summary>The bits the prediction sum is shifted down by.</summary>
    private const int SumShift = 8;

    /// <summary>The fractional bits of the interpolation step.</summary>
    private const int StepBits = 16;

    /// <summary>The samples the previous frame's last two samples occupy in front of a frame.</summary>
    private const int History = 2;

    /// <summary>Gets the weights of the predictors of the frame before, in Q13.</summary>
    internal int[] PreviousPredictors { get; } = new int[SilkConstants.Pair];

    /// <summary>Gets the last two samples of the mid channel of the frame before.</summary>
    internal short[] MidHistory { get; } = new short[History];

    /// <summary>Gets the last two samples of the side channel of the frame before.</summary>
    internal short[] SideHistory { get; } = new short[History];

    /// <summary>Reads the weights of the two predictors of the side channel.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <param name="predictors">Receives the weights, the first less the second, in Q13.</param>
    internal static void ReadPredictors(ref OpusRangeDecoder decoder, scoped Span<int> predictors)
    {
        var joint = decoder.ReadSymbol(SilkTables.StereoPredictorJointIcdf, TotalBits);
        Span<int> coarse = stackalloc int[SilkConstants.Pair];
        Span<int> fine = stackalloc int[SilkConstants.Pair];
        Span<int> group = stackalloc int[SilkConstants.Pair];
        group[0] = joint / JointWidth;
        group[1] = joint - (JointWidth * group[0]);
        for (var n = 0; n < SilkConstants.Pair; n++)
        {
            coarse[n] = decoder.ReadSymbol(SilkTables.Uniform3Icdf, TotalBits);
            fine[n] = decoder.ReadSymbol(SilkTables.Uniform5Icdf, TotalBits);
        }

        var table = SilkTables.StereoPredictorQuant;
        for (var n = 0; n < SilkConstants.Pair; n++)
        {
            var index = coarse[n] + (CoarseLevels * group[n]);
            var low = table[index];
            var step = SilkMath.MultiplyWordBottom(table[index + 1] - low, SubStepSize);
            predictors[n] = SilkMath.MultiplyAddBottoms(low, step, (SilkConstants.Pair * fine[n]) + 1);
        }

        predictors[0] -= predictors[1];
    }

    /// <summary>Reads the flag that only the mid channel is coded in this frame.</summary>
    /// <param name="decoder">The range decoder.</param>
    /// <returns><see langword="true"/> when the side channel is silent.</returns>
    internal static bool ReadMidOnly(ref OpusRangeDecoder decoder) => decoder.ReadSymbol(SilkTables.StereoMidOnlyIcdf, TotalBits) != 0;

    /// <summary>Clears the weights and the history.</summary>
    internal void Reset()
    {
        Array.Clear(PreviousPredictors);
        Array.Clear(SideHistory);
    }

    /// <summary>Turns mid and side into left and right.</summary>
    /// <param name="mid">The mid channel with two samples of history in front; becomes the left channel.</param>
    /// <param name="side">The side channel with two samples of history in front; becomes the right channel.</param>
    /// <param name="predictors">The weights of the predictors for this frame, in Q13.</param>
    /// <param name="rateKhz">The internal sampling rate in kHz.</param>
    /// <param name="length">The samples of the frame.</param>
    internal void ToLeftRight(Span<short> mid, Span<short> side, ReadOnlySpan<int> predictors, int rateKhz, int length)
    {
        MidHistory.CopyTo(mid);
        SideHistory.CopyTo(side);
        mid.Slice(length, History).CopyTo(MidHistory);
        side.Slice(length, History).CopyTo(SideHistory);
        var blend = SilkConstants.StereoInterpolationMs * rateKhz;
        var denominator = (1 << StepBits) / blend;
        var delta0 = SilkMath.RoundShift(SilkMath.MultiplyBottoms(predictors[0] - PreviousPredictors[0], denominator), StepBits);
        var delta1 = SilkMath.RoundShift(SilkMath.MultiplyBottoms(predictors[1] - PreviousPredictors[1], denominator), StepBits);
        var weight0 = PreviousPredictors[0];
        var weight1 = PreviousPredictors[1];
        for (var n = 0; n < blend; n++)
        {
            weight0 += delta0;
            weight1 += delta1;
            Predict(mid, side, n, weight0, weight1);
        }

        for (var n = blend; n < length; n++)
        {
            Predict(mid, side, n, predictors[0], predictors[1]);
        }

        predictors[..SilkConstants.Pair].CopyTo(PreviousPredictors);
        for (var n = 0; n < length; n++)
        {
            var sum = mid[n + 1] + side[n + 1];
            var difference = mid[n + 1] - side[n + 1];
            mid[n + 1] = (short)SilkMath.Saturate16(sum);
            side[n + 1] = (short)SilkMath.Saturate16(difference);
        }
    }

    /// <summary>Adds the prediction from the mid channel to one sample of the side channel.</summary>
    /// <param name="mid">The mid channel.</param>
    /// <param name="side">The side channel.</param>
    /// <param name="n">The sample.</param>
    /// <param name="weight0">The weight of the low-passed mid, in Q13.</param>
    /// <param name="weight1">The weight of the mid itself, in Q13.</param>
    private static void Predict(Span<short> mid, Span<short> side, int n, int weight0, int weight1)
    {
        var smoothed = (mid[n] + mid[n + SilkConstants.Pair] + (mid[n + 1] << 1)) << MidSumBits;
        var sum = SilkMath.MultiplyAddWordBottom(side[n + 1] << SideSumBits, smoothed, weight0);
        sum = SilkMath.MultiplyAddWordBottom(sum, mid[n + 1] << MidTermBits, weight1);
        side[n + 1] = (short)SilkMath.Saturate16(SilkMath.RoundShift(sum, SumShift));
    }
}
