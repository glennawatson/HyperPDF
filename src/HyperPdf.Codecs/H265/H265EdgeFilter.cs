// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>Filters one segment of a block edge: four samples along the edge for luma, fewer for subsampled chroma (8.7.2.5.3 to 8.7.2.5.8).</summary>
internal static class H265EdgeFilter
{
    /// <summary>The samples along a luma edge segment.</summary>
    private const int LumaSegment = 4;

    /// <summary>The last sample along a luma edge segment.</summary>
    private const int LastLumaSample = LumaSegment - 1;

    /// <summary>The bit depth the thresholds in the tables are given for.</summary>
    private const int BaseBitDepth = 8;

    /// <summary>The offset a luma quantiser index gets for a strength of 2 or more.</summary>
    private const int StrengthStep = 2;

    /// <summary>The factor of the tc threshold on the flatness test.</summary>
    private const int FlatFactor = 5;

    /// <summary>The factor of tc that the weak filter's change must stay below.</summary>
    private const int WeakFactor = 10;

    /// <summary>The offset a chroma edge adds to its quantiser index.</summary>
    private const int ChromaStrength = 2;

    /// <summary>The shift of beta that the bend of a row must stay below for the strong filter.</summary>
    private const int BendShift = 2;

    /// <summary>The shift of beta that the step across the samples on each side must stay below for the strong filter.</summary>
    private const int StepShift = 3;

    /// <summary>The shift that turns beta into the limit for filtering the second samples.</summary>
    private const int SecondLimitShift = 3;

    /// <summary>The distance of the fourth sample from the edge.</summary>
    private const int FourthSample = 4;

    /// <summary>The rounding added to a sum of eight weights before it is shifted.</summary>
    private const int EighthRound = 4;

    /// <summary>The shift of a sum of eight weights.</summary>
    private const int EighthShift = 3;

    /// <summary>The rounding added to a sum of four weights before it is shifted.</summary>
    private const int QuarterRound = 2;

    /// <summary>The shift of a sum of four weights.</summary>
    private const int QuarterShift = 2;

    /// <summary>The weight of the nearest sample in the weak filter's step.</summary>
    private const int NearWeight = 9;

    /// <summary>The weight of the second sample in the weak filter's step.</summary>
    private const int FarWeight = 3;

    /// <summary>The rounding of the weak filter's step.</summary>
    private const int WeakRound = 8;

    /// <summary>The shift of the weak filter's step.</summary>
    private const int WeakShift = 4;

    /// <summary>The rounding of the chroma filter's step.</summary>
    private const int ChromaRound = 4;

    /// <summary>The shift of the chroma filter's step.</summary>
    private const int ChromaShift = 3;

    /// <summary>The weight of the nearest sample in the chroma filter's step: the shift that multiplies a difference by four.</summary>
    private const int ChromaNearShift = 2;

    /// <summary>The weight of the third sample in the strong filter.</summary>
    private const int ThirdWeight = 3;

    /// <summary>Filters a luma edge segment of four rows or columns.</summary>
    /// <param name="segment">Where the segment is.</param>
    /// <param name="parameters">The settings.</param>
    internal static void FilterLuma(H265EdgeSegment segment, in H265EdgeParameters parameters)
    {
        var scale = 1 << (parameters.BitDepth - BaseBitDepth);
        var beta = H265DeblockTables.BetaOf(Math.Clamp(parameters.QpAverage + (parameters.BetaOffsetDiv2 << 1), 0, H265DeblockTables.MaxBetaIndex)) * scale;
        var tc = H265DeblockTables.TcOf(Math.Clamp(parameters.QpAverage + (StrengthStep * (parameters.Strength - 1)) + (parameters.TcOffsetDiv2 << 1), 0, H265DeblockTables.MaxTcIndex)) * scale;
        var plane = segment.Plane;
        var across = segment.Across;
        var q0 = segment.Q0;
        var last = q0 + (LastLumaSample * segment.Along);
        var dp0 = SecondDifference(plane, q0 - across, -across);
        var dp3 = SecondDifference(plane, last - across, -across);
        var dq0 = SecondDifference(plane, q0, across);
        var dq3 = SecondDifference(plane, last, across);
        var dpq0 = dp0 + dq0;
        var dpq3 = dp3 + dq3;
        if (dpq0 + dpq3 >= beta)
        {
            return;
        }

        var strong = IsFlat(plane, q0, across, dpq0 << 1, beta, tc) && IsFlat(plane, last, across, dpq3 << 1, beta, tc);
        var limit = (beta + (beta >> 1)) >> SecondLimitShift;
        var limits = new H265WeakLimits(tc, dp0 + dp3 < limit, dq0 + dq3 < limit, (1 << parameters.BitDepth) - 1);
        for (var k = 0; k < LumaSegment; k++)
        {
            var position = q0 + (k * segment.Along);
            if (strong)
            {
                StrongFilter(plane, position, across, tc, parameters);
                continue;
            }

            WeakFilter(plane, position, across, limits, parameters);
        }
    }

    /// <summary>Filters a chroma edge segment (8.7.2.5.5 and 8.7.2.5.8).</summary>
    /// <param name="segment">Where the segment is.</param>
    /// <param name="length">The samples along the segment.</param>
    /// <param name="parameters">The settings; the quantiser average is already mapped to the chroma quantiser.</param>
    internal static void FilterChroma(H265EdgeSegment segment, int length, in H265EdgeParameters parameters)
    {
        var scale = 1 << (parameters.BitDepth - BaseBitDepth);
        var tc = H265DeblockTables.TcOf(Math.Clamp(parameters.QpAverage + ChromaStrength + (parameters.TcOffsetDiv2 << 1), 0, H265DeblockTables.MaxTcIndex)) * scale;
        var maximum = (1 << parameters.BitDepth) - 1;
        var plane = segment.Plane;
        var across = segment.Across;
        var secondAcross = across << 1;
        for (var k = 0; k < length; k++)
        {
            var q0 = segment.Q0 + (k * segment.Along);
            var p0Value = plane[q0 - across];
            var q0Value = plane[q0];
            var delta = Math.Clamp(((((q0Value - p0Value) << ChromaNearShift) + plane[q0 - secondAcross] - plane[q0 + across] + ChromaRound) >> ChromaShift), -tc, tc);
            if (!parameters.KeepP)
            {
                plane[q0 - across] = (ushort)Math.Clamp(p0Value + delta, 0, maximum);
            }

            if (!parameters.KeepQ)
            {
                plane[q0] = (ushort)Math.Clamp(q0Value - delta, 0, maximum);
            }
        }
    }

    /// <summary>Measures how much the samples on one side of the edge bend: |s2 - 2 s1 + s0|.</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="origin">The index of the sample nearest the edge on that side.</param>
    /// <param name="step">The distance from one sample to the next one further from the edge: negative for the P side.</param>
    /// <returns>The measure.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int SecondDifference(ushort[] plane, int origin, int step) =>
        Math.Abs(plane[origin + (step << 1)] - (plane[origin + step] << 1) + plane[origin]);

    /// <summary>Tells whether the samples near a row or column of the edge are flat enough for the strong filter (8.7.2.5.6).</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="q0">The index of the first Q sample of the row or column.</param>
    /// <param name="across">The distance between samples across the edge.</param>
    /// <param name="dpq">Twice the sum of the bends on both sides.</param>
    /// <param name="beta">The strength threshold.</param>
    /// <param name="tc">The clipping threshold.</param>
    /// <returns><see langword="true"/> when the row or column is flat.</returns>
    private static bool IsFlat(ushort[] plane, int q0, int across, int dpq, int beta, int tc)
    {
        var p0 = plane[q0 - across];
        var p3 = plane[q0 - (FourthSample * across)];
        var q0Value = plane[q0];
        var q3 = plane[q0 + (LastLumaSample * across)];
        return dpq < (beta >> BendShift) && Math.Abs(p3 - p0) + Math.Abs(q0Value - q3) < (beta >> StepShift) && Math.Abs(p0 - q0Value) < (((FlatFactor * tc) + 1) >> 1);
    }

    /// <summary>Applies the strong filter to one row or column of samples (8-389 to 8-394).</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="q0">The index of the first Q sample.</param>
    /// <param name="across">The distance between samples across the edge.</param>
    /// <param name="tc">The clipping threshold.</param>
    /// <param name="parameters">The settings.</param>
    private static void StrongFilter(ushort[] plane, int q0, int across, int tc, in H265EdgeParameters parameters)
    {
        var second = across << 1;
        var third = second + across;
        int p0 = plane[q0 - across];
        int p1 = plane[q0 - second];
        int p2 = plane[q0 - third];
        int p3 = plane[q0 - (second << 1)];
        int q0Value = plane[q0];
        int q1 = plane[q0 + across];
        int q2 = plane[q0 + second];
        int q3 = plane[q0 + third];
        var range = tc << 1;
        if (!parameters.KeepP)
        {
            plane[q0 - across] = (ushort)Math.Clamp((p2 + (p1 << 1) + (p0 << 1) + (q0Value << 1) + q1 + EighthRound) >> EighthShift, p0 - range, p0 + range);
            plane[q0 - second] = (ushort)Math.Clamp((p2 + p1 + p0 + q0Value + QuarterRound) >> QuarterShift, p1 - range, p1 + range);
            plane[q0 - third] = (ushort)Math.Clamp(((p3 << 1) + (ThirdWeight * p2) + p1 + p0 + q0Value + EighthRound) >> EighthShift, p2 - range, p2 + range);
        }

        if (parameters.KeepQ)
        {
            return;
        }

        plane[q0] = (ushort)Math.Clamp((p1 + (p0 << 1) + (q0Value << 1) + (q1 << 1) + q2 + EighthRound) >> EighthShift, q0Value - range, q0Value + range);
        plane[q0 + across] = (ushort)Math.Clamp((p0 + q0Value + q1 + q2 + QuarterRound) >> QuarterShift, q1 - range, q1 + range);
        plane[q0 + second] = (ushort)Math.Clamp((p0 + q0Value + q1 + (ThirdWeight * q2) + (q3 << 1) + EighthRound) >> EighthShift, q2 - range, q2 + range);
    }

    /// <summary>Applies the weak filter to one row or column of samples (8-395 to 8-402).</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="q0">The index of the first Q sample.</param>
    /// <param name="across">The distance between samples across the edge.</param>
    /// <param name="limits">The thresholds and which second samples may change.</param>
    /// <param name="parameters">The settings.</param>
    private static void WeakFilter(ushort[] plane, int q0, int across, H265WeakLimits limits, in H265EdgeParameters parameters)
    {
        var second = across << 1;
        int p0 = plane[q0 - across];
        int p1 = plane[q0 - second];
        int p2 = plane[q0 - second - across];
        int q0Value = plane[q0];
        int q1 = plane[q0 + across];
        int q2 = plane[q0 + second];
        var delta = ((NearWeight * (q0Value - p0)) - (FarWeight * (q1 - p1)) + WeakRound) >> WeakShift;
        if (Math.Abs(delta) >= limits.Tc * WeakFactor)
        {
            return;
        }

        var tc = limits.Tc;
        delta = Math.Clamp(delta, -tc, tc);
        var half = tc >> 1;
        if (!parameters.KeepP)
        {
            plane[q0 - across] = (ushort)Math.Clamp(p0 + delta, 0, limits.Maximum);
            if (limits.FilterP1)
            {
                var change = Math.Clamp((((p2 + p0 + 1) >> 1) - p1 + delta) >> 1, -half, half);
                plane[q0 - second] = (ushort)Math.Clamp(p1 + change, 0, limits.Maximum);
            }
        }

        if (parameters.KeepQ)
        {
            return;
        }

        plane[q0] = (ushort)Math.Clamp(q0Value - delta, 0, limits.Maximum);
        if (limits.FilterQ1)
        {
            var change = Math.Clamp((((q2 + q0Value + 1) >> 1) - q1 - delta) >> 1, -half, half);
            plane[q0 + across] = (ushort)Math.Clamp(q1 + change, 0, limits.Maximum);
        }
    }
}
